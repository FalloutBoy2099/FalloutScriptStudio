namespace FalloutScriptStudio.Editor.Highlighting
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A complete editor color scheme: editor chrome colors plus the style of every token category
	/// declared in the <c>.xshd</c> syntax definitions.
	/// </summary>
	public sealed class SyntaxTheme
	{
		public SyntaxTheme(
			String name,
			Boolean isDark,
			String background,
			String foreground,
			String selection,
			String lineNumbers,
			String caretLine,
			String whitespaceMarker,
			String tabMarker,
			String verticalRuler,
			String foldingMarker,
			String bracketHighlight,
			String searchHighlight,
			String errorUnderline,
			String procedureName,
			IReadOnlyDictionary<String, TokenStyle> sslStyles,
			IReadOnlyDictionary<String, TokenStyle> messageStyles)
		{
			this.Name = name;
			this.IsDark = isDark;
			this.Background = background;
			this.Foreground = foreground;
			this.Selection = selection;
			this.LineNumbers = lineNumbers;
			this.CaretLine = caretLine;
			this.WhitespaceMarker = whitespaceMarker;
			this.TabMarker = tabMarker;
			this.VerticalRuler = verticalRuler;
			this.FoldingMarker = foldingMarker;
			this.BracketHighlight = bracketHighlight;
			this.SearchHighlight = searchHighlight;
			this.ErrorUnderline = errorUnderline;
			this.ProcedureName = procedureName;
			this.SslStyles = sslStyles;
			this.MessageStyles = messageStyles;
		}

		public String Name { get; }

		public Boolean IsDark { get; }

		public String Background { get; }

		public String Foreground { get; }

		public String Selection { get; }

		public String LineNumbers { get; }

		public String CaretLine { get; }

		public String WhitespaceMarker { get; }

		public String TabMarker { get; }

		public String VerticalRuler { get; }

		public String FoldingMarker { get; }

		public String BracketHighlight { get; }

		public String SearchHighlight { get; }

		public String ErrorUnderline { get; }

		/// <summary>Gets the color used for user-defined procedure names (not part of the keyword lists).</summary>
		public String ProcedureName { get; }

		public IReadOnlyDictionary<String, TokenStyle> SslStyles { get; }

		public IReadOnlyDictionary<String, TokenStyle> MessageStyles { get; }
	}
}
