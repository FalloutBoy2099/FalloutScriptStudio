namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Expands the keywords <c>if ifel elif else then for foreach while switch var proc</c> into code blocks when Tab is pressed
	/// after them (legacy <c>Utilities.AutoCompleteKeyWord</c>).
	/// </summary>
	/// <remarks>
	/// Differences from the legacy code: <c>ifel</c> produces a valid <c>if … then begin … end else begin … end</c> statement
	/// (the legacy template lacked the <c>end</c>/<c>begin</c> around <c>else</c> and the space after <c>if</c>), and the caret
	/// target is returned as a selection of the first placeholder instead of being computed from column arithmetic.
	/// </remarks>
	public sealed class KeywordExpander
	{
		private static readonly String[] _keywords = { "for", "foreach", "while", "switch", "if", "ifel", "elif", "else", "then", "var", "proc" };

		/// <summary>Gets the keywords that are expanded.</summary>
		public IReadOnlyList<String> Keywords => _keywords;

		/// <summary>
		/// Determines whether a word is one of the expandable keywords.
		/// </summary>
		/// <param name="word">The word (case-sensitive).</param>
		/// <returns><c>true</c> when the word is expandable.</returns>
		public Boolean IsKeyword(String word)
		{
			return Array.IndexOf(_keywords, word) >= 0;
		}

		/// <summary>
		/// Tries to expand the keyword before the caret.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="caretOffset">The caret offset (just after the keyword).</param>
		/// <param name="options">Indentation settings.</param>
		/// <returns>The expansion, or <c>null</c> when the caret is in a comment or a string or the word is not a keyword.</returns>
		public KeywordExpansion? TryExpand(String text, Int32 caretOffset, KeywordExpansionOptions options)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(options);

			if (caretOffset < 0 || caretOffset > text.Length || SslLexicalScanner.IsInCommentOrString(text, caretOffset))
			{
				return null;
			}

			Int32 wordStart = WordScanner.FindWordStart(text, caretOffset);
			String keyword = text.Substring(wordStart, caretOffset - wordStart);
			if (keyword.Length < 2 || !this.IsKeyword(keyword))
			{
				return null;
			}

			Boolean cutKeyword = WordScanner.FindWordEnd(text, caretOffset) > caretOffset;

			var map = new TextLineMap(text);
			Int32 line = map.GetLineFromOffset(caretOffset);
			Int32 lineStart = map.GetLineStart(line);
			String lineText = map.GetLineText(line);
			Int32 spaces = 0;
			Int32 tabs = 0;
			foreach (Char c in lineText)
			{
				if (c == ' ')
				{
					spaces++;
				}
				else if (c == '\t')
				{
					tabs++;
				}
				else
				{
					break;
				}
			}

			Int32 size = Math.Max(options.TabSize, 1);
			Int32 indentUnits = options.ConvertTabsToSpaces ? spaces + (tabs * size) : (spaces / size) + tabs;
			Int32 column = caretOffset - lineStart;
			String indentChar = options.ConvertTabsToSpaces ? " " : "\t";
			String beginIndent = column > keyword.Length ? Repeat(indentChar, indentUnits) : String.Empty;
			String indentation = beginIndent + (options.ConvertTabsToSpaces ? new String(' ', size) : "\t");
			String nl = options.NewLine;
			String end = beginIndent + "end" + (cutKeyword ? " " : String.Empty);

			String code;
			Int32 removeCount = 0;
			String? keepPrefix = null;
			Int32 caretInIndentation = -1;
			switch (keyword)
			{
				case "for":
					code = " ({iterator} := 0; {condition}; {iterator}++) begin" + nl + indentation + nl + end;
					break;
				case "foreach":
					code = " ({iterator}: {item} in {array}) begin" + nl + indentation + nl + end;
					break;
				case "while":
					code = " ({condition}) do begin" + nl + indentation + nl + end;
					break;
				case "switch":
					code = " ({condition}) begin" + nl + indentation + "case {constant} : {code}" + nl + indentation + "default         : {code}" + nl + end;
					break;
				case "if":
					code = " ({condition}) then begin" + nl + indentation + nl + end;
					break;
				case "ifel":
					code = " ({condition}) then begin" + nl + indentation + nl + beginIndent + "end else begin" + nl + indentation + nl + end;
					removeCount = 2;
					keepPrefix = String.Empty;
					break;
				case "elif":
					code = " if ({condition}) then begin" + nl + indentation + nl + end;
					removeCount = 2;
					keepPrefix = "se";
					break;
				case "else":
					code = " begin" + nl + indentation + nl + end;
					caretInIndentation = 1;
					break;
				case "then":
					code = " begin";
					break;
				case "var":
					code = "iable ";
					break;
				default:
					code = "edure ";
					break;
			}

			String replacement = (keepPrefix ?? String.Empty) + code;
			Int32 editOffset = caretOffset - removeCount;
			var edit = new TextEdit(editOffset, removeCount, replacement);

			Int32 codeStart = editOffset + (keepPrefix ?? String.Empty).Length;
			Int32 open = code.IndexOf('{', StringComparison.Ordinal);
			if (open >= 0)
			{
				Int32 close = code.IndexOf('}', open);
				return new KeywordExpansion(edit, new TextSpan(codeStart + open, close - open + 1));
			}

			if (caretInIndentation >= 0)
			{
				Int32 position = codeStart + code.IndexOf(nl, StringComparison.Ordinal) + nl.Length + indentation.Length;
				return new KeywordExpansion(edit, new TextSpan(position, 0));
			}

			return new KeywordExpansion(edit, new TextSpan(codeStart + code.Length, 0));
		}

		private static String Repeat(String value, Int32 count)
		{
			return count <= 0 ? String.Empty : String.Concat(System.Linq.Enumerable.Repeat(value, count));
		}
	}
}
