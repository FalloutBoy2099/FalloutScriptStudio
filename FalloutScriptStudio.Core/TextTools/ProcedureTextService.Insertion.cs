namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Building and inserting a procedure with its forward declaration.</summary>
	public sealed partial class ProcedureTextService
	{
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
	}
}
