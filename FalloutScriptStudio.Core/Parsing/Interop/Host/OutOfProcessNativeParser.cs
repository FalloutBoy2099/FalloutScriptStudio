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
	public sealed partial class OutOfProcessNativeParser : INativeParser, IDisposable
	{
		private const Int32 MaxNoiseCharacters = 1 << 20;
		private const Int32 MaxQuotedCharacters = 300;
		private const Int32 MaxHostStartFailures = 2;
		private const Int32 MaxRememberedTexts = 64;

		private static readonly TimeSpan StaleHostDirectoryAge = TimeSpan.FromHours(1);
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
		private Boolean _sweptHostFolders;
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
		private readonly HashSet<String> _crashingTexts = new(StringComparer.Ordinal);
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
					this._logger.HostNotStarted(ex, hostPath);
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
	}
}