namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// Plans the removal of a procedure that nothing uses: the procedure with the comment block that documents it and its forward
	/// declaration, leaving one blank line between its neighbours. Deciding that nothing uses the procedure is the job of the caller
	/// (see <see cref="IsUsage"/>); engine handlers are never removed (see <see cref="IsEngineHandler"/>).
	/// </summary>
	public static class UnusedProcedureRemover
	{
		private const String HandlerSuffix = "_p_proc";

		private static readonly InternalParser _parser = new InternalParser();

		/// <summary>Determines whether a procedure is called by the game (or by sfall) under its name, so that no script has to mention it.</summary>
		/// <param name="name">The procedure name.</param>
		/// <param name="handlerNames">The names of <c>prochandlers.ini</c> (the standard handlers and dialogue procedures), or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> for <c>start</c>, the <c>*_p_proc</c> handlers and the names of the list.</returns>
		public static Boolean IsEngineHandler(String name, IEnumerable<String>? handlerNames = null)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (name.EndsWith(HandlerSuffix, StringComparison.OrdinalIgnoreCase) || new ScriptProcedure(name).IsStandard)
			{
				return true;
			}

			if (handlerNames is null)
			{
				return false;
			}

			foreach (String handler in handlerNames)
			{
				String plain = handler;
				Int32 open = plain.IndexOf('(');
				if (open > 0)
				{
					plain = plain.Substring(0, open);
				}

				if (String.Equals(plain.Trim(), name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Determines whether an occurrence of a procedure name is a use of the procedure (a call, a reference by name or a string that holds its name).</summary>
		/// <param name="occurrence">The occurrence.</param>
		/// <returns><see langword="false"/> for the <c>procedure</c> line of a declaration or of the body.</returns>
		public static Boolean IsUsage(SymbolOccurrence occurrence)
		{
			ArgumentNullException.ThrowIfNull(occurrence);
			return occurrence.Kind != SymbolOccurrenceKind.Declaration;
		}

		/// <summary>Plans the removal of a procedure.</summary>
		/// <param name="text">The text of the script that holds the body of the procedure.</param>
		/// <param name="procedureName">The name of the procedure (the case is ignored).</param>
		/// <returns>The plan, or <see langword="null"/> when the text has no body of that name.</returns>
		public static ProcedureRemovalPlan? Plan(String text, String procedureName)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentException.ThrowIfNullOrEmpty(procedureName);

			ScriptProcedure? procedure = _parser.ParseProcedures(text, null)
				.FirstOrDefault(p => p.StartLine > 0 && p.EndLine > 0 && String.Equals(p.Name, procedureName, StringComparison.OrdinalIgnoreCase));
			TextLineMap map = new TextLineMap(text);
			if (procedure is null || procedure.EndLine > map.LineCount)
			{
				return null;
			}

			Int32 headerLine = procedure.StartLine;
			Int32 endLine = procedure.EndLine;
			Int32 firstLine = ProcedureComments.FindLeadingCommentStartLine(map, headerLine);

			Int32 removeStart;
			Int32 removeEnd;
			if (endLine < map.LineCount)
			{
				removeStart = map.GetLineStart(firstLine);
				removeEnd = map.GetNextLineStart(endLine);
				if (map.IsBlank(endLine + 1))
				{
					if (firstLine > 1 && map.IsBlank(firstLine - 1))
					{
						removeStart = map.GetLineStart(firstLine - 1);
					}
					else if (firstLine == 1)
					{
						removeEnd = map.GetNextLineStart(endLine + 1);
					}
				}
			}
			else
			{
				Int32 previous = firstLine - 1;
				while (previous >= 1 && map.IsBlank(previous))
				{
					previous--;
				}

				removeStart = previous >= 1 ? map.GetLineEnd(previous) : 0;
				removeEnd = map.Text.Length;
			}

			List<TextEdit> edits = new List<TextEdit> { new TextEdit(removeStart, removeEnd - removeStart, String.Empty) };
			String? declarationText = null;
			if (procedure.HasForwardDeclaration && procedure.DeclaredLine >= 1 && procedure.DeclaredLine <= map.LineCount)
			{
				Int32 start = map.GetLineStart(procedure.DeclaredLine);
				Int32 end = map.GetNextLineStart(procedure.DeclaredLine);
				if (end <= removeStart || start >= removeEnd)
				{
					edits.Add(new TextEdit(start, end - start, String.Empty));
					declarationText = map.GetLineText(procedure.DeclaredLine).Trim();
				}
			}

			return new ProcedureRemovalPlan(
				procedure.Name,
				firstLine,
				endLine,
				map.GetLinesText(firstLine, endLine),
				declarationText,
				procedure.IsExported,
				TextEditApplier.Normalize(edits));
		}
	}
}
