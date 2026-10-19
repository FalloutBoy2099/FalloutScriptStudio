namespace FalloutScriptStudio.Editor.Highlighting
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The built-in light and dark editor color schemes (ported from the legacy <c>ssl_SyntaxRules.xshd</c>
	/// and <c>ssld_SyntaxRules.xshd</c> files).
	/// </summary>
	public static class SyntaxThemes
	{
		public static SyntaxTheme Light { get; } = new SyntaxTheme(
			name: "Light",
			isDark: false,
			background: "#FFFFFF",
			foreground: "#1E1E1E",
			selection: "#B4C3D7",
			lineNumbers: "#808080",
			caretLine: "#F5F5FF",
			whitespaceMarker: "LightGray",
			tabMarker: "DarkOrange",
			verticalRuler: "#40A0A0A0",
			foldingMarker: "#808080",
			bracketHighlight: "#FFD7A0",
			searchHighlight: "GreenYellow",
			errorUnderline: "Red",
			procedureName: "Indigo",
			sslStyles: new Dictionary<String, TokenStyle>
			{
				["Number"] = new TokenStyle("Red"),
				["PreprocessorDirectives"] = new TokenStyle("DarkRed"),
				["IncludePath"] = new TokenStyle("DarkGreen"),
				["LineComment"] = new TokenStyle("DimGray", Italic: true),
				["BlockComment"] = new TokenStyle("DarkSlateGray", Italic: true),
				["String"] = new TokenStyle("#500000", "#FFFFDA"),
				["StringEscape"] = new TokenStyle("#500000", "#FFFFDA", Bold: true),
				["CodeBraces"] = new TokenStyle("Black", "LightGray"),
				["DefineName"] = new TokenStyle("#202020", Bold: true),
				["StandardProcedures"] = new TokenStyle("Indigo", Bold: true),
				["Punctuation"] = new TokenStyle("DarkBlue"),
				["SpecSymbol"] = new TokenStyle("Green"),
				["CoreKeywords"] = new TokenStyle("MediumBlue", Bold: true),
				["FalloutBaseFunctions"] = new TokenStyle("#750075", Bold: true),
				["InterfaceFunctions"] = new TokenStyle("IndianRed", Bold: true),
				["Special"] = new TokenStyle("DimGray", Bold: true),
				["FalloutBaseMacros"] = new TokenStyle("#AA00AA", Bold: true),
				["FalloutExtendMacros"] = new TokenStyle("#8000FF", Bold: true),
				["SfallUnsafeFunctions"] = new TokenStyle("#E04500", Bold: true),
				["SfallFunctions"] = new TokenStyle("SaddleBrown", Bold: true),
				["SfallMacros"] = new TokenStyle("SaddleBrown", Bold: true, Italic: true),
				["OperatorKeywords"] = new TokenStyle("MediumBlue", Bold: true),
				["Constants"] = new TokenStyle("IndianRed", Bold: true, Italic: true),
				["DefineConstants"] = new TokenStyle("#0080FF", Bold: true),
				["ErrorWords"] = new TokenStyle("Red", Bold: true),
				["WarningWords"] = new TokenStyle("#E0E000", Bold: true),
				["UserMacros"] = new TokenStyle("Green", Bold: true),
				["SpecMacros"] = new TokenStyle("White", "DimGray", Bold: true),
			},
			messageStyles: new Dictionary<String, TokenStyle>
			{
				["Number"] = new TokenStyle("DarkBlue", Bold: true),
				["LineComment"] = new TokenStyle("DarkGreen"),
				["TrailingComment"] = new TokenStyle("Silver"),
				["Emotion"] = new TokenStyle("DeepPink"),
				["Braces"] = new TokenStyle("DarkGray"),
			});

		public static SyntaxTheme Dark { get; } = new SyntaxTheme(
			name: "Dark",
			isDark: true,
			background: "#26262A",
			foreground: "#E6E6F0",
			selection: "#4F4F5F",
			lineNumbers: "Gray",
			caretLine: "#303035",
			whitespaceMarker: "DimGray",
			tabMarker: "SlateGray",
			verticalRuler: "#40A0A0A0",
			foldingMarker: "DimGray",
			bracketHighlight: "#5A4A20",
			searchHighlight: "MediumVioletRed",
			errorUnderline: "Red",
			procedureName: "Bisque",
			sslStyles: new Dictionary<String, TokenStyle>
			{
				["Number"] = new TokenStyle("#FF6A70"),
				["PreprocessorDirectives"] = new TokenStyle("LightCoral"),
				["IncludePath"] = new TokenStyle("PaleGreen"),
				["LineComment"] = new TokenStyle("#7A8A9A", Italic: true),
				["BlockComment"] = new TokenStyle("#7AAA7A", Italic: true),
				["String"] = new TokenStyle("PaleGreen"),
				["StringEscape"] = new TokenStyle("PaleGreen", Bold: true),
				["CodeBraces"] = new TokenStyle("Silver", "#40405A"),
				["DefineName"] = new TokenStyle("WhiteSmoke", Bold: true),
				["StandardProcedures"] = new TokenStyle("Bisque", Bold: true),
				["Punctuation"] = new TokenStyle("DarkGray"),
				["SpecSymbol"] = new TokenStyle("Khaki"),
				["CoreKeywords"] = new TokenStyle("#6A9ADA", Bold: true),
				["FalloutBaseFunctions"] = new TokenStyle("Orange", Bold: true),
				["InterfaceFunctions"] = new TokenStyle("IndianRed", Bold: true),
				["Special"] = new TokenStyle("#7A8A9A", Bold: true),
				["FalloutBaseMacros"] = new TokenStyle("Gold", Bold: true),
				["FalloutExtendMacros"] = new TokenStyle("#AF80FF", Bold: true),
				["SfallUnsafeFunctions"] = new TokenStyle("Tomato", Bold: true),
				["SfallFunctions"] = new TokenStyle("LightSalmon", Bold: true),
				["SfallMacros"] = new TokenStyle("LightSalmon", Bold: true, Italic: true),
				["OperatorKeywords"] = new TokenStyle("#5090F0", Bold: true),
				["Constants"] = new TokenStyle("IndianRed", Bold: true, Italic: true),
				["DefineConstants"] = new TokenStyle("#8AE0E0", Bold: true),
				["ErrorWords"] = new TokenStyle("Red", Bold: true),
				["WarningWords"] = new TokenStyle("#E0E000", Bold: true),
				["UserMacros"] = new TokenStyle("LightGreen", Bold: true),
				["SpecMacros"] = new TokenStyle("White", "DimGray", Bold: true),
			},
			messageStyles: new Dictionary<String, TokenStyle>
			{
				["Number"] = new TokenStyle("Khaki"),
				["LineComment"] = new TokenStyle("LimeGreen"),
				["TrailingComment"] = new TokenStyle("#6A7080"),
				["Emotion"] = new TokenStyle("DeepPink"),
				["Braces"] = new TokenStyle("DimGray"),
			});

		/// <summary>Gets the theme matching the requested brightness.</summary>
		public static SyntaxTheme Get(Boolean dark)
		{
			return dark ? Dark : Light;
		}
	}
}