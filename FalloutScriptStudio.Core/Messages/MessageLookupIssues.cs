namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>
	/// Conditions detected while locating a message file that the user interface may want to report.
	/// </summary>
	[Flags]
	public enum MessageLookupIssues
	{
		/// <summary>Nothing to report.</summary>
		None = 0,

		/// <summary><c>scripts.lst</c> does not exist in the output folder; the script name is used instead.</summary>
		ScriptsListMissing = 1,

		/// <summary>The entry in <c>scripts.lst</c> could not be read; no message file could be determined.</summary>
		ScriptsListEntryInvalid = 2,

		/// <summary>The script has no <c>NAME</c> macro with a script number; the script name is used instead.</summary>
		ScriptIdNotFound = 4,

		/// <summary>No output folder is configured; the folder of the script is used instead.</summary>
		OutputDirectoryNotSet = 8,
	}
}