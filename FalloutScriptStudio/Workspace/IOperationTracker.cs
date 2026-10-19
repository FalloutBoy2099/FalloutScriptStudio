namespace FalloutScriptStudio.Workspace
{
	using System;

	/// <summary>
	/// Knows the one long operation that may run at a time (a build, a mass compilation, a decompilation). The status bar shows it and
	/// offers the Cancel command; a second operation is refused while one runs.
	/// </summary>
	public interface IOperationTracker
	{
		/// <summary>Raised when an operation started or ended, or its progress or cancelling state changed.</summary>
		event EventHandler? Changed;

		/// <summary>Gets a value indicating whether an operation is running.</summary>
		Boolean IsRunning { get; }

		/// <summary>Gets what the running operation is doing, or <see langword="null"/> when none runs.</summary>
		OperationInfo? Current { get; }

		/// <summary>Starts an operation.</summary>
		/// <param name="kind">The kind of operation.</param>
		/// <param name="subject">The file being worked on, if any.</param>
		/// <param name="total">The number of files of the run, or 0.</param>
		/// <returns>The operation, or <see langword="null"/> when another one is running.</returns>
		IOperation? TryBegin(OperationKind kind, String? subject = null, Int32 total = 0);

		/// <summary>Cancels the running operation.</summary>
		/// <returns><see langword="true"/> when an operation was running and was asked to stop.</returns>
		Boolean Cancel();
	}
}