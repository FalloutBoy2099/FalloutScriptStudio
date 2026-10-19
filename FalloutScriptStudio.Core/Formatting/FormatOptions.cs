namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>
	/// The style of the code formatter, fully resolved (Core never reads the settings: the application builds it from the editor and the
	/// formatting settings). The default value reproduces the formatter of the first versions: spaces around operators and after commas,
	/// and nothing else.
	/// </summary>
	public sealed record FormatOptions
	{
		/// <summary>The smallest indentation width.</summary>
		public const Int32 MinIndentSize = 1;

		/// <summary>The largest indentation width.</summary>
		public const Int32 MaxIndentSize = 16;

		/// <summary>The value of <see cref="BlankLinesBetweenProcedures"/> that leaves the blank lines alone.</summary>
		public const Int32 KeepBlankLines = -1;

		/// <summary>The most blank lines <see cref="BlankLinesBetweenProcedures"/> and <see cref="MaxConsecutiveBlankLines"/> accept.</summary>
		public const Int32 MaxBlankLines = 3;

		/// <summary>The smallest line length that is accepted when wrapping is on.</summary>
		public const Int32 MinLineLength = 40;

		/// <summary>The largest line length that is accepted when wrapping is on.</summary>
		public const Int32 MaxLineLengthLimit = 400;

		/// <summary>Gets the options that reproduce the original formatter.</summary>
		public static FormatOptions Default { get; } = new FormatOptions();

		/// <summary>Gets a value indicating whether indentation uses tabs (otherwise spaces).</summary>
		public Boolean UseTabs { get; init; }

		/// <summary>Gets the width of one indentation level in columns (also the width of a tab).</summary>
		public Int32 IndentSize { get; init; } = 3;

		/// <summary>Gets a value indicating whether the indentation of every line is rebuilt from the <c>begin</c> / <c>end</c> structure.</summary>
		public Boolean Reindent { get; init; }

		/// <summary>Gets how many indentation levels the lines of a statement that continues on the next line are moved to the right (1 to 4).</summary>
		public Int32 ContinuationIndent { get; init; } = 1;

		/// <summary>Gets a value indicating whether the lines inside an open bracket start under the first character after it (when text follows the bracket).</summary>
		public Boolean AlignContinuationToBracket { get; init; }

		/// <summary>Gets where the <c>begin</c> of a block goes.</summary>
		public BeginPlacement Begin { get; init; }

		/// <summary>Gets whether <c>else</c> and <c>begin</c> share a line; it wins over <see cref="Begin"/> for that pair.</summary>
		public ElseBeginStyle ElseBegin { get; init; }

		/// <summary>Gets where an <c>else</c> after the <c>end</c> of a block goes.</summary>
		public ElsePlacement Else { get; init; }

		/// <summary>Gets the spaces around the binary operators (<c>+ - * / % == != &lt; &gt; &lt;= &gt;=</c>).</summary>
		public SpacingStyle OperatorSpacing { get; init; } = SpacingStyle.Add;

		/// <summary>Gets the spaces around <c>:=</c> and the compound assignments (<c>+= -= *= /= %=</c>).</summary>
		public SpacingStyle AssignmentSpacing { get; init; } = SpacingStyle.Add;

		/// <summary>Gets the space after a comma.</summary>
		public SpacingStyle CommaSpacing { get; init; } = SpacingStyle.Add;

		/// <summary>Gets the spaces just inside parentheses.</summary>
		public InnerSpacingStyle ParenthesisSpacing { get; init; }

		/// <summary>
		/// Gets a value indicating whether the spacing rules also apply to the lines of <c>#define</c>, <c>#if</c> and <c>#elif</c> (an
		/// <c>#include</c> line is never touched). When off, every preprocessor line stays as it is.
		/// </summary>
		public Boolean FormatPreprocessorLines { get; init; } = true;

		/// <summary>Gets the number of blank lines between two procedures (0 to 3), or <see cref="KeepBlankLines"/>.</summary>
		public Int32 BlankLinesBetweenProcedures { get; init; } = KeepBlankLines;

		/// <summary>Gets the most blank lines in a row (1 to 3), or 0 for no limit.</summary>
		public Int32 MaxConsecutiveBlankLines { get; init; }

		/// <summary>Gets a value indicating whether the spaces and tabs at the end of a line are removed.</summary>
		public Boolean TrimTrailingWhitespace { get; init; }

		/// <summary>Gets a value indicating whether the text ends with a line break.</summary>
		public Boolean EnsureFinalNewline { get; init; }

		/// <summary>Gets a value indicating whether the values of consecutive <c>#define</c> lines start in the same column.</summary>
		public Boolean AlignDefines { get; init; }

		/// <summary>Gets the length from which a line is wrapped at the commas of its argument lists, or 0 to never wrap.</summary>
		public Int32 MaxLineLength { get; init; }

		/// <summary>
		/// Gets a value indicating whether anything but the spacing of operators, assignments and commas is switched on (so that the formatter
		/// must understand the structure of the text, and that formatting a selection needs the whole document).
		/// </summary>
		public Boolean IsStructural =>
			this.Reindent
			|| this.Begin != BeginPlacement.Keep
			|| this.ElseBegin != ElseBeginStyle.Keep
			|| this.Else != ElsePlacement.Keep
			|| this.ParenthesisSpacing != InnerSpacingStyle.Keep
			|| this.BlankLinesBetweenProcedures != KeepBlankLines
			|| this.MaxConsecutiveBlankLines > 0
			|| this.TrimTrailingWhitespace
			|| this.EnsureFinalNewline
			|| this.AlignDefines
			|| this.MaxLineLength > 0
			|| this.OperatorSpacing == SpacingStyle.Remove
			|| this.AssignmentSpacing == SpacingStyle.Remove
			|| this.CommaSpacing == SpacingStyle.Remove;

		/// <summary>Repairs the values that are out of range.</summary>
		/// <returns>The options with every value in its range.</returns>
		public FormatOptions Normalize()
		{
			return this with
			{
				IndentSize = Math.Clamp(this.IndentSize, MinIndentSize, MaxIndentSize),
				ContinuationIndent = Math.Clamp(this.ContinuationIndent, 1, 4),
				BlankLinesBetweenProcedures = this.BlankLinesBetweenProcedures < 0 ? KeepBlankLines : Math.Min(this.BlankLinesBetweenProcedures, MaxBlankLines),
				MaxConsecutiveBlankLines = Math.Clamp(this.MaxConsecutiveBlankLines, 0, MaxBlankLines),
				MaxLineLength = this.MaxLineLength <= 0 ? 0 : Math.Clamp(this.MaxLineLength, MinLineLength, MaxLineLengthLimit),
			};
		}
	}
}