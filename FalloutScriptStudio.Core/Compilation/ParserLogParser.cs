namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Processes the log produced by the script parser (<c>parser.dll</c>) into display text and structured entries.
	/// </summary>
	public sealed class ParserLogParser
	{
		private static readonly Regex _fromPattern = new Regex(@"\w+\s*([^\>?]+):\s*(\d+):", RegexOptions.CultureInvariant);

		private static readonly String[] _lineSeparators = { "\r\n", "\n" };

		/// <summary>
		/// Parses a parser log.
		/// </summary>
		/// <param name="log">The raw parser output.</param>
		/// <param name="scriptFileName">The name of the parsed script, shown in the header line.</param>
		/// <param name="includeWarnings">Whether warnings are kept (otherwise they are removed from the text and the entry list).</param>
		/// <param name="parseTime">The time shown in the header line.</param>
		/// <returns>The prepared text and the entries.</returns>
		public ParserLogResult Parse(String log, String scriptFileName, Boolean includeWarnings, DateTime parseTime)
		{
			List<BuildError> errors = [];
			List<BuildError> includeNotes = [];
			StringBuilder sb = new StringBuilder();
			sb.Append("------ Script: ").Append(scriptFileName)
				.Append(" < Parse Time: ").Append(parseTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
				.Append(" > ------\r\n");

			Boolean skipWarning = false;
			Boolean errorSection = false;
			String[] lines = log.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
			for (Int32 i = 0; i < lines.Length; i++)
			{
				if (errorSection && lines[i].TrimStart().StartsWith("from", StringComparison.Ordinal))
				{
					Match m = _fromPattern.Match(lines[i]);
					if (m.Success && Int32.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 fromLine))
					{
						includeNotes.Add(new BuildError(
							BuildErrorType.Parser,
							"Error while parsing the contents of the header file.",
							m.Groups[1].Value,
							fromLine));
					}
				}
				else if (lines[i].StartsWith("[Error]", StringComparison.Ordinal))
				{
					sb.Append("\r\n");
					skipWarning = false;
					errorSection = true;
					errors.Add(BuildLogParser.ParseEntry(lines, i, BuildErrorType.Error));
				}
				else if (lines[i].StartsWith("[Warning]", StringComparison.Ordinal))
				{
					errorSection = false;
					if (!includeWarnings)
					{
						skipWarning = true;
						continue;
					}

					errors.Add(BuildLogParser.ParseEntry(lines, i, BuildErrorType.Warning));
					sb.Append("\r\n");
				}

				if (!skipWarning)
				{
					sb.Append(lines[i]).Append("\r\n");
				}
			}

			return new ParserLogResult(sb.ToString(), errors, includeNotes);
		}
	}
}