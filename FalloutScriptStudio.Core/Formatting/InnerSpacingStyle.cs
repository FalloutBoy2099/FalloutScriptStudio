namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>What the formatter does with the white space just inside parentheses.</summary>
	public enum InnerSpacingStyle
	{
		/// <summary>Leave the text as the user typed it.</summary>
		Keep = 0,

		/// <summary>No space after <c>(</c> and before <c>)</c>.</summary>
		None = 1,

		/// <summary>One space after <c>(</c> and before <c>)</c> (an empty pair stays empty).</summary>
		One = 2,
	}
}