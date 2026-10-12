namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>The request/answer exchange with the host process: the JSON line protocol, timeouts and the description of what came back.</summary>
	public sealed partial class OutOfProcessNativeParser
	{
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

		private static async Task ObserveAsync(Task task)
		{
			Task finished = await Task.WhenAny(task, Task.Delay(_exitWait)).ConfigureAwait(false);
			if (ReferenceEquals(finished, task) && task.IsFaulted)
			{
				// Observe the failure of the abandoned exchange so that it is not reported as unobserved.
				_ = task.Exception;
			}
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
