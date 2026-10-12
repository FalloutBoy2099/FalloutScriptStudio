namespace FalloutScriptStudio.Workspace
{
	using System;

	/// <summary>What the running operation is doing right now (structured, so that the status bar can write it in the current language).</summary>
	/// <param name="Kind">The kind of operation.</param>
	/// <param name="Subject">The file being worked on (a name), or an empty string.</param>
	/// <param name="Index">The 1-based position of the file in the run, or 0 when the run has no count.</param>
	/// <param name="Total">The number of files of the run, or 0 when unknown.</param>
	/// <param name="IsCancelling">Whether the user asked to cancel and the operation is stopping.</param>
	public sealed record OperationInfo(OperationKind Kind, String Subject, Int32 Index, Int32 Total, Boolean IsCancelling)
	{
		/// <summary>Gets the progress from 0 to 1, or -1 when the run has no count.</summary>
		public Double Fraction => this.Total > 0 ? Math.Clamp((Double)this.Index / this.Total, 0, 1) : -1;
	}
}
