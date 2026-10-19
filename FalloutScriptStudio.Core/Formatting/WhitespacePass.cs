namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Text;

	/// <summary>Removes the white space at the end of the lines and adds the final line break.</summary>
	internal static class WhitespacePass
	{
		/// <summary>Removes the spaces and tabs at the end of the lines. The inside of a comment that goes on in the next line and a line that ends with a backslash are left alone.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The text without trailing white space.</returns>
		public static String TrimTrailing(String text)
		{
			FormatModel model = FormatModel.Build(text);
			StringBuilder builder = new StringBuilder(text.Length);
			Boolean changed = false;
			foreach (FormatLine line in model.Lines)
			{
				Int32 end = line.End;
				if (!line.Verbatim && !line.StartsMultiLineToken)
				{
					Int32 trimmed = end;
					while (trimmed > line.Start && (text[trimmed - 1] == ' ' || text[trimmed - 1] == '\t'))
					{
						trimmed--;
					}

					if (trimmed < end && !(trimmed > line.Start && text[trimmed - 1] == '\\'))
					{
						end = trimmed;
						changed = true;
					}
				}

				builder.Append(text, line.Start, end - line.Start);
				builder.Append(text, line.End, line.EolLength);
			}

			return changed ? builder.ToString() : text;
		}

		/// <summary>Adds a line break at the end of a text that has none.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The text ending with a line break.</returns>
		public static String EnsureFinalNewline(String text)
		{
			if (text.Length == 0 || text[text.Length - 1] == '\n' || text[text.Length - 1] == '\r')
			{
				return text;
			}

			return text + FormatModel.Build(text).NewLine;
		}
	}
}