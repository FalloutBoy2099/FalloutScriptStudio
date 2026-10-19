namespace FalloutScriptStudio.Core.Tests.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.Tests.Native;

	/// <summary>The option sets and the sample texts the formatter tests run over.</summary>
	internal static class FormatSamples
	{
		/// <summary>Reads and writes the samples byte for byte (every byte is one char).</summary>
		public static readonly Encoding Bytes = Encoding.Latin1;

		/// <summary>The names of the option sets, for the theories.</summary>
		public static IEnumerable<Object[]> SetNames()
		{
			return Sets.Keys.Select(name => new Object[] { name });
		}

		/// <summary>Several option sets that exercise every option, by name.</summary>
		public static readonly IReadOnlyDictionary<String, FormatOptions> Sets = new Dictionary<String, FormatOptions>(StringComparer.Ordinal)
		{
			["default"] = FormatOptions.Default,
			["reindent"] = new FormatOptions { Reindent = true },
			["reindent-tabs"] = new FormatOptions { Reindent = true, UseTabs = true, IndentSize = 4, ContinuationIndent = 2 },
			["allman"] = new FormatOptions { Reindent = true, Begin = BeginPlacement.NextLine, ElseBegin = ElseBeginStyle.Split, Else = ElsePlacement.OwnLine, TrimTrailingWhitespace = true },
			["k&r"] = new FormatOptions { Reindent = true, Begin = BeginPlacement.SameLine, ElseBegin = ElseBeginStyle.Join, Else = ElsePlacement.AfterEnd, BlankLinesBetweenProcedures = 1, MaxConsecutiveBlankLines = 1, EnsureFinalNewline = true },
			["compact"] = new FormatOptions { OperatorSpacing = SpacingStyle.Remove, AssignmentSpacing = SpacingStyle.Remove, CommaSpacing = SpacingStyle.Remove, ParenthesisSpacing = InnerSpacingStyle.None, Reindent = true },
			["spacious"] = new FormatOptions { ParenthesisSpacing = InnerSpacingStyle.One, BlankLinesBetweenProcedures = 3, AlignDefines = true, Reindent = true, IndentSize = 2 },
			["wrap"] = new FormatOptions { Reindent = true, MaxLineLength = 60, AlignContinuationToBracket = true },
			["wrap-tabs"] = new FormatOptions { Reindent = true, MaxLineLength = 70, UseTabs = true, IndentSize = 4 },
			["keep-spacing"] = new FormatOptions { OperatorSpacing = SpacingStyle.Keep, AssignmentSpacing = SpacingStyle.Keep, CommaSpacing = SpacingStyle.Keep, Begin = BeginPlacement.NextLine, FormatPreprocessorLines = false },
			["everything"] = new FormatOptions
			{
				Reindent = true,
				UseTabs = true,
				IndentSize = 3,
				Begin = BeginPlacement.NextLine,
				ElseBegin = ElseBeginStyle.Join,
				Else = ElsePlacement.OwnLine,
				OperatorSpacing = SpacingStyle.Add,
				ParenthesisSpacing = InnerSpacingStyle.One,
				BlankLinesBetweenProcedures = 2,
				MaxConsecutiveBlankLines = 2,
				TrimTrailingWhitespace = true,
				EnsureFinalNewline = true,
				AlignDefines = true,
				MaxLineLength = 80,
			},
		};

		/// <summary>Gets the scripts and headers of the FO2tweaksMk2 sample, as text.</summary>
		public static IReadOnlyList<(String Name, String Text)> Fo2Tweaks()
		{
			List<(String, String)> files = [];
			if (!Directory.Exists(SslcTools.Fo2TweaksSources))
			{
				return files;
			}

			foreach (String path in Directory.EnumerateFiles(SslcTools.Fo2TweaksSources, "*.*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
			{
				String extension = Path.GetExtension(path);
				if (extension is ".ssl" or ".h")
				{
					files.Add((Path.GetRelativePath(SslcTools.Fo2TweaksSources, path), File.ReadAllText(path, Bytes)));
				}
			}

			return files;
		}

		/// <summary>
		/// Describes a text by its tokens, without the white space: formatting must never add, remove, reorder or merge a token. Comments are
		/// compared without their trailing white space and preprocessor lines without any white space.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The description.</returns>
		public static String Signature(String text)
		{
			StringBuilder signature = new StringBuilder(text.Length);
			foreach (FormatToken token in FormatLexer.Lex(text))
			{
				switch (token.Kind)
				{
					case FormatTokenKind.Space:
					case FormatTokenKind.Newline:
						break;
					case FormatTokenKind.LineComment:
					case FormatTokenKind.BlockComment:
						signature.Append(token.GetText(text).TrimEnd()).Append('\u0001');
						break;
					case FormatTokenKind.Directive:
						signature.Append(String.Concat(token.GetText(text).Where(c => !Char.IsWhiteSpace(c)))).Append('\u0001');
						break;
					default:
						signature.Append(token.GetText(text)).Append('\u0001');
						break;
				}
			}

			return signature.ToString();
		}

		/// <summary>Messes a script up in a way that cannot change its meaning: indentation removed, blank lines and spaces added, <c>begin</c> moved.</summary>
		/// <param name="text">The text.</param>
		/// <param name="seed">The seed of the random choices.</param>
		/// <returns>The mutated text.</returns>
		public static String Mutate(String text, Int32 seed)
		{
			Random random = new Random(seed);
			String[] lines = text.Split('\n');
			StringBuilder builder = new StringBuilder(text.Length + 1024);
			Boolean inBlock = false;
			for (Int32 i = 0; i < lines.Length; i++)
			{
				String line = lines[i];
				Boolean verbatim = inBlock || line.TrimStart().StartsWith('#') || line.Contains("/*", StringComparison.Ordinal) || line.Contains("*/", StringComparison.Ordinal);
				if (line.Contains("/*", StringComparison.Ordinal) && !line.Contains("*/", StringComparison.Ordinal))
				{
					inBlock = true;
				}
				else if (line.Contains("*/", StringComparison.Ordinal))
				{
					inBlock = false;
				}

				if (!verbatim)
				{
					Int32 roll = random.Next(8);
					String content = line.TrimStart();
					if (roll == 0)
					{
						line = content;
					}
					else if (roll == 1)
					{
						line = "\t\t" + content;
					}
					else if (roll == 2 && content.Length > 0)
					{
						line = new String(' ', random.Next(12)) + content;
					}
					else if (roll == 3 && content.Length > 0)
					{
						line += "   ";
					}
					else if (roll == 4)
					{
						builder.Append("\r\n");
					}
					else if (roll == 5 && line.EndsWith(" begin\r", StringComparison.Ordinal) && !line.Contains("//", StringComparison.Ordinal))
					{
						line = line.Substring(0, line.Length - " begin\r".Length) + "\r\nbegin\r";
					}
				}

				builder.Append(line);
				if (i < lines.Length - 1)
				{
					builder.Append('\n');
				}
			}

			return builder.ToString();
		}
	}
}