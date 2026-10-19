namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Text;

	/// <summary>
	/// Line terminator conversions (legacy <c>Utilities.NormalizeNewLine</c> and <c>ConvertToUnixPlatform</c>).
	/// </summary>
	public static class NewLineTools
	{
		/// <summary>
		/// Makes every line terminator a CRLF pair (a lone <c>\n</c> or <c>\r</c> becomes <c>\r\n</c>).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The normalized text; the same instance when nothing had to change.</returns>
		public static String NormalizeNewLine(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			StringBuilder? result = null;
			for (Int32 i = 0; i < text.Length; i++)
			{
				Char c = text[i];
				Boolean isLone = (c == '\n' && (i == 0 || text[i - 1] != '\r'))
					|| (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'));
				if (isLone && result is null)
				{
					result = new StringBuilder(text.Length + 16);
					result.Append(text, 0, i);
				}

				if (result is null)
				{
					continue;
				}

				if (isLone)
				{
					result.Append("\r\n");
				}
				else
				{
					result.Append(c);
				}
			}

			return result?.ToString() ?? text;
		}

		/// <summary>
		/// Converts every line terminator to a single <c>\n</c>.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The converted text.</returns>
		public static String ToUnixLineEndings(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
		}

		/// <summary>
		/// Converts the line terminators to the convention of the platform a file is saved for: Unix gets <c>\n</c>, any other
		/// platform keeps the text as it is (the legacy behaviour).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="isUnixPlatform">Whether the target platform is Unix.</param>
		/// <returns>The converted text.</returns>
		public static String ConvertToUnixPlatform(String text, Boolean isUnixPlatform)
		{
			return isUnixPlatform ? ToUnixLineEndings(text) : text;
		}
	}
}