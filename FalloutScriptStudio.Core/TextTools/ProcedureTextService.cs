namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using Parsing;

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
	public sealed partial class ProcedureTextService : IProcedureTextService
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

		private ProcedureBlock FindBlock(String text, String name)
		{
			return this._locator.GetProcedureBlock(text, name, 1, true);
		}
	}
}