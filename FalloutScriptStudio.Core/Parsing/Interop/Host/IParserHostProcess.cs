namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// A running parser host, as seen by <see cref="OutOfProcessNativeParser"/>. Abstracted so that crashes, hangs and garbage can
	/// be simulated in tests without starting processes.
	/// </summary>
	public interface IParserHostProcess : IDisposable
	{
		/// <summary>Gets the writer connected to the standard input of the host (one request per line, flushed after every line).</summary>
		TextWriter Input { get; }

		/// <summary>Gets the reader connected to the standard output of the host.</summary>
		TextReader Output { get; }

		/// <summary>Gets a value indicating whether the host process has ended.</summary>
		Boolean HasExited { get; }

		/// <summary>Gets the exit code of the host, or <c>null</c> while it is running.</summary>
		Int32? ExitCode { get; }

		/// <summary>Gets the last lines the host wrote to its standard error stream (diagnostics only).</summary>
		String StandardErrorTail { get; }

		/// <summary>
		/// Gets a task that completes when the host has written a fatal-error report (for example <c>Fatal error. 0xC0000005</c>) to its error
		/// stream. The process is going down, but it may stay alive for a long time, or forever, in the crash reporting of the operating
		/// system (when Windows Error Reporting is disabled, a crashed process can hang); the caller should kill it.
		/// </summary>
		Task FatalErrorReported { get; }

		/// <summary>Kills the host and everything it started. Safe to call repeatedly and on a host that has already exited.</summary>
		void Kill();

		/// <summary>Waits for the host to end.</summary>
		/// <param name="cancellationToken">Cancels the wait.</param>
		/// <returns>A task that completes when the host has ended.</returns>
		Task WaitForExitAsync(CancellationToken cancellationToken);
	}
}
