namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Text based helpers used when editing procedures (insert, move, delete): they locate the declaration, the body and
	/// the declaration section of a script. The class is stateless; every call works on the text it is given.
	/// </summary>
	/// <remarks>All line numbers taken and returned are 1-based; -1 means "not found".</remarks>
	public sealed class ProcedureLocator
	{
		/// <summary>
		/// Finds the lines of the body of a procedure (legacy <c>GetProcedureBlock</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="name">Procedure name (case-insensitive).</param>
		/// <param name="startLine">1-based line at which the search starts.</param>
		/// <param name="useProcedureLine">
		/// When <c>true</c> and <c>begin</c> is on a separate line, <see cref="ProcedureBlock.BeginLine"/> is the line of the
		/// <c>procedure</c> keyword instead of the <c>begin</c> line.
		/// </param>
		/// <param name="diagnostics">Receives errors and warnings; may be <c>null</c>.</param>
		/// <returns>
		/// The block. <see cref="ProcedureBlock.DeclarationLine"/> is the <c>procedure</c> line that was matched last
		/// (the header of the body, or a forward declaration when there is no body). When the body cannot be found
		/// <see cref="ProcedureBlock.BeginLine"/> and <see cref="ProcedureBlock.EndLine"/> are -1.
		/// </returns>
		public ProcedureBlock GetProcedureBlock(String text, String name, Int32 startLine = 1, Boolean useProcedureLine = false, ParseDiagnostics? diagnostics = null)
		{
			String[] lines = SslSyntax.SplitLines(text.ToLowerInvariant());
			String lowerName = name.ToLowerInvariant();
			Int32 nameLength = lowerName.Length;
			var comments = new CommentTracker();

			Int32 procFound = 0;
			Int32 beginFound = 0;
			Int32 procLine = 0;
			Int32 declaration = -1;
			Int32 begin = 0;

			for (Int32 i = Math.Max(startLine - 1, 0); i < lines.Length; i++)
			{
				lines[i] = lines[i].Trim();
				if (comments.ShouldSkip(ref lines[i]))
				{
					continue;
				}

				lines[i] = lines[i].Replace('\t', ' ');
				SslSyntax.StripProcedureModifiers(ref lines[i]);

				if (procFound == 0 && SslSyntax.IsProcedureLine(ref lines[i], lowerName))
				{
					declaration = i;
					String next = " ";
					if (lines[i].Length > SslKeywords.ProcedureLength + nameLength)
					{
						next = lines[i].Substring(SslKeywords.ProcedureLength + nameLength, 1);
					}

					if (next != " " && next != "(")
					{
						continue;
					}

					procFound++;
					lines[i] = SslSyntax.RemoveTrailingComment(lines[i], SslKeywords.ProcedureLength + nameLength);
					if (lines[i].EndsWith(SslKeywords.Begin, StringComparison.Ordinal))
					{
						begin = i;
						beginFound++;
					}
					else
					{
						procLine = i;
					}

					continue;
				}

				if (procFound > 0 && beginFound == 0)
				{
					if (lines[i].StartsWith(SslKeywords.Begin, StringComparison.Ordinal))
					{
						beginFound++;
						begin = useProcedureLine ? procLine : i;
						continue;
					}

					if (lines[i].StartsWith(SslKeywords.Procedure, StringComparison.Ordinal))
					{
						procFound--;
						i--;
						continue;
					}
				}

				if (procFound > 0 && beginFound > 0 && lines[i].StartsWith(SslKeywords.Procedure, StringComparison.Ordinal))
				{
					for (Int32 j = i - 1; j > 0; j--)
					{
						if (lines[j].StartsWith(SslKeywords.End, StringComparison.Ordinal))
						{
							return CreateBlock(name, declaration, begin, j);
						}

						if (j <= begin)
						{
							diagnostics?.Add(
								ParseDiagnosticSeverity.Error,
								ProcedureScanner.DiagnosticSource,
								begin + 1,
								"When parsing of procedure '" + name + "' of the keyword 'end' was not found.");
							return CreateBlock(name, declaration, begin, -1);
						}
					}
				}
			}

			if (procFound > 0 && beginFound > 0)
			{
				for (Int32 i = lines.Length - 1; i > 0; i--)
				{
					if (lines[i].StartsWith(SslKeywords.End, StringComparison.Ordinal))
					{
						return CreateBlock(name, declaration, begin, i);
					}
				}
			}

			diagnostics?.Add(
				ParseDiagnosticSeverity.Warning,
				ProcedureScanner.DiagnosticSource,
				declaration + 1,
				"When parsing the procedure '" + name + "' an unexpected error occurred, the construction of the code was not determined.");
			return CreateBlock(name, declaration, -1, -1);
		}

		/// <summary>
		/// Finds the forward declaration (<c>procedure Foo;</c>) of a procedure (legacy <c>GetDeclarationProcedureLine</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="name">Procedure name (case-insensitive).</param>
		/// <returns>The 1-based line, or -1 when there is no forward declaration.</returns>
		public Int32 GetDeclarationLine(String text, String name)
		{
			String[] lines = SslSyntax.SplitLines(text.ToLowerInvariant());
			String lowerName = name.ToLowerInvariant();
			Int32 prefix = SslKeywords.ProcedureLength + lowerName.Length;
			var comments = new CommentTracker();

			for (Int32 i = 0; i < lines.Length; i++)
			{
				lines[i] = lines[i].Trim();
				if (comments.ShouldSkip(ref lines[i]))
				{
					continue;
				}

				SslSyntax.StripProcedureModifiers(ref lines[i]);
				if (!SslSyntax.IsProcedureLine(ref lines[i], lowerName) || lines[i].Length <= prefix)
				{
					continue;
				}

				lines[i] = SslSyntax.RemoveTrailingComment(lines[i], prefix);
				if (lines[i].LastIndexOf(';') >= prefix)
				{
					return i + 1;
				}
			}

			return -1;
		}

		/// <summary>
		/// Finds the line at which a new forward declaration should be inserted, that is the line that follows the last
		/// declaration of the declaration section (legacy <c>GetEndLineProcDeclaration</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <returns>
		/// The 1-based line before which the declaration should be inserted (0 means "before the first line"),
		/// or -1 when the script has no procedure body.
		/// </returns>
		public Int32 GetDeclarationInsertLine(String text)
		{
			String[] lines = SslSyntax.SplitLines(text.ToLowerInvariant());
			var comments = new CommentTracker();

			for (Int32 i = 0; i < lines.Length; i++)
			{
				lines[i] = lines[i].Trim();
				if (comments.ShouldSkip(ref lines[i]))
				{
					continue;
				}

				lines[i] = SslSyntax.RemoveTrailingComment(lines[i], 0);
				if (!lines[i].EndsWith(SslKeywords.Begin, StringComparison.Ordinal))
				{
					continue;
				}

				SslSyntax.StripProcedureModifiers(ref lines[i]);
				Boolean previousIsProcedure = i > 0 && lines[i - 1].StartsWith(SslKeywords.Procedure, StringComparison.Ordinal);
				if (!lines[i].StartsWith(SslKeywords.Procedure, StringComparison.Ordinal) && !previousIsProcedure)
				{
					continue;
				}

				for (Int32 j = i - 1; j >= 0; j--)
				{
					String candidate = lines[j];
					SslSyntax.StripProcedureModifiers(ref candidate);
					if (candidate.StartsWith(SslKeywords.Procedure, StringComparison.Ordinal))
					{
						return candidate.IndexOf(';') > 0 ? j + 2 : j;
					}
				}

				i++;
			}

			return -1;
		}

		/// <summary>
		/// Finds the declaration region of a script: the block of includes, defines and procedure declarations that
		/// precedes the first procedure body (legacy <c>GetRegionDeclaration</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="firstProcedureLine">1-based line on which the first procedure body starts.</param>
		/// <returns>
		/// The region; <see cref="LineRange.EndLine"/> is -1 when the region has no non-blank line before the first procedure.
		/// </returns>
		public LineRange GetDeclarationRegion(String text, Int32 firstProcedureLine)
		{
			Int32 limit = Math.Max(firstProcedureLine - 1, 0);
			String[] lines = text.Split('\n', limit + 1);
			Int32 count = lines.Length - 1;
			var comments = new CommentTracker();

			Boolean codeSeen = false;
			Boolean previousNotEmpty = false;
			Int32 begin = 0;
			Int32 end = -1;

			for (Int32 i = 0; i < count; i++)
			{
				lines[i] = lines[i].Trim();
				if (codeSeen)
				{
					if (lines[i].Length == 0)
					{
						previousNotEmpty = false;
						continue;
					}

					begin = previousNotEmpty ? i - 1 : i;
					break;
				}

				previousNotEmpty = lines[i].Length > 0;
				if (comments.ShouldSkip(ref lines[i]))
				{
					continue;
				}

				codeSeen = true;
			}

			for (Int32 i = Math.Min(limit - 1, lines.Length - 1); i > begin; i--)
			{
				if (lines[i].Trim().Length > 0)
				{
					end = i;
					break;
				}
			}

			return new LineRange(begin + 1, end >= 0 ? end + 1 : -1);
		}

		private static ProcedureBlock CreateBlock(String name, Int32 declaration, Int32 begin, Int32 end)
		{
			return new ProcedureBlock(name, ToOneBased(declaration), ToOneBased(begin), ToOneBased(end));
		}

		private static Int32 ToOneBased(Int32 index)
		{
			return index >= 0 ? index + 1 : -1;
		}
	}
}
