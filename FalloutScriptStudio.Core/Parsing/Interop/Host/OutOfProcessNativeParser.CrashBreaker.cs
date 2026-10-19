namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>Handling of failed exchanges and the crash breaker that turns the native parser off after repeated crashes.</summary>
	public sealed partial class OutOfProcessNativeParser
	{
		/// <summary>Turns an unsuccessful exchange into a result and drops the host; <c>null</c> asks the caller to repeat the request on a fresh host.</summary>
		private async Task<NativeParseResult?> HandleFailureAsync(Exchange exchange, IParserHostProcess host, Boolean reused, Int32 attempt, Boolean isStart, String? crashKey)
		{
			switch (exchange.Kind)
			{
				case ExchangeKind.WriteFailed when reused && attempt == 0:
					// An idle host died between two requests; the request was not read, so a fresh host can take it.
					this._logger.HostGoneOnSend(exchange.Reason);
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
					String message = await DescribeEndedHostAsync(host, exchange.Reason).ConfigureAwait(false);
					this._logger.HostFailed(message);
					this.DiscardHost();
					return this.Fail(message, isStart, crashKey);
				}

				case ExchangeKind.TimedOut:
				{
					String message = "The native parser host did not answer within "
						+ this._options.RequestTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)
						+ " seconds and was stopped." + Describe(host);
					this._logger.HostFailed(message);
					this.DiscardHost();
					return this.Fail(message, isStart, crashKey);
				}

				default:
				{
					String message = "The native parser host sent an invalid answer (" + exchange.Reason + "); it was restarted." + Describe(host);
					this._logger.HostFailed(message);
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
			this._logger.ParserDisabled(this._disabledReason, message);
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
	}
}