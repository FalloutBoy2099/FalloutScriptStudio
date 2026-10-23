namespace FalloutScriptStudio.Core.Help
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Parsing;
	using TextTools;

	/// <summary>
	/// The help of what the project itself declares: a procedure shows the comment block above it, a macro shows its documentation comment
	/// (<c>/** ... **/</c>) or the comment above its <c>#define</c> and its definition, a variable shows its declaration.
	/// </summary>
	public static class ProjectHelp
	{
		private const Int32 MaxDefinitionLength = 400;

		/// <summary>Creates the topic of a macro.</summary>
		/// <param name="macro">The macro.</param>
		/// <param name="comment">The comment above the <c>#define</c> (see <see cref="ReadLeadingComment"/>), or <see langword="null"/>.</param>
		/// <returns>The topic.</returns>
		public static HelpTopic FromMacro(ScriptMacro macro, String? comment = null)
		{
			ArgumentNullException.ThrowIfNull(macro);

			List<SignatureSegment> segments = [];
			String definition = macro.DefinitionName;
			Int32 open = definition.IndexOf('(', StringComparison.Ordinal);
			if (open < 0)
			{
				segments.Add(new SignatureSegment(macro.Token, SignatureSegmentKind.Name));
			}
			else
			{
				segments.Add(new SignatureSegment(definition.Substring(0, open), SignatureSegmentKind.Name));
				segments.Add(new SignatureSegment("(", SignatureSegmentKind.Punctuation));
				String list = definition.Substring(open + 1).TrimEnd(')', ' ');
				Boolean first = true;
				foreach (String parameter in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					if (!first)
					{
						segments.Add(new SignatureSegment(", ", SignatureSegmentKind.Punctuation));
					}

					first = false;
					segments.Add(new SignatureSegment(parameter, parameter == "..." ? SignatureSegmentKind.Variadic : SignatureSegmentKind.ParameterName));
				}

				segments.Add(new SignatureSegment(")", SignatureSegmentKind.Punctuation));
			}

			String code = macro.Code.Trim();
			if (code.Length > MaxDefinitionLength)
			{
				code = code.Substring(0, MaxDefinitionLength) + "...";
			}

			String? text = !String.IsNullOrWhiteSpace(macro.Description) ? macro.Description.Trim() : comment;
			return new HelpTopic(macro.Token, HelpTopicKind.Macro, segments)
			{
				Description = SplitParagraphs(text),
				Summary = FirstLine(text),
				Definition = "#define " + definition + (code.Length > 0 ? " " + code : String.Empty),
				File = macro.DeclaredFile.Length > 0 ? macro.DeclaredFile : null,
				Line = macro.DeclaredLine,
			};
		}

		/// <summary>Creates the topic of a procedure.</summary>
		/// <param name="procedure">The procedure.</param>
		/// <param name="comment">The comment block above the procedure (see <see cref="ReadLeadingComment"/>), or <see langword="null"/>.</param>
		/// <returns>The topic.</returns>
		public static HelpTopic FromProcedure(ScriptProcedure procedure, String? comment = null)
		{
			ArgumentNullException.ThrowIfNull(procedure);

			List<SignatureSegment> segments =
			[
				new SignatureSegment("procedure", SignatureSegmentKind.ReturnType),
				new SignatureSegment(" ", SignatureSegmentKind.Punctuation),
				new SignatureSegment(procedure.Name, SignatureSegmentKind.Name),
			];
			if (procedure.ArgumentCount > 0)
			{
				segments.Add(new SignatureSegment("(", SignatureSegmentKind.Punctuation));
				for (Int32 i = 0; i < procedure.ArgumentCount; i++)
				{
					if (i > 0)
					{
						segments.Add(new SignatureSegment(", ", SignatureSegmentKind.Punctuation));
					}

					segments.Add(new SignatureSegment(i < procedure.Variables.Count ? procedure.Variables[i].Name : "x", SignatureSegmentKind.ParameterName));
				}

				segments.Add(new SignatureSegment(")", SignatureSegmentKind.Punctuation));
			}

			return new HelpTopic(procedure.Name, HelpTopicKind.Procedure, segments)
			{
				Description = SplitParagraphs(comment),
				Summary = FirstLine(comment),
				File = procedure.StartLine > 0 ? procedure.StartFile : procedure.DeclaredFile,
				Line = procedure.StartLine > 0 ? procedure.StartLine : procedure.DeclaredLine,
			};
		}

		/// <summary>Creates the topic of a variable.</summary>
		/// <param name="variable">The variable.</param>
		/// <returns>The topic.</returns>
		public static HelpTopic FromVariable(ScriptVariable variable)
		{
			ArgumentNullException.ThrowIfNull(variable);

			return new HelpTopic(
				variable.Name,
				HelpTopicKind.Variable,
				[
					new SignatureSegment("variable", SignatureSegmentKind.ReturnType),
					new SignatureSegment(" ", SignatureSegmentKind.Punctuation),
					new SignatureSegment(variable.Name, SignatureSegmentKind.Name),
				])
			{
				Definition = variable.InitialValue is null ? null : variable.Name + " := " + variable.InitialValue,
				File = variable.DeclaredFile,
				Line = variable.DeclaredLine,
			};
		}

		/// <summary>Reads the comment that sits directly above a line (no blank line between): the <c>//</c> lines and the <c>/* ... */</c> blocks, without their markers.</summary>
		/// <param name="text">The text of the file.</param>
		/// <param name="line">The 1-based line of the <c>procedure</c> or <c>#define</c>.</param>
		/// <returns>The comment, one line per line of the file, or <see langword="null"/> when there is none.</returns>
		public static String? ReadLeadingComment(String text, Int32 line)
		{
			ArgumentNullException.ThrowIfNull(text);

			TextLineMap map = new(text);
			if (line < 2 || line > map.LineCount)
			{
				return null;
			}

			Int32 first = ProcedureComments.FindLeadingCommentStartLine(map, line);
			if (first >= line)
			{
				return null;
			}

			List<String> lines = [];
			for (Int32 i = first; i < line; i++)
			{
				String trimmed = map.GetLineText(i).Trim();
				if (trimmed.StartsWith("//", StringComparison.Ordinal))
				{
					trimmed = trimmed.TrimStart('/').Trim();
				}
				else
				{
					if (trimmed.StartsWith("/*", StringComparison.Ordinal))
					{
						trimmed = trimmed.Substring(2).TrimStart('*');
					}
					else if (trimmed.StartsWith('*'))
					{
						trimmed = trimmed.TrimStart('*');
					}

					if (trimmed.EndsWith("*/", StringComparison.Ordinal))
					{
						trimmed = trimmed.Substring(0, trimmed.Length - 2).TrimEnd('*');
					}

					trimmed = trimmed.Trim();
				}

				lines.Add(trimmed);
			}

			while (lines.Count > 0 && lines[0].Length == 0)
			{
				lines.RemoveAt(0);
			}

			while (lines.Count > 0 && lines[^1].Length == 0)
			{
				lines.RemoveAt(lines.Count - 1);
			}

			return lines.Count == 0 ? null : String.Join('\n', lines);
		}

		private static IReadOnlyList<String> SplitParagraphs(String? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return Array.Empty<String>();
			}

			List<String> paragraphs = [];
			StringBuilder current = new();
			foreach (String raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
			{
				String line = raw.Trim();
				if (line.Length == 0)
				{
					if (current.Length > 0)
					{
						paragraphs.Add(current.ToString());
						current.Clear();
					}

					continue;
				}

				if (current.Length > 0)
				{
					current.Append('\n');
				}

				current.Append(line);
			}

			if (current.Length > 0)
			{
				paragraphs.Add(current.ToString());
			}

			return paragraphs;
		}

		private static String FirstLine(String? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return String.Empty;
			}

			String trimmed = text.TrimStart();
			Int32 end = trimmed.IndexOf('\n', StringComparison.Ordinal);
			return (end < 0 ? trimmed : trimmed.Substring(0, end)).Trim();
		}
	}
}