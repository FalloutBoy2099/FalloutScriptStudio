namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading;

	/// <summary>A running long operation. Disposing it (always, in a <c>finally</c> or a <c>using</c>) ends it and frees the status bar.</summary>
	public interface IOperation : IDisposable
	{
		/// <summary>Gets the token that is cancelled when the user cancels the operation.</summary>
		CancellationToken Token { get; }

		/// <summary>Tells the status bar which file is being worked on.</summary>
		/// <param name="subject">The file name.</param>
		/// <param name="index">The 1-based position of the file in the run, or 0.</param>
		/// <param name="total">The number of files of the run, or 0.</param>
		void Report(String subject, Int32 index = 0, Int32 total = 0);
	}
}
