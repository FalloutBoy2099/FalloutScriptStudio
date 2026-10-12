namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Moving a procedure (and its forward declaration) before another one.</summary>
	public sealed partial class ProcedureTextService
	{
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
