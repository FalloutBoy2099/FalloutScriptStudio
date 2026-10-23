namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	/// <summary>Writes the table of the uses of the global variables as CSV (RFC 4180: comma separated, quoted when needed, CRLF).</summary>
	public static class GlobalVariableCsv
	{
		/// <summary>The columns of the table.</summary>
		public static IReadOnlyList<String> Columns { get; } = ["Kind", "Variable", "Number", "Macros", "Access", "Function", "Type", "Value", "File", "Line", "Procedure"];

		/// <summary>Formats the uses of some variables.</summary>
		/// <param name="variables">The variables.</param>
		/// <returns>The CSV text, a header row and one row per use.</returns>
		public static String Format(IEnumerable<GlobalVariable> variables)
		{
			ArgumentNullException.ThrowIfNull(variables);

			StringBuilder text = new();
			AppendRow(text, Columns);
			foreach (GlobalVariable variable in variables)
			{
				String name = variable.Aliases.Count > 0 ? variable.Aliases[0] : variable.Key;
				String number = variable.Id?.ToString(CultureInfo.InvariantCulture) ?? String.Empty;
				String macros = String.Join(" ", variable.Aliases);
				foreach (GlobalVariableUse use in variable.Uses)
				{
					AppendRow(text, [
						variable.Kind.ToString(),
						name,
						number,
						macros,
						use.Access.ToString(),
						use.Function,
						use.ValueType == GlobalValueType.Unknown ? String.Empty : use.ValueType.ToString(),
						use.Literal ?? String.Empty,
						use.File,
						use.Line.ToString(CultureInfo.InvariantCulture),
						use.Procedure ?? String.Empty,
					]);
				}
			}

			return text.ToString();
		}

		private static void AppendRow(StringBuilder text, IReadOnlyList<String> cells)
		{
			for (Int32 i = 0; i < cells.Count; i++)
			{
				if (i > 0)
				{
					text.Append(',');
				}

				AppendCell(text, cells[i]);
			}

			text.Append("\r\n");
		}

		private static void AppendCell(StringBuilder text, String cell)
		{
			if (cell.AsSpan().IndexOfAny(",\"\r\n") < 0)
			{
				text.Append(cell);
				return;
			}

			text.Append('"').Append(cell.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
		}
	}
}