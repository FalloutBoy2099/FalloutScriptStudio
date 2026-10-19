namespace FalloutScriptStudio.Core.Snippets
{
	/// <summary>What is wrong with the trigger of a snippet.</summary>
	public enum SnippetTriggerIssue
	{
		/// <summary>Nothing.</summary>
		None = 0,

		/// <summary>The trigger is empty.</summary>
		Empty = 1,

		/// <summary>The trigger is longer than <see cref="SnippetValidator.MaxTriggerLength"/>.</summary>
		TooLong = 2,

		/// <summary>The trigger holds a character that is not a letter, a digit or an underscore, or starts with a digit.</summary>
		InvalidCharacters = 3,

		/// <summary>The trigger is one of the words of the block completion (<c>if</c>, <c>while</c>, <c>proc</c>...), which keep their own behaviour.</summary>
		Reserved = 4,

		/// <summary>Another snippet of the same list has the trigger.</summary>
		Duplicate = 5,
	}
}