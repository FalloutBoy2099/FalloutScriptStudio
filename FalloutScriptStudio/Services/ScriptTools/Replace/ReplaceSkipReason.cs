namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	/// <summary>Why a file with matches is not replaced.</summary>
	public enum ReplaceSkipReason
	{
		/// <summary>The file is read-only.</summary>
		ReadOnly,

		/// <summary>The file cannot be read (locked by another program, no access).</summary>
		Unreadable,
	}
}