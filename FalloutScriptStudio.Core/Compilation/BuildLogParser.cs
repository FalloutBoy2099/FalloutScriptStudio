namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Turns the text output of sslc, mcpp and wcc into a list of <see cref="BuildError"/>.
	/// </summary>
	public sealed class BuildLogParser
	{
		private static readonly Regex _entryPattern = new Regex(
			@"(\[\w+\])?\s*\<?([^\>?]+)\>?\s*\:(\-?\d+):?(\-?\d+|\s\w+)?\:\s*(.*)",
			RegexOptions.CultureInvariant);

		private static readonly String[] _lineSeparators = { "\r\n", "\n" };

		/// <summary>
		/// Extracts the errors, warnings and messages from the compiler output.
		/// </summary>
		/// <param name="output">The full output of the build.</param>
		/// <param name="sourceFile">The full path of the compiled script (used to resolve relative and preprocessed file names).</param>
		/// <param name="preprocessor">The preprocessor that was in use.</param>
		/// <returns>The entries in the order they appear in the output.</returns>
		public IReadOnlyList<BuildError> Parse(String output, String sourceFile, PreprocessorKind preprocessor)
		{
			List<BuildError> errors = [];
			if (String.IsNullOrEmpty(output))
			{
				return errors;
			}

			// Empty lines are kept: sslc ends every warning with one, and it tells the end of the entry from a continuation line.
			String[] log = output.Split(_lineSeparators, StringSplitOptions.None);
			for (Int32 s = 0; s < log.Length; s++)
			{
				String line = log[s];
				if (!IsEntry(line))
				{
					continue;
				}

				Boolean warning = line.IndexOf(": warning:", StringComparison.Ordinal) > 0;

				BuildErrorType type = BuildErrorType.Message;
				if (warning || line[1] == 'W')
				{
					type = BuildErrorType.Warning;
				}
				else if (line[1] == 'E')
				{
					type = BuildErrorType.Error;
				}

				BuildError error = ParseEntry(log, s, type);
				error = CorrectFilePath(error, sourceFile, preprocessor);
				errors.Add(error);
			}

			return errors;
		}

		/// <summary>
		/// Parses one <c>[Tag] &lt;file&gt;:line:column: text</c> entry. Lines that do not match the pattern become entries without a file.
		/// </summary>
		internal static BuildError ParseEntry(String[] log, Int32 index, BuildErrorType type)
		{
			Match m = _entryPattern.Match(log[index]);
			String fileName = BuildError.NoFile;
			Int32 lineNumber = 0;
			Int32 column = -1;
			String message;

			if (m.Success && Int32.TryParse(m.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 parsedLine))
			{
				fileName = m.Groups[2].Value.Replace('/', Path.DirectorySeparatorChar);
				lineNumber = parsedLine;
				String columnText = m.Groups[4].Value;
				if (columnText.Length > 0
					&& !Char.IsWhiteSpace(columnText[0])
					&& Int32.TryParse(columnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 parsedColumn))
				{
					column = parsedColumn;
				}

				message = m.Groups[5].Value.TrimEnd();
			}
			else
			{
				message = log[index].TrimEnd();
			}

			if (type == BuildErrorType.Warning && IsContinuation(log, index + 1))
			{
				message += ": " + log[index + 1].Trim();
			}

			return new BuildError(type, message, fileName, lineNumber, column);
		}

		private static Boolean IsEntry(String line)
		{
			return line.IndexOf(": warning:", StringComparison.Ordinal) > 0
				|| line.StartsWith("[Error]", StringComparison.Ordinal)
				|| line.StartsWith("[Warning]", StringComparison.Ordinal)
				|| line.StartsWith("[Message]", StringComparison.Ordinal);
		}

		/// <summary>
		/// Tells whether a line continues the warning above it. mcpp and wcc follow a warning with a line that quotes the source; sslc
		/// follows its warnings with an empty line, and the next line is another entry that must not be glued to the warning.
		/// </summary>
		private static Boolean IsContinuation(String[] log, Int32 index)
		{
			return index < log.Length && log[index].Trim().Length > 0 && !IsEntry(log[index]);
		}

		private static BuildError CorrectFilePath(BuildError error, String sourceFile, PreprocessorKind preprocessor)
		{
			String fileName = error.FileName;
			if (fileName == BuildError.NoFile)
			{
				return error;
			}

			if (preprocessor != PreprocessorKind.Builtin)
			{
				String scriptName = Path.GetFileName(sourceFile);
				if (fileName.IndexOf(scriptName, StringComparison.Ordinal) > 0)
				{
					fileName = sourceFile;
				}
			}

			if (!Path.IsPathRooted(fileName))
			{
				String directory = Path.GetDirectoryName(sourceFile) ?? String.Empty;
				fileName = Path.GetFullPath(Path.Combine(directory, fileName));
			}

			return error with { FileName = fileName };
		}
	}
}