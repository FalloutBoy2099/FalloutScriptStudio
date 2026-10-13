namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Windows.Media;
	using FalloutScriptStudio.Editor.Highlighting;

	/// <summary>
	/// The frozen brushes and pens of one <see cref="SyntaxTheme"/> used by the editor control and its renderers.
	/// </summary>
	public sealed class EditorPalette
	{
		private EditorPalette(SyntaxTheme theme)
		{
			this.Theme = theme;
			this.IsDark = theme.IsDark;
			this.Background = Freeze(theme.Background);
			this.Foreground = Freeze(theme.Foreground);
			this.Selection = Freeze(theme.Selection);
			this.LineNumbers = Freeze(theme.LineNumbers);
			this.CaretLine = Freeze(theme.CaretLine);
			this.WhitespaceMarker = Freeze(theme.WhitespaceMarker);
			this.TabMarker = Freeze(theme.TabMarker);
			this.VerticalRuler = new Pen(Freeze(theme.VerticalRuler), 1);
			this.VerticalRuler.Freeze();
			this.FoldingMarker = Freeze(theme.FoldingMarker);
			this.SelectedFoldingMarker = theme.IsDark ? Freeze("Gold") : this.Foreground;
			this.Bracket = Freeze(theme.BracketHighlight);
			this.BracketBorder = new Pen(Freeze(theme.Foreground, 0x70), 1);
			this.BracketBorder.Freeze();
			this.SearchBackground = Freeze(theme.SearchHighlight);
			this.SearchForeground = Freeze(theme.IsDark ? "White" : "Black");
			this.SearchMatchBackground = Freeze(theme.SearchHighlight, 0x90);
			this.ErrorUnderline = Freeze(theme.ErrorUnderline);
			this.WarningUnderline = Freeze(theme.IsDark ? "Orange" : "DarkOrange");
			this.MessageUnderline = Freeze(theme.IsDark ? "LimeGreen" : "ForestGreen");
			this.IncludeErrorUnderline = Freeze(theme.IsDark ? "Lime" : "Blue");
			this.UnusedUnderline = Freeze(theme.Foreground, 0x90);
			this.IncludeHighlight = Freeze(theme.IsDark ? "DarkSlateGray" : "Beige");
			this.ProcedureName = Freeze(theme.ProcedureName);
		}

		public SyntaxTheme Theme { get; }

		public Boolean IsDark { get; }

		public Brush Background { get; }

		public Brush Foreground { get; }

		public Brush Selection { get; }

		public Brush LineNumbers { get; }

		public Brush CaretLine { get; }

		/// <summary>Gets the color of the space markers.</summary>
		public Brush WhitespaceMarker { get; }

		/// <summary>Gets the color of the tab markers.</summary>
		public Brush TabMarker { get; }

		public Pen VerticalRuler { get; }

		public Brush FoldingMarker { get; }

		/// <summary>Gets the color of the folding marker and the fold line of the region under the pointer (legacy <c>SelectedFoldLine</c>: gold in the dark theme).</summary>
		public Brush SelectedFoldingMarker { get; }

		public Brush Bracket { get; }

		public Pen BracketBorder { get; }

		/// <summary>Gets the background of the highlighted occurrences of the selected word.</summary>
		public Brush SearchBackground { get; }

		/// <summary>Gets the text color on <see cref="SearchBackground"/>.</summary>
		public Brush SearchForeground { get; }

		/// <summary>Gets the background of the search matches.</summary>
		public Brush SearchMatchBackground { get; }

		public Brush ErrorUnderline { get; }

		public Brush WarningUnderline { get; }

		public Brush MessageUnderline { get; }

		/// <summary>Gets the faint dotted underline of a local variable that is never used.</summary>
		public Brush UnusedUnderline { get; }

		/// <summary>Gets the underline of an error that comes from an included header (legacy <c>HighlightIncludeError</c>).</summary>
		public Brush IncludeErrorUnderline { get; }

		/// <summary>Gets the background of an <c>#include</c> line that was just inserted (legacy <c>IncludeHighlight</c>).</summary>
		public Brush IncludeHighlight { get; }

		public Brush ProcedureName { get; }

		/// <summary>Creates the palette of a theme.</summary>
		/// <param name="theme">The theme.</param>
		/// <returns>The palette.</returns>
		public static EditorPalette Create(SyntaxTheme theme)
		{
			return new EditorPalette(theme);
		}

		/// <summary>Converts a theme color string (name or <c>#RRGGBB</c>) to a color.</summary>
		/// <param name="color">The color text.</param>
		/// <returns>The color.</returns>
		public static Color ParseColor(String color)
		{
			return (Color)ColorConverter.ConvertFromString(color);
		}

		/// <summary>Gets the foreground color of a token category of the SSL syntax.</summary>
		/// <param name="tokenName">The category name (an <c>.xshd</c> color name).</param>
		/// <returns>A frozen brush; the default foreground when the category has no color.</returns>
		public Brush GetTokenBrush(String tokenName)
		{
			if (this.Theme.SslStyles.TryGetValue(tokenName, out TokenStyle? style) && style.Foreground is not null)
			{
				return Freeze(style.Foreground);
			}

			return this.Foreground;
		}

		private static SolidColorBrush Freeze(String color, Byte alpha = 0xFF)
		{
			Color parsed = ParseColor(color);
			if (alpha != 0xFF)
			{
				parsed = Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B);
			}

			SolidColorBrush brush = new SolidColorBrush(parsed);
			brush.Freeze();
			return brush;
		}
	}
}
