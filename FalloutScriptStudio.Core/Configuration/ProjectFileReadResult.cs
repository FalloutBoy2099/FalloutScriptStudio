namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// What <see cref="ProjectSettingsFile.Read"/> found in a project folder.
	/// </summary>
	/// <param name="Status">The outcome.</param>
	/// <param name="Settings">The settings with absolute paths for <see cref="ProjectFileStatus.Loaded"/>, otherwise <see langword="null"/>.</param>
	/// <param name="Detail">The reason given by the system for <see cref="ProjectFileStatus.Unreadable"/>, otherwise <see langword="null"/>.</param>
	public sealed record ProjectFileReadResult(ProjectFileStatus Status, ProjectSettings? Settings, String? Detail = null);
}
