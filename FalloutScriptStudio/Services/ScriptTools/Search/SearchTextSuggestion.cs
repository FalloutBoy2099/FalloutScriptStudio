namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;

	/// <summary>
	/// Decides which text the "Search" field of the search window is pre-filled with. The field is a single line and the matching is done line by
	/// line (a match never spans a line break, see <see cref="SearchMatcher"/>), so a multi-line selection contributes its first line only and a
	/// multi-line or very long clipboard text is ignored.
	/// </summary>
	public static class SearchTextSuggestion
	{
		/// <summary>The longest text that is offered to the search field.</summary>
		public const Int32 MaxLength = 200;

		/// <summary>Gets the first non-blank line of a text, trimmed (the selection of the editor).</summary>
		/// <param name="text">The text, possibly with several lines.</param>
		/// <param name="maxLength">The longest line that is accepted.</param>
		/// <returns>The line; empty when there is none or when it is longer than <paramref name="maxLength"/>.</returns>
		public static String FirstLine(String? text, Int32 maxLength = MaxLength)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return String.Empty;
			}

			foreach (String line in text.Split(['\r', '\n']))
			{
				String trimmed = line.Trim();
				if (trimmed.Length > 0)
				{
					return trimmed.Length <= maxLength ? trimmed : String.Empty;
				}
			}

			return String.Empty;
		}

		/// <summary>Gets a clipboard text when it is usable as a search text: a single line that is not too long.</summary>
		/// <param name="text">The clipboard text.</param>
		/// <returns>The trimmed text; empty when it is blank, longer than <see cref="MaxLength"/> or has several lines.</returns>
		public static String SingleLine(String? text)
		{
			String trimmed = text?.Trim() ?? String.Empty;
			return trimmed.Length <= MaxLength && trimmed.AsSpan().IndexOfAny('\r', '\n') < 0 ? trimmed : String.Empty;
		}

		/// <summary>Chooses the text of the search field when the window opens.</summary>
		/// <param name="initialText">The text the caller gave.</param>
		/// <param name="selection">The selection of the active editor.</param>
		/// <param name="currentText">The text of an already open search window.</param>
		/// <param name="lastText">The text of the previous search window.</param>
		/// <param name="readClipboard">Reads the clipboard text; only called when nothing else is usable.</param>
		/// <returns>The first usable candidate in that order, and finally the clipboard; empty when nothing is usable.</returns>
		public static String Choose(String? initialText, String? selection, String? currentText, String? lastText, Func<String> readClipboard)
		{
			ArgumentNullException.ThrowIfNull(readClipboard);

			foreach (String? candidate in new[] { initialText, selection, currentText, lastText })
			{
				String line = FirstLine(candidate);
				if (line.Length > 0)
				{
					return line;
				}
			}

			return SingleLine(readClipboard());
		}
	}
}