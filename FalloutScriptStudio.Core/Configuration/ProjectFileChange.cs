namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// A change of the project file that the <see cref="ProjectFileWatcher"/> verified: the content was read completely and is stable.
	/// </summary>
	/// <param name="Kind">What happened.</param>
	/// <param name="Read">The valid content for <see cref="ProjectFileChangeKind.Modified"/> (status <see cref="ProjectFileStatus.Loaded"/>), otherwise <see langword="null"/>.</param>
	/// <param name="Error">Why the content is not valid for <see cref="ProjectFileChangeKind.Invalid"/>, otherwise <see langword="null"/>.</param>
	public sealed record ProjectFileChange(ProjectFileChangeKind Kind, ProjectFileReadResult? Read = null, ProjectFileError? Error = null);
}
