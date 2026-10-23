namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	/// <summary>Writes the rows of a comparison as a CSV table or as plain text.</summary>
	public static class MessageComparisonReport
	{
		/// <summary>Writes the rows as CSV: a heading line, then a line per row; the fields that need it are quoted, the line breaks of a text stay inside the quotes.</summary>
		/// <param name="rows">The rows.</param>
		/// <param name="labels">The texts of the headings and of the statuses.</param>
		/// <returns>The CSV text (line breaks are <c>CR LF</c>).</returns>
		public static String ToCsv(IEnumerable<MessageComparisonRow> rows, MessageReportLabels labels)
		{
			ArgumentNullException.ThrowIfNull(rows);
			ArgumentNullException.ThrowIfNull(labels);

			StringBuilder csv = new();
			AppendCsvLine(csv, labels.Number, labels.Status, labels.Issues, labels.Reference, labels.Target);
			foreach (MessageComparisonRow row in rows)
			{
				AppendCsvLine(csv, row.Number.ToString(CultureInfo.InvariantCulture), labels.StatusName(row.Status), labels.IssueNames(row.Issues), row.ReferenceText, row.TargetText);
			}

			return csv.ToString();
		}

		/// <summary>Writes the rows as text: a line per row with the number, the status and the issues, then the two texts indented below.</summary>
		/// <param name="rows">The rows.</param>
		/// <param name="labels">The texts of the headings and of the statuses.</param>
		/// <returns>The text (line breaks are <c>CR LF</c>).</returns>
		public static String ToText(IEnumerable<MessageComparisonRow> rows, MessageReportLabels labels)
		{
			ArgumentNullException.ThrowIfNull(rows);
			ArgumentNullException.ThrowIfNull(labels);

			StringBuilder text = new();
			foreach (MessageComparisonRow row in rows)
			{
				text.Append('{').Append(row.Number.ToString(CultureInfo.InvariantCulture)).Append("} ").Append(labels.StatusName(row.Status));
				String issues = labels.IssueNames(row.Issues);
				if (issues.Length > 0)
				{
					text.Append(" (").Append(issues).Append(')');
				}

				text.Append("\r\n");
				text.Append('\t').Append(labels.Reference).Append(": ").Append(OneLine(row.ReferenceText)).Append("\r\n");
				text.Append('\t').Append(labels.Target).Append(": ").Append(OneLine(row.TargetText)).Append("\r\n");
			}

			return text.ToString();
		}

		/// <summary>Gets the message numbers of some rows as one text.</summary>
		/// <param name="rows">The rows.</param>
		/// <returns>The numbers, separated by a comma and a space, in the order of the rows.</returns>
		public static String JoinNumbers(IEnumerable<MessageComparisonRow> rows)
		{
			ArgumentNullException.ThrowIfNull(rows);

			StringBuilder numbers = new();
			foreach (MessageComparisonRow row in rows)
			{
				if (numbers.Length > 0)
				{
					numbers.Append(", ");
				}

				numbers.Append(row.Number.ToString(CultureInfo.InvariantCulture));
			}

			return numbers.ToString();
		}

		private static void AppendCsvLine(StringBuilder csv, params String[] fields)
		{
			for (Int32 i = 0; i < fields.Length; i++)
			{
				if (i > 0)
				{
					csv.Append(',');
				}

				csv.Append(Quote(fields[i]));
			}

			csv.Append("\r\n");
		}

		private static String Quote(String field)
		{
			return field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : field;
		}

		private static String OneLine(String text)
		{
			return text.Replace("\r\n", " / ", StringComparison.Ordinal).Replace("\n", " / ", StringComparison.Ordinal);
		}
	}
}