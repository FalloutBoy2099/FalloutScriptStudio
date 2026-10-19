namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="IParserHostProcess"/> over a real operating system process.
	/// </summary>
	internal sealed class ProcessParserHost : IParserHostProcess
	{
		private const Int32 StandardErrorLines = 20;

		private static readonly String[] _fatalMarkers = { "Fatal error", "Stack overflow", "Unhandled exception", "Process terminated" };

		private readonly Process _process;
		private readonly Queue<String> _errorLines = new Queue<String>();
		private readonly TaskCompletionSource _fatalError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		private HostJob? _job;
		private Boolean _disposed;

		private ProcessParserHost(Process process)
		{
			this._process = process;
		}

		/// <inheritdoc />
		public Task FatalErrorReported => this._fatalError.Task;

		/// <summary>Gets the operating system id of the process.</summary>
		internal Int32 ProcessId { get; private set; }

		/// <inheritdoc />
		public TextWriter Input => this._process.StandardInput;

		/// <inheritdoc />
		public TextReader Output => this._process.StandardOutput;

		/// <inheritdoc />
		public Boolean HasExited
		{
			get
			{
				if (this._disposed)
				{
					// Disposing the host ends the process (it is killed when it does not leave on its own).
					return true;
				}

				try
				{
					return this._process.HasExited;
				}
				catch (InvalidOperationException)
				{
					return true;
				}
			}
		}

		/// <inheritdoc />
		public Int32? ExitCode
		{
			get
			{
				try
				{
					return this._process.HasExited ? this._process.ExitCode : null;
				}
				catch (InvalidOperationException)
				{
					return null;
				}
			}
		}

		/// <inheritdoc />
		public String StandardErrorTail
		{
			get
			{
				lock (this._errorLines)
				{
					return String.Join(Environment.NewLine, this._errorLines);
				}
			}
		}

		/// <summary>Starts the host executable with redirected standard streams and no window.</summary>
		/// <exception cref="Win32Exception">The process could not be started.</exception>
		internal static ProcessParserHost Start(String hostPath, String workingDirectory, ILogger logger)
		{
			var startInfo = new ProcessStartInfo(hostPath)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = workingDirectory,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardInputEncoding = HostProtocol.Encoding,
				StandardOutputEncoding = HostProtocol.Encoding,
				StandardErrorEncoding = HostProtocol.Encoding,
			};

			ConfigureRuntimeLocation(startInfo);

			// The host ends by itself when this process disappears (a parse stuck in the native library never sees the closed pipe).
			startInfo.ArgumentList.Add("--parent");
			startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

			var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
			var host = new ProcessParserHost(process);
			process.ErrorDataReceived += host.OnErrorData;
			try
			{
				process.Start();
			}
			catch
			{
				process.Dispose();
				throw;
			}

			process.BeginErrorReadLine();
			host.ProcessId = process.Id;
			host._job = HostJob.TryCreate(process, logger);
			return host;
		}

		/// <summary>
		/// Makes sure the 32-bit host looks for a 32-bit .NET runtime. The host is an apphost: it reads <c>DOTNET_ROOT</c>, and a 64-bit
		/// parent (a test runner, <c>dotnet run</c>) exports the folder of the 64-bit runtime, which a 32-bit process cannot load.
		/// </summary>
		private static void ConfigureRuntimeLocation(ProcessStartInfo startInfo)
		{
			startInfo.Environment.Remove("DOTNET_ROOT");
			startInfo.Environment.Remove("DOTNET_ROOT(x86)");
			startInfo.Environment.Remove("DOTNET_ROOT_X86");

			if (Environment.Is64BitProcess)
			{
				// The default locations (registry, "Program Files (x86)\dotnet") are searched.
				return;
			}

			// This process is 32-bit, so its own runtime is the right one: <root>\shared\Microsoft.NETCore.App\<version>\.
			DirectoryInfo runtime = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar));
			DirectoryInfo? root = runtime.Parent?.Parent?.Parent;
			if (root is not null && File.Exists(Path.Combine(root.FullName, "dotnet.exe")))
			{
				startInfo.Environment["DOTNET_ROOT_X86"] = root.FullName;
			}
		}

		/// <inheritdoc />
		public void Kill()
		{
			try
			{
				if (!this._process.HasExited)
				{
					this._process.Kill(entireProcessTree: true);
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
			{
				// The process has already ended or cannot be killed; nothing more can be done.
			}
		}

		/// <inheritdoc />
		public Task WaitForExitAsync(CancellationToken cancellationToken)
		{
			return this._process.WaitForExitAsync(cancellationToken);
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (this._disposed)
			{
				return;
			}

			this._disposed = true;
			try
			{
				// Closing the standard input is the polite way to ask the host to end.
				this._process.StandardInput.Close();
				if (!this._process.WaitForExit(300))
				{
					this.Kill();
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception)
			{
				this.Kill();
			}

			this._job?.Dispose();
			this._process.Dispose();
		}

		private void OnErrorData(Object sender, DataReceivedEventArgs e)
		{
			if (e.Data is null)
			{
				return;
			}

			lock (this._errorLines)
			{
				this._errorLines.Enqueue(e.Data);
				while (this._errorLines.Count > StandardErrorLines)
				{
					this._errorLines.Dequeue();
				}
			}

			// Queued first: the caller reads the error tail as soon as the task completes.
			if (Array.Exists(_fatalMarkers, marker => e.Data.StartsWith(marker, StringComparison.Ordinal)))
			{
				this._fatalError.TrySetResult();
			}
		}
	}
}