namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Editor settings that influence the code generated for a keyword.
	/// </summary>
	public sealed record KeywordExpansionOptions
	{
		/// <summary>Gets the width of one indentation level in columns.</summary>
		public Int32 TabSize { get; init; } = 4;

		/// <summary>Gets a value indicating whether indentation uses spaces (otherwise tabs).</summary>
		public Boolean ConvertTabsToSpaces { get; init; } = true;

		/// <summary>Gets the line terminator of the generated code.</summary>
		public String NewLine { get; init; } = TextLineMap.DefaultNewLine;
	}
}
