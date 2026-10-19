namespace FalloutScriptStudio.Core.Snippets
{
	/// <summary>What is wrong with the body of a snippet.</summary>
	public enum SnippetProblemKind
	{
		/// <summary>A <c>${</c> placeholder is not closed by a <c>}</c>.</summary>
		UnterminatedPlaceholder = 0,

		/// <summary>A <c>$NAME</c> is not one of the variables (<c>$SELECTION</c>, <c>$DATE</c>...).</summary>
		UnknownVariable = 1,

		/// <summary>A <c>$</c> or <c>${</c> is followed by something that is neither a number nor a variable name.</summary>
		InvalidPlaceholder = 2,

		/// <summary>The body is empty.</summary>
		EmptyBody = 3,
	}
}