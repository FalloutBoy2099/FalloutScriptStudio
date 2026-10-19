namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using Formatting;

	/// <summary>
	/// The style of the code formatter, as the user edits and stores it (<see cref="EditorSettings.Formatting"/>, and
	/// <see cref="ProjectSettings.Formatting"/> for a project). The defaults reproduce the original formatter: spaces around operators and
	/// after commas, nothing else. <see cref="ToOptions"/> turns it into the immutable <see cref="FormatOptions"/> Core works with.
	/// </summary>
	public sealed class FormatSettings
	{
		/// <summary>Gets or sets what the indentation is made of.</summary>
		public IndentStyle Indentation { get; set; } = IndentStyle.FromEditor;

		/// <summary>Gets or sets the width of an indentation level in columns, or 0 for the tab size of the editor.</summary>
		public Int32 IndentSize { get; set; }

		/// <summary>Gets or sets a value indicating whether the indentation of every line is rebuilt from the <c>begin</c> / <c>end</c> structure.</summary>
		public Boolean Reindent { get; set; }

		/// <summary>Gets or sets how many levels a line that continues a statement is indented (1 to 4).</summary>
		public Int32 ContinuationIndent { get; set; } = 1;

		/// <summary>Gets or sets a value indicating whether the lines inside a bracket start under the first character after it.</summary>
		public Boolean AlignContinuationToBracket { get; set; }

		/// <summary>Gets or sets where the <c>begin</c> of a block goes.</summary>
		public BeginPlacement Begin { get; set; }

		/// <summary>Gets or sets whether <c>else</c> and <c>begin</c> share a line.</summary>
		public ElseBeginStyle ElseBegin { get; set; }

		/// <summary>Gets or sets where an <c>else</c> after the <c>end</c> of a block goes.</summary>
		public ElsePlacement Else { get; set; }

		/// <summary>Gets or sets the spaces around binary operators.</summary>
		public SpacingStyle OperatorSpacing { get; set; } = SpacingStyle.Add;

		/// <summary>Gets or sets the spaces around <c>:=</c> and the compound assignments.</summary>
		public SpacingStyle AssignmentSpacing { get; set; } = SpacingStyle.Add;

		/// <summary>Gets or sets the space after a comma.</summary>
		public SpacingStyle CommaSpacing { get; set; } = SpacingStyle.Add;

		/// <summary>Gets or sets the spaces just inside parentheses.</summary>
		public InnerSpacingStyle ParenthesisSpacing { get; set; }

		/// <summary>Gets or sets a value indicating whether the spacing rules also apply to <c>#define</c>, <c>#if</c> and <c>#elif</c> lines.</summary>
		public Boolean FormatPreprocessorLines { get; set; } = true;

		/// <summary>Gets or sets the number of blank lines between procedures (0 to 3), or -1 to leave them as they are.</summary>
		public Int32 BlankLinesBetweenProcedures { get; set; } = FormatOptions.KeepBlankLines;

		/// <summary>Gets or sets the most blank lines in a row (1 to 3), or 0 for no limit.</summary>
		public Int32 MaxConsecutiveBlankLines { get; set; }

		/// <summary>Gets or sets a value indicating whether the white space at the end of the lines is removed.</summary>
		public Boolean TrimTrailingWhitespace { get; set; }

		/// <summary>Gets or sets a value indicating whether the text ends with a line break.</summary>
		public Boolean EnsureFinalNewline { get; set; }

		/// <summary>Gets or sets a value indicating whether the values of consecutive <c>#define</c> lines start in the same column.</summary>
		public Boolean AlignDefines { get; set; }

		/// <summary>Gets or sets the line length from which the argument lists are wrapped, or 0 to never wrap.</summary>
		public Int32 MaxLineLength { get; set; }

		/// <summary>Gets or sets a value indicating whether scripts and headers are formatted when they are saved.</summary>
		public Boolean FormatOnSave { get; set; }

		/// <summary>Gets or sets the properties of the file this version does not know (a newer version wrote them); they are kept when the file is written again.</summary>
		[JsonExtensionData]
		public Dictionary<String, JsonElement>? UnknownProperties { get; set; }

		/// <summary>Creates a copy.</summary>
		/// <returns>The copy.</returns>
		public FormatSettings Clone()
		{
			FormatSettings copy = (FormatSettings)this.MemberwiseClone();
			copy.UnknownProperties = CloneUnknown(this.UnknownProperties);
			return copy;
		}

		/// <summary>Copies every option of another style into this one.</summary>
		/// <param name="other">The style to copy.</param>
		public void CopyFrom(FormatSettings other)
		{
			ArgumentNullException.ThrowIfNull(other);

			this.Indentation = other.Indentation;
			this.IndentSize = other.IndentSize;
			this.Reindent = other.Reindent;
			this.ContinuationIndent = other.ContinuationIndent;
			this.AlignContinuationToBracket = other.AlignContinuationToBracket;
			this.Begin = other.Begin;
			this.ElseBegin = other.ElseBegin;
			this.Else = other.Else;
			this.OperatorSpacing = other.OperatorSpacing;
			this.AssignmentSpacing = other.AssignmentSpacing;
			this.CommaSpacing = other.CommaSpacing;
			this.ParenthesisSpacing = other.ParenthesisSpacing;
			this.FormatPreprocessorLines = other.FormatPreprocessorLines;
			this.BlankLinesBetweenProcedures = other.BlankLinesBetweenProcedures;
			this.MaxConsecutiveBlankLines = other.MaxConsecutiveBlankLines;
			this.TrimTrailingWhitespace = other.TrimTrailingWhitespace;
			this.EnsureFinalNewline = other.EnsureFinalNewline;
			this.AlignDefines = other.AlignDefines;
			this.MaxLineLength = other.MaxLineLength;
			this.FormatOnSave = other.FormatOnSave;
			this.UnknownProperties = CloneUnknown(other.UnknownProperties);
		}

		private static Dictionary<String, JsonElement>? CloneUnknown(Dictionary<String, JsonElement>? properties)
		{
			return properties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
		}

		/// <summary>Repairs the values that are out of range or not defined.</summary>
		public void Normalize()
		{
			if (!Enum.IsDefined(this.Indentation))
			{
				this.Indentation = IndentStyle.FromEditor;
			}

			this.IndentSize = this.IndentSize <= 0 ? 0 : Math.Clamp(this.IndentSize, FormatOptions.MinIndentSize, FormatOptions.MaxIndentSize);
			this.ContinuationIndent = Math.Clamp(this.ContinuationIndent, 1, 4);
			this.BlankLinesBetweenProcedures = this.BlankLinesBetweenProcedures < 0 ? FormatOptions.KeepBlankLines : Math.Min(this.BlankLinesBetweenProcedures, FormatOptions.MaxBlankLines);
			this.MaxConsecutiveBlankLines = Math.Clamp(this.MaxConsecutiveBlankLines, 0, FormatOptions.MaxBlankLines);
			this.MaxLineLength = this.MaxLineLength <= 0 ? 0 : Math.Clamp(this.MaxLineLength, FormatOptions.MinLineLength, FormatOptions.MaxLineLengthLimit);
			this.Begin = Enum.IsDefined(this.Begin) ? this.Begin : BeginPlacement.Keep;
			this.ElseBegin = Enum.IsDefined(this.ElseBegin) ? this.ElseBegin : ElseBeginStyle.Keep;
			this.Else = Enum.IsDefined(this.Else) ? this.Else : ElsePlacement.Keep;
			this.OperatorSpacing = Enum.IsDefined(this.OperatorSpacing) ? this.OperatorSpacing : SpacingStyle.Add;
			this.AssignmentSpacing = Enum.IsDefined(this.AssignmentSpacing) ? this.AssignmentSpacing : SpacingStyle.Add;
			this.CommaSpacing = Enum.IsDefined(this.CommaSpacing) ? this.CommaSpacing : SpacingStyle.Add;
			this.ParenthesisSpacing = Enum.IsDefined(this.ParenthesisSpacing) ? this.ParenthesisSpacing : InnerSpacingStyle.Keep;
		}

		/// <summary>Creates the options the formatter works with.</summary>
		/// <param name="editor">The editor settings that give the indentation when this style leaves it to the editor.</param>
		/// <returns>The options, with every value in its range.</returns>
		public FormatOptions ToOptions(EditorSettings editor)
		{
			ArgumentNullException.ThrowIfNull(editor);

			Boolean useTabs = this.Indentation switch
			{
				IndentStyle.Tabs => true,
				IndentStyle.Spaces => false,
				_ => !editor.TabsToSpaces,
			};
			return new FormatOptions
			{
				UseTabs = useTabs,
				IndentSize = this.IndentSize > 0 ? this.IndentSize : editor.TabSize,
				Reindent = this.Reindent,
				ContinuationIndent = this.ContinuationIndent,
				AlignContinuationToBracket = this.AlignContinuationToBracket,
				Begin = this.Begin,
				ElseBegin = this.ElseBegin,
				Else = this.Else,
				OperatorSpacing = this.OperatorSpacing,
				AssignmentSpacing = this.AssignmentSpacing,
				CommaSpacing = this.CommaSpacing,
				ParenthesisSpacing = this.ParenthesisSpacing,
				FormatPreprocessorLines = this.FormatPreprocessorLines,
				BlankLinesBetweenProcedures = this.BlankLinesBetweenProcedures,
				MaxConsecutiveBlankLines = this.MaxConsecutiveBlankLines,
				TrimTrailingWhitespace = this.TrimTrailingWhitespace,
				EnsureFinalNewline = this.EnsureFinalNewline,
				AlignDefines = this.AlignDefines,
				MaxLineLength = this.MaxLineLength,
			}.Normalize();
		}
	}
}