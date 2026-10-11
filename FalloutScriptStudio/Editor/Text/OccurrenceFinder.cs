namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>Finds all occurrences of a word or of a piece of text, for the "highlight selected text" command.</summary>
	public static class OccurrenceFinder
	{
		/// <summary>Determines whether a piece of text is worth highlighting (legacy rules of <c>HighlightingSelectedText</c>).</summary>
		/// <param name="text">The (trimmed) text.</param>
		/// <returns><see langword="false"/> for empty text and for one or two punctuation characters.</returns>
		public static Boolean IsHighlightable(String text)
		{
			if (text.Length == 0)
			{
				return false;
			}

			return !(text.Length <= 2 && !Char.IsLetterOrDigit(text[0]) && (text.Length == 1 || !Char.IsLetterOrDigit(text[1])));
		}

		/// <summary>Finds the occurrences of a text.</summary>
		/// <param name="document">The document text.</param>
		/// <param name="needle">The text to find (case sensitive).</param>
		/// <param name="wholeWord">Only count occurrences that are not part of a longer identifier.</param>
		/// <returns>The ranges in document order.</returns>
		public static IReadOnlyList<TextSpan> Find(String document, String needle, Boolean wholeWord)
		{
			List<TextSpan> result = new List<TextSpan>();
			if (needle.Length == 0)
			{
				return result;
			}

			Int32 index = 0;
			while (index < document.Length)
			{
				index = document.IndexOf(needle, index, StringComparison.Ordinal);
				if (index < 0)
				{
					break;
				}

				Boolean before = index > 0 && WordFinder.IsWordChar(document[index - 1]);
				Boolean after = index + needle.Length < document.Length && WordFinder.IsWordChar(document[index + needle.Length]);
				if (!wholeWord || (!before && !after))
				{
					result.Add(new TextSpan(index, needle.Length));
				}

				index += needle.Length;
			}

			return result;
		}
	}
}
