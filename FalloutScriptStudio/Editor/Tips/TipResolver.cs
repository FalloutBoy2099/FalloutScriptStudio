namespace FalloutScriptStudio.Editor.Tips
{
	using System;
	using System.Globalization;
	using Core.Parsing;
	using Localization;

	/// <summary>
	/// Decides which tooltip belongs to a word and builds its content (legacy <c>ToolTipRequest</c> and the code tips of
	/// <c>ShowCodeTips</c>). Pure logic: no controls are touched.
	/// </summary>
	public static class TipResolver
	{
		/// <summary>Gets the hint shown after typing a block keyword.</summary>
		public static String BlockKeywordHint => Loc.Get("Str.Editor.Tip.BlockKeywordHint");

		/// <summary>Builds the tooltip of a word.</summary>
		/// <param name="word">The identifier or number under the mouse.</param>
		/// <param name="line">The 1-based line of the word.</param>
		/// <param name="filePath">Path of the edited file, or <see langword="null"/> when it is unsaved.</param>
		/// <param name="info">The parse result of the script, if any.</param>
		/// <param name="opcodes">The opcode descriptions, if any.</param>
		/// <param name="messageLookup">Returns the text of a message id of the associated message file, or <see langword="null"/>.</param>
		/// <returns>The tooltip, or <see langword="null"/> when the word is unknown.</returns>
		public static TipInfo? ResolveWord(
			String word,
			Int32 line,
			String? filePath,
			ProgramInfo? info,
			OpcodeCatalog? opcodes,
			Func<Int32, String?>? messageLookup)
		{
			if (word.Length == 0)
			{
				return null;
			}

			if (messageLookup is not null && Int32.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out Int32 id))
			{
				String? text = messageLookup(id);
				if (text is not null)
				{
					return new TipInfo(Loc.Format("Str.Editor.Tip.Message", id.ToString(CultureInfo.InvariantCulture)), "\"" + text + "\"", TipKind.Message);
				}
			}

			String? opcode = opcodes?.LookupToken(word);
			if (opcode is not null)
			{
				return TipInfo.FromDescription(opcode, TipKind.Opcode);
			}

			IScriptSymbol? symbol = info?.Lookup(word, filePath ?? String.Empty, line);
			return symbol is null ? null : TipInfo.FromDescription(symbol.FullDescription, GetKind(symbol));
		}

		/// <summary>Builds the tooltip of the path of an <c>#include</c> line.</summary>
		/// <param name="lineText">The text of the line.</param>
		/// <param name="resolver">Finds the file of the directive.</param>
		/// <param name="scriptDirectory">Directory of the edited script, or <see langword="null"/> when it is unsaved.</param>
		/// <param name="fileExists">Tells whether a file exists.</param>
		/// <returns>The tooltip, or <see langword="null"/> when the line is not an include directive.</returns>
		public static TipInfo? ResolveInclude(String lineText, IncludeResolver resolver, String? scriptDirectory, Func<String, Boolean> fileExists)
		{
			String trimmed = lineText.Trim();
			if (!trimmed.StartsWith(SslKeywords.Include, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			String? path = IncludeResolver.ExtractIncludePath(trimmed);
			if (String.IsNullOrEmpty(path))
			{
				return null;
			}

			String resolved = resolver.Resolve(path, scriptDirectory);
			return fileExists(resolved)
				? new TipInfo("#include \"" + path + "\"", resolved, TipKind.Include)
				: new TipInfo("#include \"" + path + "\"", Loc.Format("Str.Editor.Tip.FileNotFound", resolved), TipKind.Include);
		}

		/// <summary>Builds the one-line parameter hint shown after typing <c>(</c> behind a function name.</summary>
		/// <param name="word">The word before the parenthesis.</param>
		/// <param name="info">The parse result of the script, if any.</param>
		/// <param name="opcodes">The opcode descriptions, if any.</param>
		/// <returns>The hint, or <see langword="null"/> when the word is not a known function or procedure.</returns>
		public static TipInfo? ResolveCodeTip(String word, ProgramInfo? info, OpcodeCatalog? opcodes)
		{
			String? description = opcodes?.LookupToken(word);
			if (description is not null)
			{
				Int32 lineBreak = description.IndexOf('\n');
				return new TipInfo(lineBreak > 0 ? description.Substring(0, lineBreak) : description, String.Empty, TipKind.Opcode);
			}

			IScriptSymbol? symbol = info?.Lookup(word);
			return symbol is null ? null : new TipInfo(symbol.ShortDescription, String.Empty, GetKind(symbol));
		}

		/// <summary>Determines whether a word is a keyword that expands to a code block on Tab.</summary>
		/// <param name="word">The word.</param>
		/// <returns><see langword="true"/> for <c>if</c>, <c>ifel</c>, <c>elif</c>, <c>while</c>, <c>for</c>, <c>foreach</c> and <c>switch</c>.</returns>
		public static Boolean IsBlockKeyword(String word)
		{
			return word is "for" or "foreach" or "while" or "switch" or "if" or "ifel" or "elif";
		}

		private static TipKind GetKind(IScriptSymbol symbol)
		{
			return symbol.Kind switch
			{
				SymbolKind.Procedure => TipKind.Procedure,
				SymbolKind.Macro => TipKind.Macro,
				_ => TipKind.Variable,
			};
		}
	}
}