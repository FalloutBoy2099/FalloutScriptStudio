namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>
	/// Finds the <c>#include</c> directives of a source text, without resolving them. Comments and string literals are skipped, the nesting of
	/// <c>#if</c> blocks is tracked, and a directive that does not name its file literally is reported so that the caller can refuse to
	/// trust the dependencies of the file. The class is pure.
	/// </summary>
	public static class IncludeScanner
	{
		/// <summary>Scans a source text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The directives in order of appearance.</returns>
		public static IReadOnlyList<IncludeDirective> Scan(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			List<IncludeDirective> directives = [];
			Boolean inBlockComment = false;
			Int32 conditionalDepth = 0;
			Int32 lineNumber = 0;
			Int32 position = 0;
			StringBuilder code = new StringBuilder();
			while (position <= text.Length)
			{
				Int32 end = text.IndexOf('\n', position);
				if (end < 0)
				{
					end = text.Length;
				}

				lineNumber++;
				ReadOnlySpan<Char> line = text.AsSpan(position, end - position);
				position = end + 1;
				if (!inBlockComment && line.IndexOf('#') < 0 && line.IndexOf("/*", StringComparison.Ordinal) < 0)
				{
					continue;
				}

				StripComments(line, ref inBlockComment, code);
				ReadOnlySpan<Char> trimmed = code.ToString().AsSpan().Trim();
				if (trimmed.Length == 0 || trimmed[0] != '#')
				{
					continue;
				}

				ReadOnlySpan<Char> directive = trimmed[1..].TrimStart();
				Int32 wordEnd = 0;
				while (wordEnd < directive.Length && Char.IsLetter(directive[wordEnd]))
				{
					wordEnd++;
				}

				ReadOnlySpan<Char> word = directive[..wordEnd];
				if (word.Equals("if", StringComparison.Ordinal) || word.Equals("ifdef", StringComparison.Ordinal) || word.Equals("ifndef", StringComparison.Ordinal))
				{
					conditionalDepth++;
				}
				else if (word.Equals("endif", StringComparison.Ordinal))
				{
					conditionalDepth = Math.Max(0, conditionalDepth - 1);
				}
				else if (word.Equals("include", StringComparison.Ordinal))
				{
					directives.Add(new IncludeDirective(ReadPath(directive[wordEnd..].Trim()), lineNumber, conditionalDepth > 0));
				}
			}

			return directives;
		}

		private static String? ReadPath(ReadOnlySpan<Char> rest)
		{
			if (rest.Length < 2)
			{
				return null;
			}

			Char close = rest[0] == '"' ? '"' : rest[0] == '<' ? '>' : '\0';
			if (close == '\0')
			{
				return null;
			}

			Int32 end = rest[1..].IndexOf(close);
			if (end <= 0)
			{
				return null;
			}

			String path = rest.Slice(1, end).ToString().Trim();
			return path.Length == 0 || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ? null : path;
		}

		/// <summary>Copies the code of a line into <paramref name="code"/>: comments are removed, string literals are kept.</summary>
		private static void StripComments(ReadOnlySpan<Char> line, ref Boolean inBlockComment, StringBuilder code)
		{
			code.Clear();
			Boolean inString = false;
			for (Int32 index = 0; index < line.Length; index++)
			{
				Char c = line[index];
				Char next = index + 1 < line.Length ? line[index + 1] : '\0';
				if (inBlockComment)
				{
					if (c == '*' && next == '/')
					{
						inBlockComment = false;
						index++;
						code.Append(' ');
					}

					continue;
				}

				if (inString)
				{
					code.Append(c);
					if (c == '"')
					{
						inString = false;
					}

					continue;
				}

				if (c == '/' && next == '/')
				{
					return;
				}

				if (c == '/' && next == '*')
				{
					inBlockComment = true;
					index++;
					continue;
				}

				if (c == '"')
				{
					inString = true;
				}

				code.Append(c);
			}
		}
	}
}