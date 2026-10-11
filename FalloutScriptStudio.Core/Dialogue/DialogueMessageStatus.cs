namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>
	/// How the lookup of a dialogue message ended.
	/// </summary>
	public enum DialogueMessageStatus
	{
		/// <summary>The message was found.</summary>
		Found = 0,

		/// <summary>The message file exists but has no message with that number.</summary>
		MessageNotFound,

		/// <summary>The message file could not be found or read.</summary>
		FileNotFound,

		/// <summary>The statement has no usable message number (it is 0 or negative).</summary>
		NoMessageNumber,
	}
}
