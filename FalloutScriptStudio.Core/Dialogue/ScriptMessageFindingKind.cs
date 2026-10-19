namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>What a <see cref="ScriptMessageFinding"/> reports.</summary>
	public enum ScriptMessageFindingKind
	{
		/// <summary>The script uses a message number that the message file does not contain (error; the place is in the script).</summary>
		MissingMessage = 0,

		/// <summary>The message file contains a number that the script never uses (information; the place is in the message file).</summary>
		UnusedMessage,

		/// <summary>The message file contains the same number twice (error; the place is the second line).</summary>
		DuplicateMessage,

		/// <summary>The script uses a message whose text in the message file is empty (warning; the place is in the message file).</summary>
		EmptyMessage,

		/// <summary>The script plays female variants of its dialogue (the number plus the offset) and the message file lacks the variant of a used message (information).</summary>
		MissingFemaleVariant,

		/// <summary>The script uses messages but there is no message file (warning; one finding, no place).</summary>
		MessageFileMissing,

		/// <summary>Some message numbers cannot be worked out, so the unused messages were not checked (information; one finding).</summary>
		UnresolvedUsages,

		/// <summary>The script uses no message at all although the message file has some, so the unused messages were not listed (information; one finding).</summary>
		NoUsageFound,
	}
}