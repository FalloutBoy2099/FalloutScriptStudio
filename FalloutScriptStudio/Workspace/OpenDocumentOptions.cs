namespace FalloutScriptStudio.Workspace
{
	using System;

	/// <summary>How a file is opened by <see cref="IWorkspaceManager.OpenAsync(String, OpenDocumentOptions)"/>.</summary>
	public sealed record OpenDocumentOptions
	{
		/// <summary>Gets the 1-based line to go to, or -1.</summary>
		public Int32 Line { get; init; } = -1;

		/// <summary>Gets the 1-based column to go to, or -1.</summary>
		public Int32 Column { get; init; } = -1;

		/// <summary>Gets the number of characters to select, or -1.</summary>
		public Int32 SelectLength { get; init; } = -1;

		/// <summary>Gets a value indicating whether the file is added to the recent files list.</summary>
		public Boolean AddToRecent { get; init; } = true;

		/// <summary>Gets a value indicating whether the new tab becomes the selected tab.</summary>
		public Boolean Activate { get; init; } = true;

		/// <summary>Gets a value indicating whether the user is asked to open a copy when the file is already open.</summary>
		public Boolean PromptIfAlreadyOpen { get; init; }

		/// <summary>Gets a value indicating whether the file comes from the recent list (a missing file can then be removed from it).</summary>
		public Boolean FromRecentList { get; init; }

		/// <summary>Gets a value indicating whether a decompiled script is opened without asking where to save it (round trip).</summary>
		public Boolean TemporaryDecompile { get; init; }

		/// <summary>Gets a value indicating whether the output (Build tab) is cleared before a decompilation.</summary>
		public Boolean ClearBuildLog { get; init; } = true;
	}
}
