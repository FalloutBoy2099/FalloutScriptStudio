namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// "Extract Procedure": turns a block of complete statements of a procedure into a new procedure that is placed in front of the
	/// procedure it was taken from, and replaces the block by a call. The variables of the enclosing procedure that the block uses become
	/// parameters (a procedure receives its arguments by value), the local variables that only the block uses move into the new procedure,
	/// and a single variable that the block changes and the rest of the procedure reads is returned.
	/// </summary>
	public static class ProcedureExtractor
	{
		/// <summary>Examines a selection.</summary>
		/// <param name="text">The text of the script.</param>
		/// <param name="selectionStart">The offset of the start of the selection.</param>
		/// <param name="selectionLength">The length of the selection.</param>
		/// <returns>What the new procedure needs, or why the selection cannot be extracted.</returns>
		public static ExtractAnalysis Analyze(String text, Int32 selectionStart, Int32 selectionLength)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (selectionLength <= 0 || selectionStart < 0 || selectionStart + selectionLength > text.Length)
			{
				return new ExtractAnalysis(ExtractFailure.EmptySelection, null);
			}

			SslScriptModel model = SslScriptModel.Analyze(text);
			SslTokenStream tokens = model.Tokens;
			Int32 selectionEnd = selectionStart + selectionLength;
			Int32 first = tokens.FindTokenAt(selectionStart);
			Int32 endToken = tokens.FindTokenAt(selectionEnd);
			if (first >= tokens.Count || first >= endToken)
			{
				return new ExtractAnalysis(ExtractFailure.EmptySelection, null);
			}

			if (tokens[first].Offset < selectionStart || (endToken < tokens.Count && tokens[endToken].Offset < selectionEnd))
			{
				return new ExtractAnalysis(ExtractFailure.PartialStatement, null);
			}

			Int32 last = endToken - 1;
			for (Int32 i = first; i <= last; i++)
			{
				if (tokens[i].InDirective)
				{
					return new ExtractAnalysis(ExtractFailure.ContainsPreprocessorLine, null);
				}
			}

			SslProcedureModel? procedure = model.FindProcedureAt(first);
			if (procedure is null || !ReferenceEquals(procedure, model.FindProcedureAt(last)) || first <= procedure.BeginToken
				|| (procedure.IsClosed ? last >= procedure.EndToken : last > procedure.EndToken))
			{
				return new ExtractAnalysis(ExtractFailure.NotInsideProcedure, null);
			}

			if (!IsBalanced(tokens, first, last))
			{
				return new ExtractAnalysis(ExtractFailure.UnbalancedBlock, null);
			}

			if (!IsCompleteStatements(tokens, first, last))
			{
				return new ExtractAnalysis(ExtractFailure.PartialStatement, null);
			}

			ExtractFailure flow = CheckFlow(tokens, first, last);
			if (flow != ExtractFailure.None)
			{
				return new ExtractAnalysis(flow, null);
			}

			ExtractAnalysis analysis = new ExtractAnalysis(ExtractFailure.None, null)
			{
				Model = model,
				Procedure = procedure,
				FirstToken = first,
				LastToken = last,
				ProcedureName = procedure.Name,
			};
			return AnalyzeVariables(analysis, model, procedure, first, last);
		}

		/// <summary>Creates the edits that extract the analyzed block.</summary>
		/// <param name="text">The text the analysis was made for.</param>
		/// <param name="analysis">The analysis; it must be valid.</param>
		/// <param name="declaration">The name of the new procedure with its parameters, for example <c>Helper(variable a, variable b)</c>; the parameters are the ones of the analysis, in any order.</param>
		/// <param name="indentUnit">One level of indentation (a tab or spaces).</param>
		/// <returns>The edits.</returns>
		/// <exception cref="ArgumentException">The analysis is not valid, or the declaration is not valid or does not have the parameters the block needs.</exception>
		public static ExtractResult CreateEdits(String text, ExtractAnalysis analysis, String declaration, String indentUnit)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(analysis);
			ArgumentNullException.ThrowIfNull(declaration);
			ArgumentNullException.ThrowIfNull(indentUnit);

			if (!analysis.IsValid || analysis.Model is null || analysis.Procedure is null)
			{
				throw new ArgumentException("The block cannot be extracted.", nameof(analysis));
			}

			ParsedProcedureName parsed = ProcedureNameParser.Parse(declaration, true);
			if (!parsed.IsValid)
			{
				throw new ArgumentException(parsed.Error, nameof(declaration));
			}

			List<String> arguments = GetArgumentNames(parsed.Declaration);
			if (!analysis.MatchesParameters(arguments))
			{
				throw new ArgumentException("The parameters must be: " + String.Join(", ", analysis.Parameters), nameof(declaration));
			}

			SslScriptModel model = analysis.Model;
			SslTokenStream tokens = model.Tokens;
			TextLineMap map = new TextLineMap(text);
			String newLine = TextLineMap.DetectNewLine(text);
			Int32 blockStart = tokens[analysis.FirstToken].Offset;
			Int32 blockEnd = tokens[analysis.LastToken].End;

			StringBuilder procedureText = new StringBuilder();
			String header = arguments.Count == 0 && declaration.Replace(" ", String.Empty, StringComparison.Ordinal).EndsWith("()", StringComparison.Ordinal) ? parsed.Name + "()" : parsed.Declaration;
			procedureText.Append("procedure ").Append(header).Append(" begin").Append(newLine);
			List<String> locals = new List<String>(analysis.MovedLocals);
			if (analysis.ResultVariable is not null && analysis.ResultIsLocal)
			{
				locals.Add(analysis.ResultVariable);
			}

			if (locals.Count > 0)
			{
				procedureText.Append(indentUnit).Append("variable ").Append(String.Join(", ", locals)).Append(';').Append(newLine).Append(newLine);
			}

			procedureText.Append(BuildBody(text, map, blockStart, blockEnd, indentUnit, newLine)).Append(newLine);
			if (analysis.ResultVariable is not null)
			{
				procedureText.Append(indentUnit).Append("return ").Append(analysis.ResultVariable).Append(';').Append(newLine);
			}

			procedureText.Append("end");

			String assignment = DetectAssignmentOperator(tokens);
			// A procedure called as a statement needs the "call" keyword; as the value of an assignment it does not.
			String call = (analysis.ResultVariable is null ? "call " : analysis.ResultVariable + " " + assignment + " ")
				+ parsed.Name + "(" + String.Join(", ", arguments) + ");";

			Int32 procedureLine = tokens[analysis.Procedure.FirstToken].Line;
			procedureLine = ProcedureComments.FindLeadingCommentStartLine(map, procedureLine);
			Int32 insertOffset = map.GetLineStart(procedureLine);
			String prefix = procedureLine > 1 && !map.IsBlank(procedureLine - 1) ? newLine : String.Empty;

			List<TextEdit> edits = new List<TextEdit>
			{
				TextEdit.Insert(insertOffset, prefix + procedureText + newLine + newLine),
				new TextEdit(blockStart, blockEnd - blockStart, call),
			};
			edits.AddRange(CreateMovedDeclarationEdits(text, analysis));

			IReadOnlyList<TextEdit> ordered = TextEditApplier.Normalize(edits);
			String newText = TextEditApplier.Apply(text, ordered);
			TextLineMap newMap = new TextLineMap(newText);
			Int32 headerLine = newMap.GetLineFromOffset(insertOffset + prefix.Length);
			Int32 callLine = newMap.GetLineFromOffset(TextEditApplier.MapOffset(blockStart, ordered));
			return new ExtractResult(ordered, newText, headerLine, callLine);
		}

		/// <summary>Gets the names of the parameters of a declaration such as <c>Helper(variable a, variable b)</c>.</summary>
		/// <param name="declaration">The declaration (see <see cref="ProcedureNameParser"/>).</param>
		/// <returns>The parameter names; empty when there is no parameter list.</returns>
		public static List<String> GetArgumentNames(String declaration)
		{
			ArgumentNullException.ThrowIfNull(declaration);

			List<String> names = new List<String>();
			Int32 open = declaration.IndexOf('(');
			Int32 close = declaration.LastIndexOf(')');
			if (open < 0 || close < open)
			{
				return names;
			}

			foreach (String part in declaration.Substring(open + 1, close - open - 1).Split(','))
			{
				String argument = part.Trim();
				if (argument.StartsWith("variable ", StringComparison.OrdinalIgnoreCase))
				{
					argument = argument.Substring(9).Trim();
				}

				if (argument.Length > 0)
				{
					names.Add(argument);
				}
			}

			return names;
		}

		private static Boolean IsBalanced(SslTokenStream tokens, Int32 first, Int32 last)
		{
			Int32 blocks = 0;
			Int32 parentheses = 0;
			Int32 brackets = 0;
			for (Int32 i = first; i <= last; i++)
			{
				if (tokens[i].Kind == SslTokenKind.Identifier)
				{
					if (tokens.IsKeyword(i, "begin"))
					{
						blocks++;
					}
					else if (tokens.IsKeyword(i, "end"))
					{
						blocks--;
					}
				}
				else if (tokens[i].Kind == SslTokenKind.Punctuation)
				{
					if (tokens.IsPunctuation(i, '('))
					{
						parentheses++;
					}
					else if (tokens.IsPunctuation(i, ')'))
					{
						parentheses--;
					}
					else if (tokens.IsPunctuation(i, '['))
					{
						brackets++;
					}
					else if (tokens.IsPunctuation(i, ']'))
					{
						brackets--;
					}
				}

				if (blocks < 0 || parentheses < 0 || brackets < 0)
				{
					return false;
				}
			}

			return blocks == 0 && parentheses == 0 && brackets == 0;
		}

		private static Boolean IsCompleteStatements(SslTokenStream tokens, Int32 first, Int32 last)
		{
			if (tokens.IsKeyword(first, "else") || tokens.IsKeyword(first, "then") || tokens.IsKeyword(first, "do") || tokens.IsKeyword(first, "end")
				|| tokens.IsKeyword(first, "case") || tokens.IsKeyword(first, "default") || tokens.IsKeyword(first, "in"))
			{
				return false;
			}

			if (!VariableOccurrenceFinder.IsStatementStart(tokens, first))
			{
				return false;
			}

			Int32 position = first;
			Int32 count = 0;
			while (position <= last)
			{
				Int32 end = SslStatements.FindStatementEnd(tokens, position);
				if (end > last)
				{
					return false;
				}

				position = end + 1;
				count++;
			}

			Boolean singleBody = first > 0 && (tokens.IsKeyword(first - 1, "then") || tokens.IsKeyword(first - 1, "else") || tokens.IsKeyword(first - 1, "do"));
			if (singleBody && count > 1)
			{
				return false;
			}

			return !tokens.IsKeyword(last + 1, "else");
		}

		private static ExtractFailure CheckFlow(SslTokenStream tokens, Int32 first, Int32 last)
		{
			List<(Int32 Start, Int32 End, Boolean IsSwitch)> loops = new List<(Int32 Start, Int32 End, Boolean IsSwitch)>();
			for (Int32 i = first; i <= last; i++)
			{
				if (tokens[i].Kind != SslTokenKind.Identifier)
				{
					continue;
				}

				if (tokens.IsKeyword(i, "return"))
				{
					return ExtractFailure.ContainsReturn;
				}

				Boolean isSwitch = tokens.IsKeyword(i, "switch");
				if (isSwitch || tokens.IsKeyword(i, "while") || tokens.IsKeyword(i, "for") || tokens.IsKeyword(i, "foreach"))
				{
					loops.Add((i, SslStatements.FindStatementEnd(tokens, i), isSwitch));
				}
			}

			for (Int32 i = first; i <= last; i++)
			{
				Boolean isBreak = tokens.IsKeyword(i, "break");
				if (!isBreak && !tokens.IsKeyword(i, "continue"))
				{
					continue;
				}

				Int32 position = i;
				if (!loops.Exists(loop => loop.Start < position && position <= loop.End && (isBreak || !loop.IsSwitch)))
				{
					return ExtractFailure.ContainsLoopControl;
				}
			}

			return ExtractFailure.None;
		}

		private static ExtractAnalysis AnalyzeVariables(ExtractAnalysis analysis, SslScriptModel model, SslProcedureModel procedure, Int32 first, Int32 last)
		{
			SslTokenStream tokens = model.Tokens;
			HashSet<VariableDeclaration> owned = new HashSet<VariableDeclaration>(procedure.Declarations);
			ILookup<VariableDeclaration, VariableOccurrence> occurrences = VariableOccurrenceFinder.Find(model)
				.Where(o => owned.Contains(o.Declaration))
				.ToLookup(o => o.Declaration);

			Int32 loopStart = Int32.MaxValue;
			Int32 loopEnd = -1;
			for (Int32 i = procedure.BeginToken + 1; i < first; i++)
			{
				if (tokens[i].Kind == SslTokenKind.Identifier && (tokens.IsKeyword(i, "while") || tokens.IsKeyword(i, "for") || tokens.IsKeyword(i, "foreach")))
				{
					Int32 end = SslStatements.FindStatementEnd(tokens, i);
					if (end >= last)
					{
						loopStart = Math.Min(loopStart, i);
						loopEnd = Math.Max(loopEnd, end);
					}
				}
			}

			List<(Int32 Position, VariableDeclaration Declaration)> parameters = new List<(Int32 Position, VariableDeclaration Declaration)>();
			List<VariableDeclaration> results = new List<VariableDeclaration>();
			Boolean resultIsLocal = false;
			foreach (VariableDeclaration declaration in procedure.Declarations)
			{
				List<VariableOccurrence> all = occurrences[declaration].OrderBy(o => o.TokenIndex).ToList();
				List<VariableOccurrence> inside = all.Where(o => o.TokenIndex >= first && o.TokenIndex <= last).ToList();
				Boolean declaredInside = declaration.TokenIndex >= first && declaration.TokenIndex <= last;
				List<VariableOccurrence> after = all.Where(o => o.TokenIndex > last).ToList();
				if (declaredInside)
				{
					if (after.Count > 0 || all.Exists(o => tokens[o.TokenIndex].InDirective))
					{
						return new ExtractAnalysis(ExtractFailure.DeclaredVariableUsedAfter, declaration.Name);
					}

					continue;
				}

				if (inside.Count == 0)
				{
					continue;
				}

				Boolean usedOutside = all.Exists(o => o.TokenIndex < first || o.TokenIndex > last);
				if (!usedOutside && declaration.Kind == VariableDeclarationKind.Local && !declaration.HasInitializer && !declaration.IsInParentheses)
				{
					analysis.AddMovedLocal(declaration.Name);
					analysis.MovedDeclarations.Add(declaration);
					continue;
				}

				Boolean modified = inside.Exists(o => IsModified(tokens, o.TokenIndex));
				Boolean liveOut = false;
				if (modified)
				{
					liveOut = after.Count > 0 && !FirstAccessIsWrite(tokens, after);
					if (!liveOut && loopEnd >= 0)
					{
						liveOut = all.Exists(o => !o.IsWrite && (o.TokenIndex < first || o.TokenIndex > last) && o.TokenIndex >= loopStart && o.TokenIndex <= loopEnd);
					}
				}

				if (liveOut)
				{
					results.Add(declaration);
					if (FirstAccessIsWrite(tokens, inside) && declaration.Kind != VariableDeclarationKind.Parameter)
					{
						resultIsLocal = true;
						continue;
					}
				}

				parameters.Add((inside[0].TokenIndex, declaration));
			}

			if (results.Count > 1)
			{
				return new ExtractAnalysis(ExtractFailure.TooManyResults, String.Join(", ", results.Select(r => r.Name)));
			}

			foreach ((Int32 Position, VariableDeclaration Declaration) item in parameters.OrderBy(p => p.Position))
			{
				analysis.AddParameter(item.Declaration.Name);
			}

			if (results.Count == 1)
			{
				analysis.ResultVariable = results[0].Name;
				analysis.ResultIsLocal = resultIsLocal;
			}

			return analysis;
		}

		/// <summary>Determines whether the first of some occurrences only assigns the variable: <c>x = 1;</c>, but not <c>x = x + 1;</c>, which reads it first.</summary>
		private static Boolean FirstAccessIsWrite(SslTokenStream tokens, IReadOnlyList<VariableOccurrence> occurrences)
		{
			VariableOccurrence first = occurrences[0];
			if (!first.IsWrite)
			{
				return false;
			}

			Int32 end = SslStatements.FindStatementEnd(tokens, first.TokenIndex);
			for (Int32 i = 1; i < occurrences.Count; i++)
			{
				if (occurrences[i].TokenIndex <= end)
				{
					return false;
				}
			}

			return true;
		}

		private static Boolean IsModified(SslTokenStream tokens, Int32 index)
		{
			if (tokens.IsPunctuation(index + 1, ":=") || tokens.IsPunctuation(index + 1, "+=") || tokens.IsPunctuation(index + 1, "-=")
				|| tokens.IsPunctuation(index + 1, "*=") || tokens.IsPunctuation(index + 1, "/=") || tokens.IsPunctuation(index + 1, "%=")
				|| tokens.IsPunctuation(index + 1, "++") || tokens.IsPunctuation(index + 1, "--")
				|| tokens.IsPunctuation(index - 1, "++") || tokens.IsPunctuation(index - 1, "--"))
			{
				return true;
			}

			if (tokens.IsPunctuation(index + 1, '=')
				&& (VariableOccurrenceFinder.IsStatementStart(tokens, index) || (tokens.IsPunctuation(index - 1, '(') && tokens.IsKeyword(index - 2, "for"))))
			{
				return true;
			}

			return tokens.IsKeyword(index + 1, "in")
				|| (tokens.IsPunctuation(index + 1, ':') && tokens.IsPunctuation(index - 1, '(') && tokens.IsKeyword(index - 2, "foreach"));
		}

		private static String DetectAssignmentOperator(SslTokenStream tokens)
		{
			Int32 colon = 0;
			Int32 plain = 0;
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				if (tokens.IsPunctuation(i, ":="))
				{
					colon++;
				}
				else if (tokens.IsPunctuation(i, '=') && VariableOccurrenceFinder.IsStatementStart(tokens, i - 1))
				{
					plain++;
				}
			}

			return plain > colon ? "=" : ":=";
		}

		private static String BuildBody(String text, TextLineMap map, Int32 blockStart, Int32 blockEnd, String indentUnit, String newLine)
		{
			Int32 firstLine = map.GetLineFromOffset(blockStart);
			Int32 lastLine = map.GetLineFromOffset(Math.Max(blockEnd - 1, blockStart));
			List<String> lines = new List<String>();
			List<String> indents = new List<String>();
			for (Int32 line = firstLine; line <= lastLine; line++)
			{
				Int32 lineStart = map.GetLineStart(line);
				Int32 from = line == firstLine ? blockStart : lineStart;
				Int32 to = line == lastLine ? blockEnd : map.GetLineEnd(line);
				String content = text.Substring(from, to - from);
				String indent = String.Empty;
				if (line == firstLine)
				{
					String before = text.Substring(lineStart, blockStart - lineStart);
					indent = before.Trim().Length == 0 ? before : String.Empty;
				}
				else
				{
					Int32 n = 0;
					while (n < content.Length && (content[n] == ' ' || content[n] == '\t'))
					{
						n++;
					}

					indent = content.Substring(0, n);
					content = content.Substring(n);
				}

				lines.Add(content);
				indents.Add(indent);
			}

			String common = String.Empty;
			Boolean started = false;
			for (Int32 i = 0; i < lines.Count; i++)
			{
				if (lines[i].Trim().Length == 0)
				{
					continue;
				}

				common = started ? CommonPrefix(common, indents[i]) : indents[i];
				started = true;
			}

			StringBuilder body = new StringBuilder();
			for (Int32 i = 0; i < lines.Count; i++)
			{
				if (i > 0)
				{
					body.Append(newLine);
				}

				if (lines[i].Trim().Length == 0)
				{
					continue;
				}

				String rest = indents[i].Length >= common.Length ? indents[i].Substring(common.Length) : String.Empty;
				body.Append(indentUnit).Append(rest).Append(lines[i]);
			}

			return body.ToString();
		}

		private static String CommonPrefix(String a, String b)
		{
			Int32 n = 0;
			while (n < a.Length && n < b.Length && a[n] == b[n])
			{
				n++;
			}

			return a.Substring(0, n);
		}

		private static IEnumerable<TextEdit> CreateMovedDeclarationEdits(String text, ExtractAnalysis analysis)
		{
			foreach (IGrouping<DeclarationStatement, VariableDeclaration> group in analysis.MovedDeclarations.GroupBy(d => d.Statement))
			{
				DeclarationStatement statement = group.Key;
				if (statement.Items.Count == group.Count())
				{
					yield return VariableDeclarationEditor.CreateStatementRemoval(text, statement);
					continue;
				}

				List<VariableDeclaration> remaining = statement.Items.Where(item => !group.Contains(item)).ToList();
				Int32 start = statement.Items[0].ItemStart;
				Int32 end = statement.Items[statement.Items.Count - 1].ItemEnd;
				String joined = String.Join(", ", remaining.Select(item => text.Substring(item.ItemStart, item.ItemEnd - item.ItemStart)));
				yield return new TextEdit(start, end - start, joined);
			}
		}
	}
}
