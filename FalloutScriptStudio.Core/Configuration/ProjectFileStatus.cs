namespace FalloutScriptStudio.Core.Configuration
{
	/// <summary>
	/// The outcome of reading the project file of a folder.
	/// </summary>
	public enum ProjectFileStatus
	{
		/// <summary>The folder has no project file: the project inherits everything.</summary>
		Missing,

		/// <summary>The file was read.</summary>
		Loaded,

		/// <summary>The file exists but its content is damaged; the project inherits everything and a <c>.corrupt</c> copy is kept.</summary>
		Damaged,

		/// <summary>The file exists but cannot be read; the project inherits everything and the file is never overwritten.</summary>
		Unreadable,
	}
}
