namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// A problem with a file of the settings folder, reported by <see cref="ISettingsService"/> so that the application can tell the user
	/// (the settings are never silently replaced by defaults).
	/// </summary>
	/// <param name="Kind">What went wrong.</param>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Detail">The reason given by the system for <see cref="SettingsProblemKind.SaveFailed"/>, otherwise <see langword="null"/>.</param>
	public sealed record SettingsProblem(SettingsProblemKind Kind, String Path, String? Detail = null);
}
