namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// Searches the cells of the script list (the "find" box of the register script window).
	/// The cells of a row are: id, script file, description, local variables, game name.
	/// </summary>
	public static class ScriptListFinder
	{
		/// <summary>The number of searchable columns.</summary>
		public const Int32 ColumnCount = 5;

		/// <summary>
		/// Finds the next cell that contains the text, ignoring the letter case, starting after (or before) a cell and wrapping around.
		/// </summary>
		/// <param name="entries">The entries.</param>
		/// <param name="text">The text to look for; leading and trailing spaces are ignored.</param>
		/// <param name="startRow">The row of the current cell, or -1 when there is none.</param>
		/// <param name="startColumn">The column of the current cell.</param>
		/// <param name="forward">Search towards the end of the list instead of the beginning.</param>
		/// <returns>The row and column of the match, or <see langword="null"/> when nothing was found.</returns>
		public static (Int32 Row, Int32 Column)? Find(IReadOnlyList<ScriptListEntry> entries, String text, Int32 startRow, Int32 startColumn, Boolean forward)
		{
			ArgumentNullException.ThrowIfNull(entries);

			String needle = (text ?? String.Empty).Trim();
			Int32 total = entries.Count * ColumnCount;
			if (needle.Length == 0 || total == 0)
			{
				return null;
			}

			Int32 current = startRow < 0 ? (forward ? -1 : total) : (startRow * ColumnCount) + Math.Clamp(startColumn, 0, ColumnCount - 1);
			Int32 step = forward ? 1 : -1;
			for (Int32 i = 1; i <= total; i++)
			{
				Int32 index = (((current + (i * step)) % total) + total) % total;
				Int32 row = index / ColumnCount;
				Int32 column = index % ColumnCount;
				if (GetCellText(entries[row], row, column).Contains(needle, StringComparison.OrdinalIgnoreCase))
				{
					return (row, column);
				}
			}

			return null;
		}

		/// <summary>Gets the text shown in a cell.</summary>
		/// <param name="entry">The entry.</param>
		/// <param name="row">The 0-based row.</param>
		/// <param name="column">The column.</param>
		/// <returns>The cell text.</returns>
		public static String GetCellText(ScriptListEntry entry, Int32 row, Int32 column)
		{
			ArgumentNullException.ThrowIfNull(entry);

			return column switch
			{
				0 => (row + 1).ToString(CultureInfo.InvariantCulture),
				1 => entry.Script,
				2 => entry.Description,
				3 => entry.LocalVars.ToString(CultureInfo.InvariantCulture),
				_ => entry.GameName,
			};
		}
	}
}
