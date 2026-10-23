namespace FalloutScriptStudio.Core.Messages
{
	/// <summary>How a message of a reference message file compares with the message of the same number in a target message file (another language).</summary>
	public enum MessageComparisonStatus
	{
		/// <summary>Both files have the message, the target text differs from the reference and nothing else is wrong.</summary>
		Ok = 0,

		/// <summary>The reference has the message and the target does not.</summary>
		MissingInTarget,

		/// <summary>The target has a message that the reference does not have.</summary>
		ExtraInTarget,

		/// <summary>The target text is the text of the reference: the message was not translated.</summary>
		SameText,

		/// <summary>The target text is the text of the reference except for its placeholders, tags or variables.</summary>
		PlaceholdersOnly,

		/// <summary>The reference has a text and the target text is empty (or only white space).</summary>
		EmptyInTarget,

		/// <summary>The texts differ as they should, but one is a player line (leading tab) and the other is not, or the text after the closing brace (the description) differs.</summary>
		PlayerOrDescription,
	}
}