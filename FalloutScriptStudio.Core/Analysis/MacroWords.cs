namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Collects the words that the bodies of macros mention (a macro can use a variable of the procedure it is expanded in).</summary>
	public static class MacroWords
	{
		/// <summary>Collects the identifiers of the bodies of some macros.</summary>
		/// <param name="macros">The macros.</param>
		/// <param name="words">Receives the words; it should compare without regard to the case.</param>
		public static void Collect(IEnumerable<ScriptMacro> macros, ISet<String> words)
		{
			ArgumentNullException.ThrowIfNull(macros);
			ArgumentNullException.ThrowIfNull(words);

			foreach (ScriptMacro macro in macros)
			{
				String code = macro.Code;
				Boolean inString = false;
				Int32 i = 0;
				while (i < code.Length)
				{
					Char c = code[i];
					if (c == '"')
					{
						inString = !inString;
						i++;
					}
					else if (!inString && (c == '_' || Char.IsLetter(c)))
					{
						Int32 start = i;
						while (i < code.Length && (code[i] == '_' || Char.IsLetterOrDigit(code[i])))
						{
							i++;
						}

						words.Add(code.Substring(start, i - start));
					}
					else if (!inString && Char.IsDigit(c))
					{
						while (i < code.Length && (code[i] == '_' || Char.IsLetterOrDigit(code[i])))
						{
							i++;
						}
					}
					else
					{
						i++;
					}
				}
			}
		}
	}
}
