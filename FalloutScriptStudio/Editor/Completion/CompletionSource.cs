namespace FalloutScriptStudio.Editor.Completion
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;

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
			if (prefix.Length < Math.Max(minimumLength, 1) || Char.IsDigit(prefix[0]))
			{
				return Array.Empty<AutosuggestItem>();
			}

			IReadOnlyList<AutosuggestItem> items = info is null ? opcodes.Lookup(prefix) : info.GetAutosuggest(prefix, opcodes);
			if (items.Count == 1 && String.Equals(items[0].Name, prefix, StringComparison.Ordinal))
			{
				return Array.Empty<AutosuggestItem>();
			}

			return items;
		}
	}
}
