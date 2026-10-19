namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>The kinds of piece a snippet body is made of.</summary>
	internal enum SnippetPartKind
	{
		/// <summary>Plain text.</summary>
		Text,

		/// <summary>A tab stop (<c>$1</c>, <c>${1:default}</c>, <c>$0</c>).</summary>
		Stop,

		/// <summary>A variable (<c>$SELECTION</c>, <c>${DATE}</c>).</summary>
		Variable,
	}

	/// <summary>One piece of a parsed snippet body.</summary>
	/// <param name="Kind">The kind.</param>
	/// <param name="Text">The text of a text piece, or the name of a variable.</param>
	/// <param name="Index">The number of a tab stop.</param>
	/// <param name="Children">The default content of a tab stop or of a variable (which may hold other stops), or an empty list.</param>
	internal sealed record SnippetPart(SnippetPartKind Kind, String Text, Int32 Index, IReadOnlyList<SnippetPart> Children)
	{
		/// <summary>Creates a text piece.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The piece.</returns>
		public static SnippetPart CreateText(String text)
		{
			return new SnippetPart(SnippetPartKind.Text, text, 0, Array.Empty<SnippetPart>());
		}
	}
}