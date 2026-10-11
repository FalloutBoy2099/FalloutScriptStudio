namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// <see cref="INativeParser"/> that runs <c>parser.dll</c> in a separate 32-bit process (<c>FalloutScriptStudio.ParserHost.exe</c>)
	/// so that a crash of the native library only loses the request being served.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Design: one long-lived host is started on the first request and kept for the following ones (JSON lines over its standard
	/// input and output, see <see cref="HostProtocol"/>). Starting a .NET process and loading the library costs several hundred
	/// milliseconds, which would be paid on every pause in typing if a process were started per request. Requests are serialized
	/// (the library keeps global state and shares one scratch file anyway).
	/// </para>
	/// <para>
	/// A request that ends in a crash (non-zero exit, closed pipe), a timeout, a cancellation or an invalid answer kills the host
	/// process tree and drops it; the next request starts a new host automatically. If a host that was idle turns out to be dead when
	/// the request is written, the request is repeated once on a fresh host (it was never read). The host is also replaced after
	/// <see cref="NativeParserHostOptions.RecycleAfterRequests"/> requests. When hosts keep dying before answering anything
	/// (missing runtime, blocked executable), the parser reports <see cref="NativeParseStatus.Unavailable"/> for a while instead of
	/// starting a doomed process for every parse.
	/// </para>
	/// <para>
	/// The method never throws because of the host: problems become <see cref="NativeParseStatus.Failed"/> or
	/// <see cref="NativeParseStatus.Unavailable"/> results with an explanation. Only a cancelled token throws
	/// <see cref="OperationCanceledException"/>, after the host has been killed.
	/// </para>
	/// </remarks>
	public sealed class OutOfProcessNativeParser : INativeParser, IDisposable
	{
		private const Int32 MaxNoiseCharacters = 1 << 20;
		private const Int32 MaxQuotedCharacters = 300;
		private const Int32 MaxHostStartFailures = 2;
		private const Int32 MaxRememberedTexts = 64;

		private static readonly TimeSpan _exitWait = TimeSpan.FromSeconds(2);
		private static readonly TimeSpan _fatalErrorGrace = TimeSpan.FromMilliseconds(500);
		private static readonly Regex _reportedCode = new Regex(@"^0x(?<code>[0-9A-Fa-f]{8})\b", RegexOptions.Multiline | RegexOptions.CultureInvariant);

		private readonly NativeParserHostOptions _options;
		private readonly IParserHostLauncher _launcher;
		private readonly ILogger _logger;
		private readonly TimeProvider _timeProvider;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		private IParserHostProcess? _host;
		private String? _hostDirectory;
		private Int32 _hostRequests;
		private Boolean _hostAnswered;
		private Int32 _startFailures;
		private DateTimeOffset _unavailableUntil;
		private String _unavailableMessage = String.Empty;
		private Int64 _lastId;
		private Int32 _consecutiveCrashes;
		private String? _disabledReason;
		private String _currentLibrary = String.Empty;
		private String _disabledLibrary = String.Empty;
		private DateTime _disabledLibraryStamp;
		private readonly HashSet<String> _crashingTexts = new HashSet<String>(StringComparer.Ordinal);
		private Boolean _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="OutOfProcessNativeParser"/> class.
		/// </summary>
		/// <param name="options">Where the host is and how long it may take.</param>
		/// <param name="logger">Optional logger.</param>
		/// <param name="launcher">Starts the host processes; the real executable is started when omitted.</param>
		/// <param name="timeProvider">The clock used for the start failure back-off; the system clock when omitted.</param>
		public OutOfProcessNativeParser(
			NativeParserHostOptions options,
			ILogger<OutOfProcessNativeParser>? logger = null,
			IParserHostLauncher? launcher = null,
			TimeProvider? timeProvider = null)
		{
			ArgumentNullException.ThrowIfNull(options);
			this._options = options;
			this._logger = logger ?? NullLogger<OutOfProcessNativeParser>.Instance;
			this._launcher = launcher ?? new ProcessParserHostLauncher();
			this._timeProvider = timeProvider ?? TimeProvider.System;
		}

		/// <inheritdoc />
		public async Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(filePath);
			ArgumentNullException.ThrowIfNull(options);
			cancellationToken.ThrowIfCancellationRequested();
			if (this._disposed)
			{
				return Unavailable("The native parser has been shut down.");
			}

			NativeParseResult? unavailable = this.CheckAvailability(options, out String libraryPath, out String hostPath);
			if (unavailable is not null)
			{
				return unavailable;
			}

			String fullFilePath;
			try
			{
				fullFilePath = Path.GetFullPath(filePath);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return Failed("The script path is invalid: " + ex.Message);
			}

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (this._disposed)
				{
					return Unavailable("The native parser has been shut down.");
				}

				String crashKey = CreateCrashKey(scriptText, fullFilePath);
				NativeParseResult? blocked = this.CheckCrashBreaker(libraryPath, crashKey);
				if (blocked is not null)
				{
					return blocked;
				}

				return await this.ParseCoreAsync(scriptText, fullFilePath, options with { NativeParserPath = libraryPath }, hostPath, crashKey, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				this._gate.Release();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the crash breaker has turned the native parser off for this session; every parse then reports
		/// <see cref="NativeParseStatus.Unavailable"/> and the built-in parser is used.
		/// </summary>
		public Boolean IsDisabled => this._disabledReason is not null;

		/// <summary>Gets why the native parser was turned off, or <c>null</c>.</summary>
		public String? DisabledReason => this._disabledReason;

		/// <summary>Stops the host process.</summary>
		public void Dispose()
		{
			if (this._disposed)
			{
				return;
			}

			this._disposed = true;
			this.DiscardHost();
		}

		private static NativeParseResult Unavailable(String message)
		{
			return new NativeParseResult(NativeParseStatus.Unavailable) { Message = message };
		}

		private static NativeParseResult Failed(String message)
		{
			return new NativeParseResult(NativeParseStatus.Failed) { Message = message };
		}

		private static String Describe(IParserHostProcess host)
		{
			String? stderr = Quote(host.StandardErrorTail);
			return stderr is null ? String.Empty : " Error output of the host: " + stderr;
		}

		private static String? Quote(String? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			String trimmed = text.Trim().Replace("\r\n", " | ", StringComparison.Ordinal).Replace('\n', ' ');
			return trimmed.Length <= MaxQuotedCharacters ? trimmed : trimmed.Substring(trimmed.Length - MaxQuotedCharacters);
		}

		private static String DescribeExitCode(Int32 exitCode)
		{
			String hex = "0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture);
			return unchecked((UInt32)exitCode) switch
			{
				0xC0000005 => hex + ", access violation",
				0xC00000FD => hex + ", stack overflow",
				0xC0000374 => hex + ", heap corruption",
				0xC000001D => hex + ", illegal instruction",
				0xC0000409 => hex + ", stack buffer overrun",
				0xE0434352 => hex + ", unhandled .NET exception",
				0x80008096 or 0x80008083 or 0x8000808C => hex + ", the .NET runtime of the host could not be found",
				_ => exitCode < 0 ? hex : exitCode.ToString(CultureInfo.InvariantCulture),
			};
		}

		private static async Task ObserveAsync(Task task)
		{
			Task finished = await Task.WhenAny(task, Task.Delay(_exitWait)).ConfigureAwait(false);
			if (ReferenceEquals(finished, task) && task.IsFaulted)
			{
				// Observe the failure of the abandoned exchange so that it is not reported as unobserved.
				_ = task.Exception;
			}
		}

		private NativeParseResult? CheckAvailability(ParserOptions options, out String libraryPath, out String hostPath)
		{
			libraryPath = String.Empty;
			hostPath = String.Empty;

			if (String.IsNullOrWhiteSpace(options.NativeParserPath))
			{
				return Unavailable("The path of the native parser (parser.dll) is not configured.");
			}

			try
			{
				libraryPath = Path.GetFullPath(options.NativeParserPath);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return Unavailable("The path of the native parser is invalid: " + ex.Message);
			}

			if (!File.Exists(libraryPath))
			{
				return Unavailable("The native parser was not found: '" + libraryPath + "'.");
			}

			if (String.IsNullOrWhiteSpace(this._options.HostPath))
			{
				return Unavailable("The path of the native parser host (" + NativeParserHostOptions.HostFileName + ") is not configured.");
			}

			try
			{
				hostPath = Path.GetFullPath(this._options.HostPath);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return Unavailable("The path of the native parser host is invalid: " + ex.Message);
			}

			if (!File.Exists(hostPath))
			{
				return Unavailable("The native parser host was not found: '" + hostPath + "'.");
			}

			if (this._startFailures >= MaxHostStartFailures)
			{
				if (this._timeProvider.GetUtcNow() < this._unavailableUntil)
				{
					return Unavailable(this._unavailableMessage);
				}

				// The back-off is over: one more failure puts the parser back to sleep.
				this._startFailures = MaxHostStartFailures - 1;
			}

			return null;
		}

		private async Task<NativeParseResult> ParseCoreAsync(String scriptText, String filePath, ParserOptions options, String hostPath, String crashKey, CancellationToken cancellationToken)
		{
			for (Int32 attempt = 0; ; attempt++)
			{
				Boolean reused;
				IParserHostProcess host;
				try
				{
					host = this.AcquireHost(hostPath, out reused);
				}
				catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
				{
					this._logger.LogError(ex, "The native parser host '{Host}' could not be started", hostPath);
					String message = "The native parser host could not be started: " + ex.Message;
					this.RegisterStartFailure(message);
					return Failed(message);
				}

				Exchange exchange;
				if (!this._hostAnswered)
				{
					// A new host first has to prove that it can run at all (right runtime, not blocked): a host that dies here is a
					// start failure, whereas a host that dies while parsing is a crash of the native library.
					var ping = new HostRequest { Id = ++this._lastId, Command = HostProtocol.PingCommand };
					exchange = await this.ExchangeAsync(host, HostProtocol.SerializeRequest(ping), ping.Id, cancellationToken).ConfigureAwait(false);
					if (exchange.Kind != ExchangeKind.Answered)
					{
						NativeParseResult? startFailure = await this.HandleFailureAsync(exchange, host, reused, attempt, isStart: true, null).ConfigureAwait(false);
						if (startFailure is null)
						{
							continue;
						}

						return startFailure;
					}

					this._hostAnswered = true;
					this._startFailures = 0;
				}

				HostRequest request = HostMapper.ToRequest(++this._lastId, scriptText, filePath, options);
				exchange = await this.ExchangeAsync(host, HostProtocol.SerializeRequest(request), request.Id, cancellationToken).ConfigureAwait(false);
				if (exchange.Kind == ExchangeKind.Answered)
				{
					this._hostRequests++;
					this._consecutiveCrashes = 0;
					return exchange.Result!;
				}

				NativeParseResult? failure = await this.HandleFailureAsync(exchange, host, reused, attempt, isStart: false, crashKey).ConfigureAwait(false);
				if (failure is not null)
				{
					return failure;
				}
			}
		}

		/// <summary>Turns an unsuccessful exchange into a result and drops the host; <c>null</c> asks the caller to repeat the request on a fresh host.</summary>
		private async Task<NativeParseResult?> HandleFailureAsync(Exchange exchange, IParserHostProcess host, Boolean reused, Int32 attempt, Boolean isStart, String? crashKey)
		{
			switch (exchange.Kind)
			{
				case ExchangeKind.WriteFailed when reused && attempt == 0:
					// An idle host died between two requests; the request was not read, so a fresh host can take it.
					this._logger.LogWarning("The native parser host was gone when a request was sent ({Reason}); restarting it", exchange.Reason);
					this.DiscardHost();
					return null;

				case ExchangeKind.WriteFailed:
				{
					String message = "The request could not be sent to the native parser host: " + exchange.Reason;
					this.DiscardHost();
					return this.Fail(message, isStart, null);
				}

				case ExchangeKind.HostEnded:
				{
					String message = await this.DescribeEndedHostAsync(host, exchange.Reason).ConfigureAwait(false);
					this._logger.LogError("{Message}", message);
					this.DiscardHost();
					return this.Fail(message, isStart, crashKey);
				}

				case ExchangeKind.TimedOut:
				{
					String message = "The native parser host did not answer within "
						+ this._options.RequestTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)
						+ " seconds and was stopped." + Describe(host);
					this._logger.LogError("{Message}", message);
					this.DiscardHost();
					return this.Fail(message, isStart, crashKey);
				}

				default:
				{
					String message = "The native parser host sent an invalid answer (" + exchange.Reason + "); it was restarted." + Describe(host);
					this._logger.LogError("{Message}", message);
					this.DiscardHost();
					return this.Fail(message, isStart, null);
				}
			}
		}

		private NativeParseResult Fail(String message, Boolean isStart, String? crashKey)
		{
			if (isStart)
			{
				this.RegisterStartFailure(message);
			}

			if (crashKey is not null)
			{
				message = this.RegisterCrash(message, crashKey);
			}

			return Failed(message);
		}

		private static String CreateCrashKey(String scriptText, String filePath)
		{
			Byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath + "\n" + scriptText));
			return System.Convert.ToHexString(hash);
		}

		/// <summary>
		/// Counts a crash (or hang) of the native library while parsing. The text is remembered so that the same text is not sent again,
		/// and after <see cref="NativeParserHostOptions.MaxConsecutiveCrashes"/> crashes in a row the native parser is turned off.
		/// </summary>
		private String RegisterCrash(String message, String crashKey)
		{
			if (this._crashingTexts.Count >= MaxRememberedTexts)
			{
				this._crashingTexts.Clear();
			}

			this._crashingTexts.Add(crashKey);
			this._consecutiveCrashes++;
			if (this._options.MaxConsecutiveCrashes <= 0 || this._consecutiveCrashes < this._options.MaxConsecutiveCrashes)
			{
				return message;
			}

			this._disabledReason = "The native parser (parser.dll) crashed or hung " + this._consecutiveCrashes.ToString(CultureInfo.InvariantCulture)
				+ " times in a row, so it is turned off until the application is restarted (or parser.dll is replaced). The built-in parser is used instead.";
			this._disabledLibrary = this._currentLibrary;
			this._disabledLibraryStamp = File.GetLastWriteTimeUtc(this._currentLibrary);
			this._logger.LogError("{Reason} Last problem: {Message}", this._disabledReason, message);
			return message + " " + this._disabledReason;
		}

		/// <summary>
		/// Returns the result to use instead of running the native parser when the crash breaker has tripped or the very same text
		/// already crashed it; <c>null</c> lets the parse go ahead.
		/// </summary>
		private NativeParseResult? CheckCrashBreaker(String libraryPath, String crashKey)
		{
			this._currentLibrary = libraryPath;
			if (this._disabledReason is not null)
			{
				if (String.Equals(this._disabledLibrary, libraryPath, StringComparison.OrdinalIgnoreCase)
					&& this._disabledLibraryStamp == File.GetLastWriteTimeUtc(libraryPath))
				{
					return Unavailable(this._disabledReason);
				}

				// A different (or replaced) parser.dll gets a fresh start.
				this._disabledReason = null;
				this._disabledLibrary = String.Empty;
				this._consecutiveCrashes = 0;
				this._crashingTexts.Clear();
			}

			if (this._crashingTexts.Contains(crashKey))
			{
				return Failed("The native parser crashed on exactly this text before, so it is not run on it again until the text changes.");
			}

			return null;
		}

		private async Task<Exchange> ExchangeAsync(IParserHostProcess host, String requestLine, Int64 id, CancellationToken cancellationToken)
		{
			Task<Exchange> work = RunExchangeAsync(host, requestLine, id);
			try
			{
				Task first = await Task.WhenAny(work, host.FatalErrorReported).WaitAsync(this._options.RequestTimeout, cancellationToken).ConfigureAwait(false);
				if (ReferenceEquals(first, work))
				{
					return await work.ConfigureAwait(false);
				}

				// The host reported a fatal error: it normally ends by itself right away, but when the crash reporting of the operating
				// system is disabled or stuck, the dead-man process stays around. Give it a moment, then put it down.
				await Task.WhenAny(work, Task.Delay(_fatalErrorGrace, cancellationToken)).ConfigureAwait(false);
				host.Kill();
				await ObserveAsync(work).ConfigureAwait(false);
				return new Exchange(ExchangeKind.HostEnded, null, String.Empty);
			}
			catch (TimeoutException)
			{
				host.Kill();
				await ObserveAsync(work).ConfigureAwait(false);
				return new Exchange(ExchangeKind.TimedOut, null, String.Empty);
			}
			catch (OperationCanceledException)
			{
				host.Kill();
				this.DiscardHost();
				await ObserveAsync(work).ConfigureAwait(false);
				throw;
			}
		}


		private static async Task<Exchange> RunExchangeAsync(IParserHostProcess host, String requestLine, Int64 id)
		{
			try
			{
				try
				{
					await host.Input.WriteLineAsync(requestLine).ConfigureAwait(false);
					await host.Input.FlushAsync().ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IOException or ObjectDisposedException)
				{
					return new Exchange(ExchangeKind.WriteFailed, null, ex.Message);
				}

				Int32 noiseCharacters = 0;
				String? firstNoise = null;
				while (true)
				{
					String? line = await host.Output.ReadLineAsync().ConfigureAwait(false);
					if (line is null)
					{
						return new Exchange(ExchangeKind.HostEnded, null, Quote(firstNoise) is { } noise ? "Output before the end: " + noise : String.Empty);
					}

					HostResponse? response;
					try
					{
						if (!HostProtocol.TryReadResponse(line, out response))
						{
							// Whatever the native library prints to the shared standard output; not part of the protocol.
							firstNoise ??= line;
							noiseCharacters += line.Length;
							if (noiseCharacters > MaxNoiseCharacters)
							{
								return new Exchange(ExchangeKind.InvalidResponse, null, "more than " + MaxNoiseCharacters.ToString(CultureInfo.InvariantCulture) + " characters without a response; first line: " + Quote(firstNoise));
							}

							continue;
						}
					}
					catch (Exception ex) when (ex is System.Text.Json.JsonException or NullReferenceException)
					{
						return new Exchange(ExchangeKind.InvalidResponse, null, "not valid JSON: " + ex.Message);
					}

					return Convert(response!, id);
				}
			}
			catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
			{
				return new Exchange(ExchangeKind.HostEnded, null, ex.Message);
			}
		}

		private static Exchange Convert(HostResponse response, Int64 id)
		{
			if (response.Version != HostProtocol.Version)
			{
				return new Exchange(ExchangeKind.InvalidResponse, null, "protocol version " + response.Version.ToString(CultureInfo.InvariantCulture));
			}

			if (response.Id != id)
			{
				return new Exchange(ExchangeKind.InvalidResponse, null, "answer to request " + response.Id.ToString(CultureInfo.InvariantCulture) + " instead of " + id.ToString(CultureInfo.InvariantCulture));
			}

			try
			{
				return new Exchange(ExchangeKind.Answered, HostMapper.ToResult(response), String.Empty);
			}
			catch (Exception ex) when (ex is FormatException or NullReferenceException or ArgumentException)
			{
				return new Exchange(ExchangeKind.InvalidResponse, null, ex.Message);
			}
		}

		private async Task<String> DescribeEndedHostAsync(IParserHostProcess host, String detail)
		{
			Int32? exitCode = host.ExitCode;
			if (exitCode is null)
			{
				using var wait = new CancellationTokenSource(_exitWait);
				try
				{
					await host.WaitForExitAsync(wait.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					host.Kill();
				}

				exitCode = host.ExitCode;
			}

			// A host that was killed after its fatal-error report has the exit code of the kill; the report names the real one.
			Match reported = _reportedCode.Match(host.StandardErrorTail);
			if (reported.Success)
			{
				exitCode = unchecked((Int32)UInt32.Parse(reported.Groups["code"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
			}

			var message = new StringBuilder("The native parser host stopped unexpectedly");
			if (exitCode is { } code)
			{
				message.Append(" (exit code ").Append(DescribeExitCode(code)).Append(')');
			}

			message.Append('.').Append(Describe(host));
			if (detail.Length > 0)
			{
				message.Append(' ').Append(detail);
			}

			return message.ToString();
		}


		private IParserHostProcess AcquireHost(String hostPath, out Boolean reused)
		{
			IParserHostProcess? current = this._host;
			if (current is not null)
			{
				Boolean worn = this._options.RecycleAfterRequests > 0 && this._hostRequests >= this._options.RecycleAfterRequests;
				if (!current.HasExited && !worn)
				{
					reused = true;
					return current;
				}

				this.DiscardHost();
			}

			String root = this._options.WorkingDirectoryRoot ?? Path.GetTempPath();
			String directory = Path.Combine(root, "FalloutScriptStudio", "parser-host-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);

			IParserHostProcess host;
			try
			{
				host = this._launcher.Start(hostPath, directory);
			}
			catch
			{
				TryDeleteDirectory(directory);
				throw;
			}

			this._host = host;
			this._hostDirectory = directory;
			this._hostRequests = 0;
			this._hostAnswered = false;
			reused = false;
			return host;
		}

		private void DiscardHost()
		{
			IParserHostProcess? host = Interlocked.Exchange(ref this._host, null);
			String? directory = this._hostDirectory;
			this._hostDirectory = null;
			if (host is not null)
			{
				host.Kill();
				host.Dispose();
			}

			if (directory is not null)
			{
				TryDeleteDirectory(directory);
			}
		}

		private void RegisterStartFailure(String message)
		{
			this._startFailures++;
			if (this._startFailures >= MaxHostStartFailures)
			{
				this._unavailableUntil = this._timeProvider.GetUtcNow() + this._options.StartFailureBackoff;
				this._unavailableMessage = "The native parser host keeps failing before it answers, so it is not used for "
					+ this._options.StartFailureBackoff.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)
					+ " seconds. Last problem: " + message;
			}
		}

		private static void TryDeleteDirectory(String directory)
		{
			try
			{
				Directory.Delete(directory, true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A leftover temporary folder is harmless.
			}
		}

		private enum ExchangeKind
		{
			Answered,
			WriteFailed,
			HostEnded,
			TimedOut,
			InvalidResponse,
		}

		private sealed record Exchange(ExchangeKind Kind, NativeParseResult? Result, String Reason);
	}
}
