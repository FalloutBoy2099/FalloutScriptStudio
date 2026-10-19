namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Text;

	/// <summary>Rebuilds the leading white space of the lines from the block structure of the script.</summary>
	internal static class IndentPass
	{
		/// <summary>Applies the indentation.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The re-indented text.</returns>
		public static String Apply(String text, FormatOptions options)
		{
			FormatModel model = FormatModel.Build(text);
			LayoutInfo info = LayoutAnalyzer.Analyze(model, options);
			StringBuilder builder = new StringBuilder(text.Length + 256);
			Boolean changed = false;
			foreach (FormatLine line in model.Lines)
			{
				String? indent = info.Indents[line.Index];
				Int32 contentStart = line.Start;
				if (indent is not null && !line.Verbatim)
				{
					while (contentStart < line.End && (text[contentStart] == ' ' || text[contentStart] == '\t'))
					{
						contentStart++;
					}

					if (contentStart < line.End)
					{
						Boolean same = contentStart - line.Start == indent.Length
							&& String.CompareOrdinal(text, line.Start, indent, 0, indent.Length) == 0;
						if (!same)
						{
							builder.Append(indent);
							builder.Append(text, contentStart, line.Next - contentStart);
							changed = true;
							continue;
						}
					}
				}

				builder.Append(text, line.Start, line.Next - line.Start);
			}

			return changed ? builder.ToString() : text;
		}
	}
}