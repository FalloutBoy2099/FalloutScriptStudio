namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>
	/// Collects <c>#define</c> macros from script text and header files (legacy <c>GetMacros</c>).
	/// </summary>
	/// <remarks>
	/// Multi-line macros (lines ending with a backslash) are supported, documentation comments of the form
	/// <c>/** ... **/</c> placed directly above a macro become its description, and <c>#include</c> files are followed
	/// recursively; every file is read at most once per collection, which protects against circular includes.
	/// </remarks>
	public sealed class MacroCollector
	{
		private const String DiagnosticSource = "Macros";
		private const Int32 DefineLength = 8;
		private const Int32 MaxMacroLines = 42;

		private static readonly SearchValues<Char> _spaceOrLineBreak = SearchValues.Create(" \n");

		private readonly IncludeResolver _resolver;

		/// <summary>
		/// Initializes a new instance of the <see cref="MacroCollector"/> class.
		/// </summary>
		/// <param name="resolver">Resolver used to locate <c>#include</c> files.</param>
		public MacroCollector(IncludeResolver resolver)
		{
			this._resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
		}

		/// <summary>
		/// Lists the header files (<c>*.h</c>) below a directory.
		/// </summary>
		/// <param name="directory">The headers directory.</param>
		/// <param name="diagnostics">Receives a message when the directory is missing; may be <c>null</c>.</param>
		/// <returns>The files, or <c>null</c> when the directory is unset or does not exist.</returns>
		public static IReadOnlyList<String>? FindHeaderFiles(String? directory, ParseDiagnostics? diagnostics = null)
		{
			if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
			{
				diagnostics?.Add(ParseDiagnosticSeverity.Info, DiagnosticSource, -1, "Directory of headers not found: '" + (directory ?? "<unset>") + "'");
				return null;
			}

			try
			{
				return Directory.GetFiles(directory, "*.h", SearchOption.AllDirectories);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				diagnostics?.Add(ParseDiagnosticSeverity.Warning, DiagnosticSource, -1, "Cannot enumerate headers in '" + directory + "': " + ex.Message);
				return null;
			}
		}

		/// <summary>
		/// Collects the macros of a script text into a new dictionary.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="filePath">Full path of the script, or <c>null</c> for an unsaved script.</param>
		/// <param name="followIncludes">Whether to follow <c>#include</c> directives.</param>
		/// <param name="diagnostics">Receives informational messages (missing includes); may be <c>null</c>.</param>
		/// <returns>The macros keyed by token (case-sensitive, ordinal order).</returns>
		public SortedDictionary<String, ScriptMacro> CollectFromText(String text, String? filePath, Boolean followIncludes = true, ParseDiagnostics? diagnostics = null)
		{
			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			this.CollectFromText(text, filePath, macros, followIncludes, diagnostics);
			return macros;
		}

		/// <summary>
		/// Collects the macros of a script text into an existing dictionary; later definitions replace earlier ones.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="filePath">Full path of the script, or <c>null</c> for an unsaved script.</param>
		/// <param name="target">The dictionary that receives the macros.</param>
		/// <param name="followIncludes">Whether to follow <c>#include</c> directives.</param>
		/// <param name="diagnostics">Receives informational messages (missing includes); may be <c>null</c>.</param>
		public void CollectFromText(String text, String? filePath, IDictionary<String, ScriptMacro> target, Boolean followIncludes = true, ParseDiagnostics? diagnostics = null)
		{
			var visited = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			String? directory = null;
			if (!String.IsNullOrEmpty(filePath))
			{
				visited.Add(NormalizePath(filePath));
				directory = Path.GetDirectoryName(filePath);
			}

			this.CollectLines(SslSyntax.SplitLines(text), filePath ?? String.Empty, directory, target, followIncludes, visited, diagnostics);
		}

		/// <summary>
		/// Collects the macros of a file into an existing dictionary.
		/// </summary>
		/// <param name="filePath">Path of the file.</param>
		/// <param name="target">The dictionary that receives the macros.</param>
		/// <param name="followIncludes">Whether to follow <c>#include</c> directives.</param>
		/// <param name="diagnostics">Receives informational messages (missing files); may be <c>null</c>.</param>
		public void CollectFromFile(String filePath, IDictionary<String, ScriptMacro> target, Boolean followIncludes = true, ParseDiagnostics? diagnostics = null)
		{
			var visited = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			this.CollectFile(filePath, target, followIncludes, visited, diagnostics);
		}

		private static String NormalizePath(String path)
		{
			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}

		private static String? ReadDescription(String[] lines, Int32 defineIndex)
		{
			var description = new List<String>();
			Int32 depth = 0;
			for (Int32 j = defineIndex - 1; j >= 0; j--)
			{
				if (depth <= 0 && lines[j].Length == 0)
				{
					break;
				}

				String line = lines[j];
				Int32 index = line.LastIndexOf(" **/", StringComparison.Ordinal);
				if (depth == 0 && index == -1)
				{
					break;
				}

				if (index != -1)
				{
					depth++;
					line = line.Remove(index);
				}

				index = line.IndexOf("/** ", StringComparison.Ordinal);
				if (index != -1)
				{
					depth--;
					if (depth < 0)
					{
						break;
					}

					line = line.Substring(index + 4);
				}

				description.Add(line.Trim());
			}

			if (description.Count == 0)
			{
				return null;
			}

			description.Reverse();
			var sb = new StringBuilder();
			foreach (String item in description)
			{
				sb.Append(item).Append('\n');
			}

			return sb.ToString();
		}

		private static void AddMacro(String line, IDictionary<String, ScriptMacro> macros, String file, Int32 lineIndex, String? description)
		{
			line = line.TrimStart();
			Int32 firstSpace = line.AsSpan().IndexOfAny(_spaceOrLineBreak);
			if (firstSpace == -1)
			{
				return;
			}

			String token;
			String definitionName;
			String code;
			Int32 firstBracket = line.IndexOf('(');
			if (firstBracket != -1 && firstBracket < firstSpace)
			{
				Int32 closeBracket = line.IndexOf(')');
				if (closeBracket < 0 || line.Length == closeBracket + 1)
				{
					return;
				}

				definitionName = line.Remove(closeBracket + 1);
				token = line.Remove(firstBracket);
				code = FormatCode(line.Substring(closeBracket + 1), definitionName.Length);
			}
			else
			{
				definitionName = line.Remove(firstSpace);
				token = definitionName;
				code = FormatCode(line.Substring(firstSpace), definitionName.Length);
			}

			macros[token] = new ScriptMacro(token, definitionName, code, file, lineIndex + 1, description);
		}

		private static String FormatCode(String code, Int32 definitionLength)
		{
			String[] lines = code.Split('\n', MaxMacroLines);
			if (lines.Length <= 1)
			{
				return code.TrimStart();
			}

			Int32 indent = -1;
			lines[0] = lines[0].TrimEnd();
			if (lines[0].Length > 1)
			{
				indent = DefineLength + definitionLength + lines[0].Length - lines[0].TrimStart().Length;
			}

			for (Int32 i = 1; i < lines.Length; i++)
			{
				lines[i] = lines[i].TrimEnd();
				if (indent == -1 && lines[i].Length > 1)
				{
					indent = lines[i].Length - lines[i].TrimStart().Length;
				}
				else if (indent == -1 || lines[i].Length == 0)
				{
					continue;
				}

				Int32 adjust = lines[i].Length - lines[i].TrimStart().Length;
				if (adjust > indent)
				{
					adjust = indent;
				}
				else if (i == 1)
				{
					indent = adjust;
				}

				lines[i] = lines[i].Remove(0, adjust);
				if (i > 40)
				{
					lines[i] = "...continue macro...";
					break;
				}
			}

			return String.Join("\n", lines).TrimStart();
		}

		private void CollectFile(String filePath, IDictionary<String, ScriptMacro> target, Boolean followIncludes, HashSet<String> visited, ParseDiagnostics? diagnostics)
		{
			if (!visited.Add(NormalizePath(filePath)))
			{
				return;
			}

			if (!File.Exists(filePath))
			{
				diagnostics?.Add(ParseDiagnosticSeverity.Info, DiagnosticSource, -1, "File not found: '" + filePath + "'");
				return;
			}

			String text;
			try
			{
				text = TextFileReader.ReadAllText(filePath);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				diagnostics?.Add(ParseDiagnosticSeverity.Warning, DiagnosticSource, -1, "Cannot read '" + filePath + "': " + ex.Message);
				return;
			}

			this.CollectLines(SslSyntax.SplitLines(text), filePath, Path.GetDirectoryName(filePath), target, followIncludes, visited, diagnostics);
		}

		private void CollectLines(String[] lines, String file, String? directory, IDictionary<String, ScriptMacro> target, Boolean followIncludes, HashSet<String> visited, ParseDiagnostics? diagnostics)
		{
			Boolean inBlockComment = false;
			for (Int32 i = 0; i < lines.Length; i++)
			{
				if (lines[i].Length < 2)
				{
					continue;
				}

				lines[i] = lines[i].Replace('\t', ' ').Trim();
				if (lines[i].StartsWith(SslKeywords.LineComment, StringComparison.Ordinal))
				{
					continue;
				}

				if (!inBlockComment)
				{
					if (lines[i].StartsWith(SslKeywords.BlockCommentStart, StringComparison.Ordinal))
					{
						if (lines[i].LastIndexOf(SslKeywords.BlockCommentEnd, StringComparison.Ordinal) == -1)
						{
							inBlockComment = true;
						}

						continue;
					}
				}
				else
				{
					Int32 close = lines[i].IndexOf(SslKeywords.BlockCommentEnd, StringComparison.Ordinal);
					if (close == -1)
					{
						continue;
					}

					inBlockComment = false;
					close += 2;
					if (close == lines[i].Length)
					{
						continue;
					}

					lines[i] = lines[i].Remove(0, close).TrimStart();
				}

				if (lines[i].Length <= DefineLength)
				{
					continue;
				}

				if (followIncludes && lines[i].StartsWith(SslKeywords.Include, StringComparison.Ordinal))
				{
					String? includePath = IncludeResolver.ExtractIncludePath(lines[i]);
					if (includePath is not null)
					{
						this.CollectFile(this._resolver.Resolve(includePath, directory), target, true, visited, diagnostics);
					}
				}
				else if (lines[i].StartsWith(SslKeywords.Define, StringComparison.Ordinal))
				{
					String? description = ReadDescription(lines, i);
					Int32 startIndex = i;
					String definition = lines[i].Substring(DefineLength);
					if (definition.EndsWith('\\'))
					{
						var sb = new StringBuilder();
						String current = definition;
						while (current.EndsWith('\\'))
						{
							sb.Append(current.Remove(current.Length - 1).TrimEnd()).Append('\n');
							i++;
							if (i >= lines.Length)
							{
								current = String.Empty;
								break;
							}

							current = lines[i].TrimEnd();
						}

						sb.Append(current);
						definition = sb.ToString();
					}

					AddMacro(definition, target, file, startIndex, description);
				}
			}
		}
	}
}
