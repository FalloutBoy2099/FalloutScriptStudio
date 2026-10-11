namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Media;

	/// <summary>
	/// Typefaces and the factory for <see cref="FormattedText"/> instances used by the diagram.
	/// </summary>
	internal sealed class DiagramTextStyles
	{
		private readonly Double _pixelsPerDip;

		public DiagramTextStyles(FontFamily family, Double pixelsPerDip)
		{
			this._pixelsPerDip = pixelsPerDip > 0.0 ? pixelsPerDip : 1.0;
			this.Title = new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
			this.Body = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			this.Mono = new Typeface(new FontFamily("Cascadia Mono, Consolas, Courier New"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			this.Italic = new Typeface(family, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
		}

		public Typeface Title { get; }

		public Typeface Body { get; }

		public Typeface Mono { get; }

		public Typeface Italic { get; }

		/// <summary>
		/// Replaces line breaks and tabs, which the single-line rows cannot display, with spaces.
		/// </summary>
		public static String Normalize(String? text)
		{
			if (String.IsNullOrEmpty(text))
			{
				return String.Empty;
			}

			return text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
		}

		/// <summary>
		/// Creates a formatted text block.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="face">Typeface.</param>
		/// <param name="size">Font size in device independent pixels.</param>
		/// <param name="brush">Foreground brush.</param>
		/// <param name="maxWidth">Maximum width; zero or less means unconstrained.</param>
		/// <param name="maxLines">Maximum number of lines; extra text is replaced with an ellipsis.</param>
		/// <param name="alignment">Horizontal alignment inside <paramref name="maxWidth"/>.</param>
		public FormattedText Create(String text, Typeface face, Double size, Brush brush, Double maxWidth = 0.0, Int32 maxLines = 1, TextAlignment alignment = TextAlignment.Left)
		{
			FormattedText formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, size, brush, this._pixelsPerDip)
			{
				Trimming = TextTrimming.CharacterEllipsis,
				MaxLineCount = Math.Max(1, maxLines),
				TextAlignment = alignment
			};
			if (maxWidth > 0.0)
			{
				formatted.MaxTextWidth = maxWidth;
			}

			return formatted;
		}
	}
}
