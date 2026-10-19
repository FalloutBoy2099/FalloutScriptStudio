namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>The files that formatting would change.</summary>
	/// <param name="Changes">The files that would change, ordered by name.</param>
	/// <param name="Examined">The number of files that were looked at.</param>
	/// <param name="Unreadable">The number of files that could not be read or are too big (each one is a problem in the list of problems).</param>
	/// <param name="Cancelled">Whether the user cancelled before every file was looked at.</param>
	public sealed record ProjectFormatPreview(IReadOnlyList<FormatFileChange> Changes, Int32 Examined, Int32 Unreadable, Boolean Cancelled)
	{
		/// <summary>Gets the number of files that were looked at and need no change.</summary>
		public Int32 Unchanged => this.Examined - this.Changes.Count - this.Unreadable;
	}
}