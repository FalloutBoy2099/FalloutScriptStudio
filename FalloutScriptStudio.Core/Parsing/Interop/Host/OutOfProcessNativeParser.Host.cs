namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>The life cycle of the host process: starting, recycling, discarding and describing how it ended.</summary>
	public sealed partial class OutOfProcessNativeParser
	{
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

		private static async Task<String> DescribeEndedHostAsync(IParserHostProcess host, String detail)
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
	}
}
