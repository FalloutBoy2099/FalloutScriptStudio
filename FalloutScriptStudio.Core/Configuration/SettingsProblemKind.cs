namespace FalloutScriptStudio.Core.Configuration
{
	/// <summary>
	/// What went wrong with a file of the settings folder.
	/// </summary>
	public enum SettingsProblemKind
	{
		/// <summary>The file exists but its content is damaged; default values are used and a <c>.corrupt</c> copy is kept.</summary>
		Damaged,

		/// <summary>The file exists but cannot be read; it is left untouched.</summary>
		Unreadable,

		/// <summary>The file could not be written.</summary>
		SaveFailed,
	}
}