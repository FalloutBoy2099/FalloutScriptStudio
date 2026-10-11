namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// <see cref="IProcessRunner"/> that starts real operating system processes without a console window.
	/// </summary>
	public sealed class ProcessRunner : IProcessRunner
	{
		/// <inheritdoc/>
		public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			ProcessStartInfo startInfo = new ProcessStartInfo(request.FileName, request.Arguments)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = request.CaptureOutput,
			};

			if (request.WorkingDirectory != null)
			{
				startInfo.WorkingDirectory = request.WorkingDirectory;
			}

			if (request.CaptureOutput)
			{
				startInfo.StandardOutputEncoding = request.OutputEncoding ?? TextEncodings.Ansi;
			}

			using Process process = new Process { StartInfo = startInfo };
			process.Start();

			try
			{
				Task<String> outputTask = request.CaptureOutput
					? ReadOutputAsync(process.StandardOutput, request.OutputLineProgress, cancellationToken)
					: Task.FromResult(String.Empty);

				await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
				String output = await outputTask.ConfigureAwait(false);
				return new ProcessRunResult(process.ExitCode, output);
			}
			catch (OperationCanceledException)
			{
				TryKill(process);
				throw;
			}
		}

		private static async Task<String> ReadOutputAsync(StreamReader reader, IProgress<String>? progress, CancellationToken cancellationToken)
		{
			if (progress == null)
			{
				return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
			}

			StringBuilder output = new StringBuilder();
			String? line;
			while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
			{
				output.Append(line).Append(Environment.NewLine);
				progress.Report(line);
			}

			return output.ToString();
		}

		private static void TryKill(Process process)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill(entireProcessTree: true);
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
			{
				// The process already exited or cannot be killed; nothing more can be done.
			}
		}
	}
}
