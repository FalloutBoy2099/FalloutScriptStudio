namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Globalization;

	/// <summary>
	/// Converts the selected number between hexadecimal and decimal (legacy <c>convertHexDecToolStripMenuItem_Click</c>).
	/// </summary>
	public static class NumberBaseConverter
	{
		/// <summary>
		/// Converts a selected <c>0x...</c> number to decimal, or a selected decimal number to <c>0x...</c> (upper case digits).
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection holding the number; nothing happens for an empty span.</param>
		/// <returns>
		/// The edit, or <c>null</c> when the selection is not a 32-bit number. For a positive decimal number preceded by a
		/// minus sign the sign is included in the replaced range and the negative value is converted
		/// (<c>-5</c> becomes <c>0xFFFFFFFB</c>, and back).
		/// </returns>
		public static TextEdit? Convert(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (selection.IsEmpty)
			{
				return null;
			}

			String selected = text.Substring(selection.Offset, selection.Length);
			if (selected.Contains("0x", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					Int32 value = System.Convert.ToInt32(selected, 16);
					return new TextEdit(selection.Offset, selection.Length, value.ToString(CultureInfo.InvariantCulture));
				}
				catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
				{
					return null;
				}
			}

			if (!Int32.TryParse(selected, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 number))
			{
				return null;
			}

			Int32 offset = selection.Offset;
			Int32 length = selection.Length;
			if (number > 0 && offset > 0 && text[offset - 1] == '-')
			{
				number = -number;
				offset--;
				length++;
			}

			return new TextEdit(offset, length, "0x" + System.Convert.ToString(number, 16).ToUpperInvariant());
		}
	}
}