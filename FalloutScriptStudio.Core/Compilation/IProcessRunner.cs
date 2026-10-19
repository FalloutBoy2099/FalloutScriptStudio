namespace FalloutScriptStudio.Core.Compilation
{
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Runs external console tools. Abstracted so that the compiler services can be tested without the real tools.
	/// </summary>
	public interface IProcessRunner
	{
		/// <summary>
		/// Starts a process, waits for it to exit and returns its exit code and output.
		/// </summary>
		/// <param name="request">What to run.</param>
		/// <param name="cancellationToken">Cancels the wait and kills the process tree.</param>
		/// <returns>The result of the run.</returns>
		/// <exception cref="System.ComponentModel.Win32Exception">The process could not be started.</exception>
		/// <exception cref="System.OperationCanceledException">The operation was cancelled.</exception>
		Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default);
	}
}