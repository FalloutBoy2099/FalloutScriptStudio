namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Default <see cref="IProcedureTextService"/>, built on <see cref="ProcedureLocator"/> and <see cref="InternalParser"/>.
	/// </summary>
	/// <remarks>
	/// Fixes of the legacy behaviour: a new procedure is inserted with exactly one blank line on each side (the legacy code
	/// inserted at "the trimmed length of a line", which broke on indented lines); a forward declaration is never inserted
	/// inside the body of a procedure that has no declaration of its own; a caret inside a procedure inserts after that
	/// procedure; moving a procedure works also when it has no forward declaration; and the blank-line rules of deleting
	/// apply at the end of the file too.
	/// </remarks>
	public sealed class ProcedureTextService : IProcedureTextService
	{
		private readonly InternalParser _parser;
		private readonly ProcedureLocator _locator;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcedureTextService"/> class with default collaborators.
		/// </summary>
		public ProcedureTextService()
			: this(new InternalParser(), new ProcedureLocator())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcedureTextService"/> class.
		/// </summary>
		/// <param name="parser">The text based procedure parser.</param>
		/// <param name="locator">The procedure locator.</param>
		public ProcedureTextService(InternalParser parser, ProcedureLocator locator)
		{
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(locator);
			this._parser = parser;
			this._locator = locator;
		}

		/// <inheritdoc />
		public String? GetProcedureCode(String text, ScriptProcedure procedure)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedure);

			if (procedure.StartLine <= 0 || procedure.EndLine <= 0)
			{
				return null;
			}

			var map = new TextLineMap(text);
			return ReadBody(map, procedure.StartLine, procedure.EndLine);
		}

		/// <inheritdoc />
		public String? GetProcedureCode(String text, String procedureName)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureName);

			ProcedureBlock block = this.FindBlock(text, procedureName);
			if (block.BeginLine <= 0 || block.EndLine <= 0)
			{
				return null;
			}

			return ReadBody(new TextLineMap(text), block.BeginLine, block.EndLine);
		}

		/// <inheritdoc />
		public IReadOnlyDictionary<String, String> GetProcedureCodes(String text, IEnumerable<String> procedureNames)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureNames);

			var wanted = new HashSet<String>(procedureNames, StringComparer.OrdinalIgnoreCase);
			var result = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
			if (wanted.Count == 0)
			{
				return result;
			}

			var map = new TextLineMap(text);
			foreach (ScriptProcedure procedure in this._parser.ParseProcedures(text, null))
			{
				if (wanted.Contains(procedure.Name) && procedure.StartLine > 0 && procedure.EndLine > 0)
				{
					String? body = ReadBody(map, procedure.StartLine, procedure.EndLine);
					if (body is not null)
					{
						result[procedure.Name] = body;
					}
				}
			}

			return result;
		}

		/// <inheritdoc />
		public TextEdit? ReplaceProcedureCode(String text, String procedureName, String newCode)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureName);
			ArgumentNullException.ThrowIfNull(newCode);

			ProcedureBlock block = this.FindBlock(text, procedureName);
			var map = new TextLineMap(text);
			if (block.BeginLine <= 0 || block.EndLine <= 0 || block.EndLine > map.LineCount)
			{
				return null;
			}

			(Int32 first, Int32 last) = GetBodyRange(map, block.BeginLine, block.EndLine);
			if (last < first)
			{
				String code = newCode;
				if (code.Length > 0 && !code.EndsWith('\n'))
				{
					code += TextLineMap.DetectNewLine(text);
				}

				return TextEdit.Insert(map.GetLineStart(block.EndLine), code);
			}

			Int32 start = map.GetLineStart(first);
			return new TextEdit(start, map.GetLineEnd(last) - start, newCode);
		}

		/// <inheritdoc />
		public ProcedureEditResult? DeleteProcedure(String text, String procedureName)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureName);

			ProcedureBlock block = this.FindBlock(text, procedureName);
			var map = new TextLineMap(text);
			if (block.BeginLine <= 0 || block.EndLine <= 0 || block.EndLine > map.LineCount)
			{
				return null;
			}

			Int32 declarationLine = this._locator.GetDeclarationLine(text, procedureName);
			var edits = new List<TextEdit>();
			Int32 removeStart;
			Int32 removeEnd;
			if (block.EndLine < map.LineCount)
			{
				removeStart = map.GetLineStart(block.BeginLine);
				removeEnd = map.GetNextLineStart(block.EndLine);
				if (map.IsBlank(block.EndLine + 1))
				{
					if (block.BeginLine > 1 && map.IsBlank(block.BeginLine - 1))
					{
						removeStart = map.GetLineStart(block.BeginLine - 1);
					}
					else if (block.BeginLine == 1)
					{
						removeEnd = map.GetNextLineStart(block.EndLine + 1);
					}
				}
			}
			else
			{
				Int32 previous = block.BeginLine - 1;
				while (previous >= 1 && map.IsBlank(previous))
				{
					previous--;
				}

				removeStart = previous >= 1 ? map.GetLineEnd(previous) : 0;
				removeEnd = text.Length;
			}

			edits.Add(new TextEdit(removeStart, removeEnd - removeStart, String.Empty));

			String? removedDeclaration = null;
			if (declarationLine > 0 && declarationLine != block.BeginLine && declarationLine <= map.LineCount)
			{
				Int32 start = map.GetLineStart(declarationLine);
				Int32 end = map.GetNextLineStart(declarationLine);
				if (end <= removeStart || start >= removeEnd)
				{
					edits.Add(new TextEdit(start, end - start, String.Empty));
					removedDeclaration = map.GetLineText(declarationLine);
				}
			}

			IReadOnlyList<TextEdit> ordered = TextEditApplier.Normalize(edits);
			return new ProcedureEditResult(ordered, TextEditApplier.Apply(text, ordered))
			{
				RemovedBody = map.GetLinesText(block.BeginLine, block.EndLine),
				RemovedDeclaration = removedDeclaration,
			};
		}

		/// <inheritdoc />
		public ProcedureEditResult DeleteProcedures(String text, IEnumerable<String> procedureNames)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureNames);

			var builder = new TextEditBuilder(text);
			foreach (String name in procedureNames)
			{
				ProcedureEditResult? deletion = this.DeleteProcedure(builder.CurrentText, name);
				if (deletion is not null)
				{
					ApplyToBuilder(builder, deletion.Edits);
				}
			}

			return new ProcedureEditResult(builder.ToEdits(), builder.CurrentText);
		}

		/// <inheritdoc />
		public String BuildProcedureBlock(ProcedureInsertRequest request, String newLine, String? copiedBody = null)
		{
			ArgumentNullException.ThrowIfNull(request);
			ArgumentNullException.ThrowIfNull(newLine);

			ParsedProcedureName parsed = ProcedureNameParser.Parse(request.Declaration, true);
			if (!parsed.IsValid)
			{
				throw new ArgumentException(parsed.Error, nameof(request));
			}

			var block = new StringBuilder();
			block.Append("procedure ").Append(parsed.Declaration).Append(" begin");
			if (request.Body is not null)
			{
				block.Append(newLine).Append(ConvertLineBreaks(request.Body, newLine)).Append(newLine).Append("end");
			}
			else if (request.CopyAnchorBody && copiedBody is not null)
			{
				block.Append(newLine);
				if (copiedBody.Length > 0)
				{
					block.Append(ConvertLineBreaks(copiedBody, newLine)).Append(newLine);
				}

				block.Append("end");
			}
			else if (request.UseScriptOverrides)
			{
				block.Append(newLine).Append(request.Indentation).Append("script_overrides;").Append(newLine).Append(newLine).Append("end");
			}
			else
			{
				block.Append(newLine).Append(newLine).Append("end");
			}

			return block.ToString();
		}

		/// <inheritdoc />
		public ProcedureEditResult? InsertProcedure(String text, ProcedureInsertRequest request)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(request);

			ParsedProcedureName parsed = ProcedureNameParser.Parse(request.Declaration, true);
			if (!parsed.IsValid)
			{
				throw new ArgumentException(parsed.Error, nameof(request));
			}

			String name = parsed.Name;
			String newLine = request.NewLine ?? TextLineMap.DetectNewLine(text);
			var map = new TextLineMap(text);
			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(text, null);
			ScriptProcedure? existing = procedures.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
			if (existing is not null && existing.StartLine > 0)
			{
				return null;
			}

			List<ScriptProcedure> bodies = procedures.Where(p => p.StartLine > 0 && p.EndLine > 0).OrderBy(p => p.StartLine).ToList();
			ScriptProcedure? top = bodies.FirstOrDefault();
			Int32 regionEnd = top is null ? GetLastNonBlankLine(map) : this._locator.GetDeclarationRegion(text, top.StartLine).EndLine;

			InsertPlacement placement = request.Placement;
			ScriptProcedure? anchor = null;
			Int32 caretLine = Math.Clamp(request.CaretLine, 1, map.LineCount);
			if (placement == InsertPlacement.Caret && (map.LineCount == 1 || (regionEnd > 0 && caretLine <= regionEnd)))
			{
				placement = InsertPlacement.End;
			}

			if (placement == InsertPlacement.After)
			{
				anchor = request.AnchorProcedure is null
					? null
					: bodies.FirstOrDefault(p => String.Equals(p.Name, request.AnchorProcedure, StringComparison.OrdinalIgnoreCase));
				if (anchor is null)
				{
					placement = InsertPlacement.End;
				}
			}

			ScriptProcedure? neighbor = null;
			Boolean above = false;
			Int32 afterLine = -1;
			if (placement == InsertPlacement.Caret)
			{
				ScriptProcedure? containing = bodies.FirstOrDefault(p => caretLine >= p.StartLine && caretLine <= p.EndLine);
				if (containing is not null)
				{
					placement = InsertPlacement.After;
					anchor = containing;
				}
				else
				{
					neighbor = bodies.FirstOrDefault(p => p.StartLine > caretLine) ?? bodies.LastOrDefault();
					above = neighbor is not null && caretLine < neighbor.StartLine;
					afterLine = map.IsBlank(caretLine) ? caretLine : caretLine - 1;
				}
			}

			if (placement == InsertPlacement.After && anchor is not null)
			{
				neighbor = anchor;
				afterLine = anchor.EndLine;
			}

			String? copiedBody = null;
			if (request.CopyAnchorBody && anchor is not null)
			{
				copiedBody = this.GetProcedureCode(text, anchor) ?? String.Empty;
			}

			String block = this.BuildProcedureBlock(request, newLine, copiedBody);
			(Int32 blockOffset, String blockText, Int32 blockPrefix) = placement == InsertPlacement.End
				? PlanAppend(map, block, newLine)
				: PlanInsertAfterLine(map, afterLine, block, newLine);

			var edits = new List<TextEdit>();
			Int32 declarationDelta = 0;
			if (request.AddDeclaration && existing is null)
			{
				Int32 declarationLine = this.GetDeclarationInsertionLine(text, neighbor, above, regionEnd);
				String declaration = "procedure " + parsed.Declaration + ";" + newLine;
				if (declarationLine <= map.LineCount)
				{
					edits.Add(TextEdit.Insert(map.GetLineStart(declarationLine), declaration));
				}
				else
				{
					edits.Add(TextEdit.Insert(text.Length, EndsWithLineBreak(map) ? declaration : newLine + declaration));
				}

				if (edits[0].Offset <= blockOffset)
				{
					declarationDelta = edits[0].NewText.Length;
				}
			}

			edits.Add(TextEdit.Insert(blockOffset, blockText));
			IReadOnlyList<TextEdit> ordered = TextEditApplier.Normalize(edits);
			String newText = TextEditApplier.Apply(text, ordered);

			Int32 headerOffset = blockOffset + declarationDelta + blockPrefix;
			Int32 headerLine = new TextLineMap(newText).GetLineFromOffset(headerOffset);
			Boolean overridesAdded = request.Body is null && copiedBody is null && request.UseScriptOverrides;
			Int32 focus = headerLine + 1 + (overridesAdded ? 1 : 0);
			return new ProcedureEditResult(ordered, newText) { FocusLine = focus };
		}

		/// <inheritdoc />
		public ProcedureEditResult? MoveProcedure(String text, String procedureName, String? beforeProcedure)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(procedureName);

			if (beforeProcedure is not null && String.Equals(beforeProcedure, procedureName, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			ProcedureBlock moved = this.FindBlock(text, procedureName);
			if (moved.BeginLine <= 0 || moved.EndLine <= 0)
			{
				return null;
			}

			if (beforeProcedure is not null)
			{
				ProcedureBlock target = this.FindBlock(text, beforeProcedure);
				if (target.BeginLine <= 0)
				{
					return null;
				}
			}

			String newLine = TextLineMap.DetectNewLine(text);
			ProcedureEditResult? deletion = this.DeleteProcedure(text, procedureName);
			if (deletion is null || deletion.RemovedBody is null)
			{
				return null;
			}

			var builder = new TextEditBuilder(text);
			ApplyToBuilder(builder, deletion.Edits);

			if (deletion.RemovedDeclaration is not null)
			{
				String current = builder.CurrentText;
				var currentMap = new TextLineMap(current);
				Int32 line = this.GetMoveDeclarationLine(current, beforeProcedure);
				Int32 offset = line <= currentMap.LineCount ? currentMap.GetLineStart(line) : current.Length;
				String declaration = deletion.RemovedDeclaration + newLine;
				if (offset == current.Length && !EndsWithLineBreak(currentMap) && current.Length > 0)
				{
					declaration = newLine + deletion.RemovedDeclaration;
				}

				builder.Insert(offset, declaration);
			}

			String afterDeclaration = builder.CurrentText;
			var map = new TextLineMap(afterDeclaration);
			Int32 afterLine;
			if (beforeProcedure is not null)
			{
				ProcedureBlock target = this.FindBlock(afterDeclaration, beforeProcedure);
				afterLine = target.BeginLine - 1;
			}
			else
			{
				ScriptProcedure? last = this._parser.ParseProcedures(afterDeclaration, null)
					.Where(p => p.StartLine > 0 && p.EndLine > 0)
					.OrderByDescending(p => p.StartLine)
					.FirstOrDefault();
				if (last is null)
				{
					(Int32 appendOffset, String appendText, Int32 _) = PlanAppend(map, deletion.RemovedBody, newLine);
					builder.Insert(appendOffset, appendText);
					return new ProcedureEditResult(builder.ToEdits(), builder.CurrentText);
				}

				afterLine = last.EndLine;
			}

			(Int32 blockOffset, String blockText, Int32 _) = PlanInsertAfterLine(map, afterLine, deletion.RemovedBody, newLine);
			builder.Insert(blockOffset, blockText);

			String newText = builder.CurrentText;
			ProcedureBlock result = this.FindBlock(newText, procedureName);
			return new ProcedureEditResult(builder.ToEdits(), newText) { FocusLine = result.BeginLine > 0 ? result.BeginLine : -1 };
		}

		private static Int32 GetLastNonBlankLine(TextLineMap map)
		{
			for (Int32 line = map.LineCount; line >= 1; line--)
			{
				if (!map.IsBlank(line))
				{
					return line;
				}
			}

			return -1;
		}

		private static Boolean EndsWithLineBreak(TextLineMap map)
		{
			return map.LineCount > 1 && map.GetLineLength(map.LineCount) == 0;
		}

		private static String ConvertLineBreaks(String text, String newLine)
		{
			return NewLineTools.ToUnixLineEndings(text).Replace("\n", newLine, StringComparison.Ordinal);
		}

		private static void ApplyToBuilder(TextEditBuilder builder, IEnumerable<TextEdit> edits)
		{
			foreach (TextEdit edit in edits.OrderByDescending(e => e.Offset))
			{
				builder.Replace(edit.Offset, edit.Length, edit.NewText);
			}
		}

		private static String? ReadBody(TextLineMap map, Int32 headerLine, Int32 endLine)
		{
			if (headerLine > map.LineCount || endLine > map.LineCount)
			{
				return null;
			}

			(Int32 first, Int32 last) = GetBodyRange(map, headerLine, endLine);
			return last < first ? String.Empty : map.GetLinesText(first, last);
		}

		/// <summary>Finds the first and the last line of the body: the lines between <c>begin</c> and <c>end</c>.</summary>
		private static (Int32 First, Int32 Last) GetBodyRange(TextLineMap map, Int32 headerLine, Int32 endLine)
		{
			Int32 first = headerLine + 1;
			for (Int32 line = headerLine; line < endLine && line <= map.LineCount; line++)
			{
				if (EndsWithBegin(map.GetLineText(line)))
				{
					first = line + 1;
					break;
				}
			}

			return (first, endLine - 1);
		}

		private static Boolean EndsWithBegin(String line)
		{
			String trimmed = line.TrimStart();
			if (trimmed.StartsWith(SslKeywords.LineComment, StringComparison.Ordinal))
			{
				return false;
			}

			String code = SslSyntax.RemoveTrailingComment(trimmed, 0).TrimEnd();
			if (!code.EndsWith(SslKeywords.Begin, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			Int32 before = code.Length - SslKeywords.Begin.Length - 1;
			return before < 0 || !WordScanner.IsWordChar(code[before]);
		}

		/// <summary>Plans the insertion of a block after a line, with a blank line on each side.</summary>
		private static (Int32 Offset, String Text, Int32 BlockStart) PlanInsertAfterLine(TextLineMap map, Int32 afterLine, String block, String newLine)
		{
			Int32 line = Math.Clamp(afterLine, 0, map.LineCount);
			Boolean hasNext = line < map.LineCount;
			Boolean blankBefore = line >= 1 && !map.IsBlank(line);
			Boolean blankAfter = hasNext && !map.IsBlank(line + 1);
			if (line == 0)
			{
				return (0, block + newLine + (blankAfter ? newLine : String.Empty), 0);
			}

			if (hasNext)
			{
				String prefix = blankBefore ? newLine : String.Empty;
				return (map.GetLineStart(line + 1), prefix + block + newLine + (blankAfter ? newLine : String.Empty), prefix.Length);
			}

			String lead = newLine + (blankBefore ? newLine : String.Empty);
			return (map.Text.Length, lead + block, lead.Length);
		}

		/// <summary>Plans the insertion of a block at the end of the text, with a blank line before it.</summary>
		private static (Int32 Offset, String Text, Int32 BlockStart) PlanAppend(TextLineMap map, String block, String newLine)
		{
			if (map.Text.Length == 0)
			{
				return (0, block + newLine, 0);
			}

			if (EndsWithLineBreak(map))
			{
				String prefix = map.IsBlank(map.LineCount - 1) ? String.Empty : newLine;
				return (map.Text.Length, prefix + block + newLine, prefix.Length);
			}

			String lead = newLine + (map.IsBlank(map.LineCount) ? String.Empty : newLine);
			return (map.Text.Length, lead + block, lead.Length);
		}

		private ProcedureBlock FindBlock(String text, String name)
		{
			return this._locator.GetProcedureBlock(text, name, 1, true);
		}

		/// <summary>Finds the line (1-based) before which a new forward declaration is inserted.</summary>
		private Int32 GetDeclarationInsertionLine(String text, ScriptProcedure? neighbor, Boolean above, Int32 regionEnd)
		{
			if (neighbor is not null && neighbor.HasForwardDeclaration)
			{
				return above ? neighbor.DeclaredLine : neighbor.DeclaredLine + 1;
			}

			Int32 line = this._locator.GetDeclarationInsertLine(text);
			if (line < 0)
			{
				line = regionEnd > 0 ? regionEnd + 1 : 1;
			}

			return Math.Max(line, 1);
		}

		/// <summary>Finds the line (1-based) before which the declaration of a moved procedure is inserted.</summary>
		private Int32 GetMoveDeclarationLine(String text, String? beforeProcedure)
		{
			if (beforeProcedure is not null)
			{
				Int32 declared = this._locator.GetDeclarationLine(text, beforeProcedure);
				if (declared > 0)
				{
					return declared;
				}
			}
			else
			{
				ScriptProcedure? last = this._parser.ParseProcedures(text, null)
					.Where(p => p.StartLine > 0)
					.OrderByDescending(p => p.StartLine)
					.FirstOrDefault();
				if (last is not null && last.HasForwardDeclaration)
				{
					return last.DeclaredLine + 1;
				}
			}

			Int32 line = this._locator.GetDeclarationInsertLine(text);
			return line < 0 ? 1 : Math.Max(line, 1);
		}
	}
}
