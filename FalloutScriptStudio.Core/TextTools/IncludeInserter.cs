namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Pastes an <c>#include "file"</c> line into a script (legacy <c>Utilities.PasteIncludeFile</c>).
	/// </summary>
	public sealed class IncludeInserter
	{
		private readonly InternalParser _parser;
		private readonly ProcedureLocator _locator;

		/// <summary>
		/// Initializes a new instance of the <see cref="IncludeInserter"/> class with default collaborators.
		/// </summary>
		public IncludeInserter()
			: this(new InternalParser(), new ProcedureLocator())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IncludeInserter"/> class.
		/// </summary>
		/// <param name="parser">The text based procedure parser.</param>
		/// <param name="locator">The procedure locator.</param>
		public IncludeInserter(InternalParser parser, ProcedureLocator locator)
		{
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(locator);
			this._parser = parser;
			this._locator = locator;
		}

		/// <summary>
		/// Creates the insertion of an include line.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="headerFile">The file name or relative path written between the quotes.</param>
		/// <param name="caretLine">The 1-based line of the caret. When it is inside the declaration region of the script the line is inserted there.</param>
		/// <returns>
		/// The insertion. Otherwise the line goes after the last <c>#include</c> of the declaration region, or at its start when
		/// the script has no include (the legacy code ignored an include on the very first line).
		/// </returns>
		public IncludePasteResult CreateInclude(String text, String headerFile, Int32 caretLine)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentException.ThrowIfNullOrEmpty(headerFile);

			var map = new TextLineMap(text);
			String newLine = TextLineMap.DetectNewLine(text);
			Int32? firstBody = this._parser.ParseProcedures(text, null)
				.Where(p => p.StartLine > 0)
				.Select(p => (Int32?)p.StartLine)
				.Min();

			LineRange region = this._locator.GetDeclarationRegion(text, firstBody ?? map.LineCount);
			Boolean hasRegion = region.IsValid;
			Int32 regionStart = hasRegion ? region.StartLine : 1;
			Int32 regionEnd = hasRegion ? region.EndLine : Math.Max(1, (firstBody ?? map.LineCount + 1) - 1);
			regionEnd = Math.Min(regionEnd, map.LineCount);

			Int32 line = regionStart;
			if (hasRegion && caretLine >= regionStart && caretLine <= regionEnd)
			{
				line = caretLine;
			}
			else
			{
				Boolean found = false;
				for (Int32 i = regionEnd; i >= 1; i--)
				{
					if (map.GetLineText(i).TrimStart().StartsWith(SslKeywords.Include, StringComparison.OrdinalIgnoreCase))
					{
						line = i + 1;
						found = true;
						break;
					}
				}

				if (!found)
				{
					// Keep a leading header comment on top: go in front of the first preprocessor directive.
					for (Int32 i = regionStart; i <= regionEnd; i++)
					{
						if (map.GetLineText(i).TrimStart().StartsWith('#'))
						{
							line = i;
							break;
						}
					}
				}
			}

			String include = SslKeywords.Include + "\"" + headerFile + "\"" + newLine;
			if (line <= map.LineCount)
			{
				return new IncludePasteResult(TextEdit.Insert(map.GetLineStart(line), include), line);
			}

			Boolean endsWithNewLine = map.LineCount > 1 && map.GetLineLength(map.LineCount) == 0;
			String appended = endsWithNewLine ? include : newLine + include;
			return new IncludePasteResult(TextEdit.Insert(text.Length, appended), endsWithNewLine ? map.LineCount : map.LineCount + 1);
		}
	}
}
