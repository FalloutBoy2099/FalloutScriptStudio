namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// <see cref="IProcessRunner"/> that starts real operating system processes without a console window. Cancelling ends the whole
	/// process tree (the tool, the <c>cmd.exe</c> that runs a batch file, and whatever the batch file started) and returns only after
	/// the processes are gone, so the caller may clean up the files they were writing.
	/// </summary>
	public sealed class ProcessRunner : IProcessRunner
	{
		private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(5);

		/// <inheritdoc/>
		public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);
			cancellationToken.ThrowIfCancellationRequested();

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

			if (request.Environment != null)
			{
				foreach (KeyValuePair<String, String> variable in request.Environment)
				{
					startInfo.Environment[variable.Key] = variable.Value;
				}
			}

			if (request.CaptureOutput)
			{
				startInfo.StandardOutputEncoding = request.OutputEncoding ?? TextEncodings.Ansi;
			}

			using Process process = new Process { StartInfo = startInfo };
			process.Start();

			// Closing the job ends the whole tree: it is the safety net for every way out except a normal end.
			using ProcessTreeJob? job = ProcessTreeJob.TryCreate(process);
			Task<String>? outputTask = null;
			try
			{
				outputTask = request.CaptureOutput
					? ReadOutputAsync(process.StandardOutput, request.OutputLineProgress, cancellationToken)
					: null;

				await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
				String output = outputTask is null ? String.Empty : await outputTask.ConfigureAwait(false);
				job?.Release();
				return new ProcessRunResult(process.ExitCode, output);
			}
			catch
			{
				await StopAsync(process, job, outputTask).ConfigureAwait(false);
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

		private static async Task StopAsync(Process process, ProcessTreeJob? job, Task<String>? outputTask)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill(entireProcessTree: true);
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException)
			{
				// The process already exited or part of the tree cannot be killed this way; the job below takes care of the rest.
			}

			job?.Terminate();

			// The files the tool was writing must be free when the caller cleans up: wait for the tool and for everything it started.
			using CancellationTokenSource timeout = new CancellationTokenSource(KillTimeout);
			try
			{
				await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
			{
				// It did not end in time (or never really started); nothing more can be done.
			}

			if (job is not null)
			{
				await job.WaitUntilEmptyAsync(KillTimeout).ConfigureAwait(false);
			}

			if (outputTask is not null)
			{
				try
				{
					await outputTask.WaitAsync(KillTimeout).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException or ObjectDisposedException or InvalidOperationException)
				{
					// The reader was cancelled together with the run or is still blocked by a surviving child: it is abandoned.
				}
			}
		}
	}
}
