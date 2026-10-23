namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Analysis;
	using Parsing;

	/// <summary>
	/// Finds the reads and writes of global variables in one script or header: the calls of the functions of
	/// <see cref="GlobalFunctionTable"/> whose first argument is a string, a number or a name. Comments are never looked into (the
	/// tokenizer drops them), the body of a <c>#define</c> is, so that a macro that sets a variable counts, and the name of a variable of the
	/// script or a parameter of the macro is never taken for a macro (it stands for a value only the running script knows).
	/// </summary>
	internal static class GlobalUseScanner
	{
		private const Int32 MaxCallTokens = 400;

		/// <summary>Determines whether a text can contain a use (a cheap test that avoids tokenizing the files that cannot).</summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="false"/> when no function of the table appears in the text.</returns>
		internal static Boolean MayContainUses(String text)
		{
			return text.Contains("_var", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("var_bit", StringComparison.OrdinalIgnoreCase)
				|| text.Contains("sfall_global", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Scans a text from scratch (tokenizer, script model and macro constants).</summary>
		/// <param name="text">The text.</param>
		/// <param name="pool">Shares equal texts; none when <see langword="null"/>.</param>
		/// <returns>What was found.</returns>
		internal static FileGlobalScan ScanText(String text, GlobalStringPool? pool = null)
		{
			ArgumentNullException.ThrowIfNull(text);

			SslScriptModel model = SslScriptModel.Analyze(text);
			List<ScriptMacro> macros = [];
			if (text.Contains("#define", StringComparison.Ordinal))
			{
				macros.AddRange(new MacroCollector(new IncludeResolver(null, null)).CollectFromText(text, null, false).Values);
			}

			return Scan(model, CollectConstants(macros, pool), pool);
		}

		/// <summary>Finds the macros that stand for a number, a string or another macro.</summary>
		/// <param name="macros">The macros of a file.</param>
		/// <param name="pool">Shares equal texts; none when <see langword="null"/>.</param>
		/// <returns>The constants.</returns>
		internal static MacroConstant[] CollectConstants(IEnumerable<ScriptMacro> macros, GlobalStringPool? pool = null)
		{
			List<MacroConstant> constants = [];
			foreach (ScriptMacro macro in macros)
			{
				if (String.Equals(macro.DefinitionName, macro.Token, StringComparison.Ordinal) && TryParseConstant(macro.Code, out MacroConstantKind kind, out String value))
				{
					constants.Add(new MacroConstant(Share(pool, macro.Token), kind, Share(pool, value)));
				}
			}

			return constants.Count == 0 ? Array.Empty<MacroConstant>() : constants.ToArray();
		}

		/// <summary>Reads the body of a macro.</summary>
		/// <param name="code">The body.</param>
		/// <param name="kind">Receives what the body is.</param>
		/// <param name="value">Receives the value.</param>
		/// <returns><see langword="true"/> when the body is a number, a string or a name.</returns>
		internal static Boolean TryParseConstant(String code, out MacroConstantKind kind, out String value)
		{
			kind = MacroConstantKind.Number;
			value = String.Empty;
			if (code.Contains('\n', StringComparison.Ordinal) || code.Contains('\\', StringComparison.Ordinal))
			{
				return false;
			}

			ReadOnlySpan<Char> span = code.AsSpan();
			Int32 comment = span.IndexOf("//", StringComparison.Ordinal);
			if (comment >= 0)
			{
				span = span.Slice(0, comment);
			}

			comment = span.IndexOf("/*", StringComparison.Ordinal);
			if (comment >= 0)
			{
				span = span.Slice(0, comment);
			}

			span = span.Trim();
			while (span.Length >= 2 && span[0] == '(' && span[^1] == ')')
			{
				span = span.Slice(1, span.Length - 2).Trim();
			}

			if (span.Length == 0)
			{
				return false;
			}

			if (span[0] == '"')
			{
				if (span.Length >= 2 && span[^1] == '"' && span.Slice(1, span.Length - 2).IndexOf('"') < 0)
				{
					kind = MacroConstantKind.String;
					value = span.Slice(1, span.Length - 2).ToString();
					return true;
				}

				return false;
			}

			ReadOnlySpan<Char> digits = span;
			Boolean negative = false;
			if (span[0] == '-')
			{
				negative = true;
				digits = span.Slice(1).TrimStart();
			}

			if (GlobalNumbers.LooksLikeNumber(digits))
			{
				kind = MacroConstantKind.Number;
				value = negative ? "-" + digits.ToString() : digits.ToString();
				return true;
			}

			if (!negative && IsIdentifier(span))
			{
				kind = MacroConstantKind.Alias;
				value = span.ToString();
				return true;
			}

			return false;
		}

		/// <summary>Scans the tokens of a script.</summary>
		/// <param name="model">The model of the script (its tokens, procedures and variables).</param>
		/// <param name="constants">The constants the file defines (kept in the result).</param>
		/// <param name="pool">Shares equal texts; none when <see langword="null"/>.</param>
		/// <returns>What was found.</returns>
		internal static FileGlobalScan Scan(SslScriptModel model, MacroConstant[] constants, GlobalStringPool? pool)
		{
			SslTokenStream tokens = model.Tokens;
			List<RawGlobalUse> uses = [];
			List<String> procedures = [];
			Dictionary<String, Int32> procedureIndex = new(StringComparer.Ordinal);
			HashSet<String> declared = new(StringComparer.OrdinalIgnoreCase);
			foreach (VariableDeclaration declaration in model.Declarations)
			{
				declared.Add(declaration.Name);
			}

			HashSet<String>? parameters = null;
			String? defineName = null;
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				SslToken token = tokens[i];
				if (token.Kind == SslTokenKind.Directive)
				{
					parameters = null;
					defineName = null;
					if (tokens.GetDirectiveName(i) == "define" && i + 1 < tokens.Count && tokens[i + 1].Kind == SslTokenKind.Identifier && tokens[i + 1].InDirective)
					{
						defineName = tokens.GetText(i + 1);
						parameters = ReadMacroParameters(tokens, i + 1);
					}

					continue;
				}

				if (!token.InDirective)
				{
					parameters = null;
					defineName = null;
				}

				if (token.Kind != SslTokenKind.Identifier || !GlobalFunctionTable.TryGet(tokens.Span(i), out GlobalFunction? function) || function is null || !tokens.IsPunctuation(i + 1, '('))
				{
					continue;
				}

				if (i > 0 && tokens[i - 1].Kind == SslTokenKind.Directive)
				{
					continue;
				}

				if (!TryReadArguments(tokens, i + 1, out List<(Int32 Start, Int32 End)>? arguments) || arguments is null || arguments.Count == 0)
				{
					continue;
				}

				if (!TryReadKey(tokens, arguments[0], function.Kind, declared, parameters, out GlobalKeyKind keyKind, out String key))
				{
					continue;
				}

				String? value = null;
				Boolean valueIsSymbol = false;
				if (function.ValueArgument >= 0 && function.ValueArgument < arguments.Count)
				{
					value = ReadValue(tokens, arguments[function.ValueArgument], declared, parameters, out valueIsSymbol);
				}

				Int32 procedure = -1;
				String? procedureName = model.FindProcedureAt(i)?.Name;
				if (procedureName is null && defineName is not null)
				{
					procedureName = "#define " + defineName;
				}

				if (procedureName is not null && !procedureIndex.TryGetValue(procedureName, out procedure))
				{
					procedure = procedures.Count;
					procedures.Add(Share(pool, procedureName));
					procedureIndex.Add(procedureName, procedure);
				}

				uses.Add(new RawGlobalUse(function, keyKind, Share(pool, key), token.Line, procedureName is null ? -1 : procedure, value is null ? null : Share(pool, value), valueIsSymbol));
			}

			if (uses.Count == 0 && constants.Length == 0)
			{
				return FileGlobalScan.Empty;
			}

			return new FileGlobalScan(uses.ToArray(), procedures.ToArray(), constants);
		}

		private static String Share(GlobalStringPool? pool, String text)
		{
			return pool is null ? text : pool.Get(text);
		}

		private static HashSet<String>? ReadMacroParameters(SslTokenStream tokens, Int32 nameIndex)
		{
			Int32 open = nameIndex + 1;
			if (!tokens.IsPunctuation(open, '(') || !tokens[open].InDirective || tokens[open].Offset != tokens[nameIndex].End)
			{
				return null;
			}

			HashSet<String> parameters = new(StringComparer.OrdinalIgnoreCase);
			for (Int32 j = open + 1; j < tokens.Count && tokens[j].InDirective && tokens[j].Kind != SslTokenKind.Directive; j++)
			{
				if (tokens.IsPunctuation(j, ')'))
				{
					break;
				}

				if (tokens[j].Kind == SslTokenKind.Identifier)
				{
					parameters.Add(tokens.GetText(j));
				}
			}

			return parameters;
		}

		private static Boolean TryReadArguments(SslTokenStream tokens, Int32 open, out List<(Int32 Start, Int32 End)>? arguments)
		{
			arguments = null;
			Boolean inDirective = tokens[open].InDirective;
			List<(Int32 Start, Int32 End)> found = [];
			Int32 depth = 1;
			Int32 start = open + 1;
			for (Int32 j = open + 1; j < tokens.Count && j - open <= MaxCallTokens; j++)
			{
				SslToken token = tokens[j];
				if (token.Kind == SslTokenKind.Directive || token.InDirective != inDirective)
				{
					return false;
				}

				if (token.Kind != SslTokenKind.Punctuation)
				{
					continue;
				}

				if (tokens.IsPunctuation(j, '('))
				{
					depth++;
				}
				else if (tokens.IsPunctuation(j, ')'))
				{
					depth--;
					if (depth == 0)
					{
						if (j > start || found.Count > 0)
						{
							found.Add((start, j));
						}

						arguments = found;
						return true;
					}
				}
				else if (depth == 1 && tokens.IsPunctuation(j, ','))
				{
					found.Add((start, j));
					start = j + 1;
				}
				else if (tokens.IsPunctuation(j, ';'))
				{
					return false;
				}
			}

			return false;
		}

		/// <summary>Reduces an argument to the one token (or the minus sign and the number) it is made of.</summary>
		private static Boolean TryReduce(SslTokenStream tokens, (Int32 Start, Int32 End) range, out Int32 index, out Boolean negative)
		{
			Int32 start = range.Start;
			Int32 end = range.End;
			negative = false;
			index = -1;
			while (end - start >= 2 && tokens.IsPunctuation(start, '(') && tokens.IsPunctuation(end - 1, ')') && IsEnclosing(tokens, start, end - 1))
			{
				start++;
				end--;
			}

			if (end - start == 1)
			{
				index = start;
				return true;
			}

			if (end - start == 2 && tokens.IsPunctuation(start, '-') && tokens[start + 1].Kind == SslTokenKind.Number)
			{
				negative = true;
				index = start + 1;
				return true;
			}

			return false;
		}

		private static Boolean IsEnclosing(SslTokenStream tokens, Int32 open, Int32 close)
		{
			Int32 depth = 0;
			for (Int32 j = open; j <= close; j++)
			{
				if (tokens.IsPunctuation(j, '('))
				{
					depth++;
				}
				else if (tokens.IsPunctuation(j, ')'))
				{
					depth--;
					if (depth == 0 && j < close)
					{
						return false;
					}
				}
			}

			return depth == 0;
		}

		private static Boolean TryReadKey(SslTokenStream tokens, (Int32 Start, Int32 End) range, GlobalVariableKind kind, HashSet<String> declared, HashSet<String>? parameters, out GlobalKeyKind keyKind, out String key)
		{
			keyKind = GlobalKeyKind.Id;
			key = String.Empty;
			if (!TryReduce(tokens, range, out Int32 index, out Boolean negative))
			{
				return false;
			}

			switch (tokens[index].Kind)
			{
				case SslTokenKind.String:
					String quoted = tokens.GetText(index);
					if (kind != GlobalVariableKind.SfallGlobal || negative || quoted.Length < 2 || quoted[^1] != '"')
					{
						return false;
					}

					keyKind = GlobalKeyKind.Name;
					key = quoted.Substring(1, quoted.Length - 2);
					return true;
				case SslTokenKind.Number:
					String number = (negative ? "-" : String.Empty) + tokens.GetText(index);
					if (!GlobalNumbers.TryParseInteger(number, out Int64 value))
					{
						return false;
					}

					keyKind = GlobalKeyKind.Id;
					key = value.ToString(CultureInfo.InvariantCulture);
					return true;
				case SslTokenKind.Identifier:
					String name = tokens.GetText(index);
					if (negative || declared.Contains(name) || (parameters is not null && parameters.Contains(name)))
					{
						return false;
					}

					keyKind = GlobalKeyKind.Symbol;
					key = name;
					return true;
				default:
					return false;
			}
		}

		private static String? ReadValue(SslTokenStream tokens, (Int32 Start, Int32 End) range, HashSet<String> declared, HashSet<String>? parameters, out Boolean isSymbol)
		{
			isSymbol = false;
			if (!TryReduce(tokens, range, out Int32 index, out Boolean negative))
			{
				return null;
			}

			if (tokens[index].Kind == SslTokenKind.Number)
			{
				return (negative ? "-" : String.Empty) + tokens.GetText(index);
			}

			if (tokens[index].Kind == SslTokenKind.Identifier && !negative)
			{
				String name = tokens.GetText(index);
				if (!declared.Contains(name) && (parameters is null || !parameters.Contains(name)))
				{
					isSymbol = true;
					return name;
				}
			}

			return null;
		}

		private static Boolean IsIdentifier(ReadOnlySpan<Char> text)
		{
			if (text.Length == 0 || !(text[0] == '_' || Char.IsLetter(text[0])))
			{
				return false;
			}

			foreach (Char c in text)
			{
				if (!(c == '_' || Char.IsLetterOrDigit(c)))
				{
					return false;
				}
			}

			return true;
		}
	}
}