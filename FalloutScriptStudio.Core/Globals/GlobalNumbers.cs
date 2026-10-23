namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Globalization;

	/// <summary>Reads the number literals of a script (decimal and hexadecimal integers, floating point numbers).</summary>
	internal static class GlobalNumbers
	{
		/// <summary>Reads an integer literal.</summary>
		/// <param name="text">The literal, optionally with a leading minus sign: <c>27</c>, <c>-1</c>, <c>0x1B</c>.</param>
		/// <param name="value">Receives the value.</param>
		/// <returns><see langword="true"/> for an integer literal.</returns>
		internal static Boolean TryParseInteger(String text, out Int64 value)
		{
			value = 0;
			ReadOnlySpan<Char> span = text.AsSpan().Trim();
			Boolean negative = false;
			if (span.Length > 0 && span[0] == '-')
			{
				negative = true;
				span = span.Slice(1).TrimStart();
			}

			if (span.Length == 0)
			{
				return false;
			}

			Boolean parsed;
			Int64 number;
			if (span.Length > 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
			{
				parsed = Int64.TryParse(span.Slice(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out number);
			}
			else
			{
				parsed = Int64.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out number);
			}

			if (!parsed)
			{
				return false;
			}

			value = negative ? -number : number;
			return true;
		}

		/// <summary>Determines whether a literal is a floating point number (it has a decimal point and is not hexadecimal).</summary>
		/// <param name="text">The literal.</param>
		/// <returns><see langword="true"/> for <c>1.5</c>.</returns>
		internal static Boolean IsFloat(String text)
		{
			return text.Contains('.', StringComparison.Ordinal) && !text.Contains("0x", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Determines whether a text looks like a number literal (digits, letters for hexadecimal, a decimal point).</summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> when the text starts with a digit and holds only letters, digits, underscores and points.</returns>
		internal static Boolean LooksLikeNumber(ReadOnlySpan<Char> text)
		{
			if (text.Length == 0 || text[0] < '0' || text[0] > '9')
			{
				return false;
			}

			foreach (Char c in text)
			{
				if (!(Char.IsLetterOrDigit(c) || c == '.' || c == '_'))
				{
					return false;
				}
			}

			return true;
		}
	}
}