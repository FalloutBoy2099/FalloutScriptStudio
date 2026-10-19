namespace FalloutScriptStudio.Editor.Completion
{
	using System;
	using System.Collections.Generic;
	using ICSharpCode.AvalonEdit.Document;
	using Core.Parsing;
	using Core.Snippets;
	using Editor.Text;

	/// <summary>
	/// Decides when the completion list opens and what it contains (legacy <c>AutoComplete.GenerateList</c>).
	/// Pure logic without controls.
	/// </summary>
	public static class CompletionSource
	{
		/// <summary>The minimal length of the typed prefix that opens the list.</summary>
		public const Int32 MinPrefixLength = 2;

		/// <summary>Gets the identifier part before the caret.</summary>
		/// <param name="text">The text.</param>
		/// <param name="caret">The caret offset.</param>
		/// <param name="start">Receives the offset of the first character of the prefix.</param>
		/// <returns>The characters from the start of the identifier to the caret; empty if the caret is not behind an identifier.</returns>
		public static String GetPrefix(ITextSource text, Int32 caret, out Int32 start)
		{
			start = caret;
			while (start > 0 && WordFinder.IsWordChar(text.GetCharAt(start - 1)))
			{
				start--;
			}

			return text.GetText(start, caret - start);
		}

		/// <summary>Builds the list for a typed prefix.</summary>
		/// <param name="prefix">The typed beginning of the word.</param>
		/// <param name="info">The parse result, or <see langword="null"/> when none is available (then only opcodes are listed).</param>
		/// <param name="opcodes">The opcode catalog.</param>
		/// <param name="minimumLength">The shortest prefix that produces a list.</param>
		/// <returns>The matching entries; empty when there is nothing to complete.</returns>
		public static IReadOnlyList<AutosuggestItem> GetItems(String prefix, ProgramInfo? info, OpcodeCatalog opcodes, Int32 minimumLength = MinPrefixLength)
		{
			return GetItems(prefix, info, opcodes, minimumLength, null, null);
		}

		/// <summary>Builds the list for a typed prefix, with the snippets whose trigger starts with it.</summary>
		/// <param name="prefix">The typed beginning of the word.</param>
		/// <param name="info">The parse result, or <see langword="null"/> when none is available (then only opcodes are listed).</param>
		/// <param name="opcodes">The opcode catalog.</param>
		/// <param name="minimumLength">The shortest prefix that produces a list.</param>
		/// <param name="snippets">The snippets in effect, or <see langword="null"/> for none.</param>
		/// <param name="describe">Gives the text that describes a snippet in the current language; the description of the snippet itself when <see langword="null"/>.</param>
		/// <returns>The matching entries, the snippets after the other ones; empty when there is nothing to complete.</returns>
		public static IReadOnlyList<AutosuggestItem> GetItems(String prefix, ProgramInfo? info, OpcodeCatalog opcodes, Int32 minimumLength, IReadOnlyList<Snippet>? snippets, Func<Snippet, String>? describe)
		{
			if (prefix.Length < Math.Max(minimumLength, 1) || Char.IsDigit(prefix[0]))
			{
				return Array.Empty<AutosuggestItem>();
			}

			IReadOnlyList<AutosuggestItem> items = info is null ? opcodes.Lookup(prefix) : info.GetAutosuggest(prefix, opcodes);
			if (items.Count == 1 && String.Equals(items[0].Name, prefix, StringComparison.Ordinal))
			{
				items = Array.Empty<AutosuggestItem>();
			}

			if (snippets is null || snippets.Count == 0)
			{
				return items;
			}

			List<AutosuggestItem> result = new(items);
			foreach (Snippet snippet in snippets)
			{
				if (snippet.IsEnabled && snippet.Trigger.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					result.Add(new AutosuggestItem(snippet.Trigger, describe is null ? snippet.Description : describe(snippet), AutosuggestKind.Snippet));
				}
			}

			return result;
		}
	}
}