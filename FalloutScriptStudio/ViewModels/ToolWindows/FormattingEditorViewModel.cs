namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Edits one <see cref="FormatSettings"/> (the global style, or the style a project has of its own) and shows what it does: the preview
	/// is a fixed sample script formatted with the options as they are edited. The edited object is changed in place.
	/// </summary>
	public sealed partial class FormattingEditorViewModel : ObservableObject
	{
		private static readonly CodeFormatter _formatter = new();

		private readonly Func<EditorSettings> _editor;
		private readonly FormatSettings _settings;

		/// <summary>Initializes a new instance of the <see cref="FormattingEditorViewModel"/> class.</summary>
		/// <param name="settings">The style to edit.</param>
		/// <param name="editor">Supplies the editor settings that give the indentation when the style leaves it to the editor.</param>
		public FormattingEditorViewModel(FormatSettings settings, Func<EditorSettings> editor)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(editor);

			this._settings = settings;
			this._editor = editor;
			this.IndentChoices = new List<Choice<IndentStyle>>
			{
				new(IndentStyle.FromEditor, Loc.Get("Str.ToolWindows.Fmt.Choice.Indent.FromEditor")),
				new(IndentStyle.Spaces, Loc.Get("Str.ToolWindows.Fmt.Choice.Indent.Spaces")),
				new(IndentStyle.Tabs, Loc.Get("Str.ToolWindows.Fmt.Choice.Indent.Tabs")),
			};
			this.BeginChoices = new List<Choice<BeginPlacement>>
			{
				new(BeginPlacement.Keep, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(BeginPlacement.SameLine, Loc.Get("Str.ToolWindows.Fmt.Choice.Begin.SameLine")),
				new(BeginPlacement.NextLine, Loc.Get("Str.ToolWindows.Fmt.Choice.Begin.NextLine")),
			};
			this.ElseBeginChoices = new List<Choice<ElseBeginStyle>>
			{
				new(ElseBeginStyle.Keep, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(ElseBeginStyle.Join, Loc.Get("Str.ToolWindows.Fmt.Choice.ElseBegin.Join")),
				new(ElseBeginStyle.Split, Loc.Get("Str.ToolWindows.Fmt.Choice.ElseBegin.Split")),
			};
			this.ElseChoices = new List<Choice<ElsePlacement>>
			{
				new(ElsePlacement.Keep, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(ElsePlacement.AfterEnd, Loc.Get("Str.ToolWindows.Fmt.Choice.Else.AfterEnd")),
				new(ElsePlacement.OwnLine, Loc.Get("Str.ToolWindows.Fmt.Choice.Else.OwnLine")),
			};
			this.SpacingChoices = new List<Choice<SpacingStyle>>
			{
				new(SpacingStyle.Keep, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(SpacingStyle.Add, Loc.Get("Str.ToolWindows.Fmt.Choice.Spacing.Add")),
				new(SpacingStyle.Remove, Loc.Get("Str.ToolWindows.Fmt.Choice.Spacing.Remove")),
			};
			this.InnerSpacingChoices = new List<Choice<InnerSpacingStyle>>
			{
				new(InnerSpacingStyle.Keep, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(InnerSpacingStyle.None, Loc.Get("Str.ToolWindows.Fmt.Choice.Inner.None")),
				new(InnerSpacingStyle.One, Loc.Get("Str.ToolWindows.Fmt.Choice.Inner.One")),
			};
			this.ProcedureBlankChoices = new List<Choice<Int32>>
			{
				new(FormatOptions.KeepBlankLines, Loc.Get("Str.ToolWindows.Fmt.Choice.Keep")),
				new(0, "0"),
				new(1, "1"),
				new(2, "2"),
				new(3, "3"),
			};
			this.MaxBlankChoices = new List<Choice<Int32>>
			{
				new(0, Loc.Get("Str.ToolWindows.Fmt.Choice.NoLimit")),
				new(1, "1"),
				new(2, "2"),
				new(3, "3"),
			};
		}

		/// <summary>Gets the style being edited.</summary>
		public FormatSettings Settings => this._settings;

		/// <summary>Gets the choices of the indentation.</summary>
		public IReadOnlyList<Choice<IndentStyle>> IndentChoices { get; }

		/// <summary>Gets the choices for the place of <c>begin</c>.</summary>
		public IReadOnlyList<Choice<BeginPlacement>> BeginChoices { get; }

		/// <summary>Gets the choices for <c>else begin</c>.</summary>
		public IReadOnlyList<Choice<ElseBeginStyle>> ElseBeginChoices { get; }

		/// <summary>Gets the choices for the place of <c>else</c>.</summary>
		public IReadOnlyList<Choice<ElsePlacement>> ElseChoices { get; }

		/// <summary>Gets the choices for the spaces around operators and after commas.</summary>
		public IReadOnlyList<Choice<SpacingStyle>> SpacingChoices { get; }

		/// <summary>Gets the choices for the spaces inside parentheses.</summary>
		public IReadOnlyList<Choice<InnerSpacingStyle>> InnerSpacingChoices { get; }

		/// <summary>Gets the choices for the number of blank lines between procedures.</summary>
		public IReadOnlyList<Choice<Int32>> ProcedureBlankChoices { get; }

		/// <summary>Gets the choices for the most blank lines in a row.</summary>
		public IReadOnlyList<Choice<Int32>> MaxBlankChoices { get; }

		/// <summary>Gets the largest width of an indentation level.</summary>
		public Double MaxIndentSize => FormatOptions.MaxIndentSize;

		/// <summary>Gets the longest line length.</summary>
		public Double MaxLineLengthLimit => FormatOptions.MaxLineLengthLimit;

		/// <summary>Gets the sample script formatted with the options being edited.</summary>
		public String PreviewText => _formatter.Format(FormatPreviewSample.Text, this._settings.ToOptions(this._editor()));

		/// <summary>Gets or sets what the indentation is made of.</summary>
		public IndentStyle Indentation
		{
			get => this._settings.Indentation;
			set => this.Change(this._settings.Indentation, value, v => this._settings.Indentation = v);
		}

		/// <summary>Gets or sets the width of an indentation level (0 follows the editor).</summary>
		public Double? IndentSize
		{
			get => this._settings.IndentSize;
			set => this.ChangeNumber(this._settings.IndentSize, value, 0, FormatOptions.MaxIndentSize, v => this._settings.IndentSize = v);
		}

		/// <summary>Gets or sets a value indicating whether the indentation of every line is rebuilt.</summary>
		public Boolean Reindent
		{
			get => this._settings.Reindent;
			set => this.Change(this._settings.Reindent, value, v => this._settings.Reindent = v);
		}

		/// <summary>Gets or sets the levels a continued line is indented.</summary>
		public Double? ContinuationIndent
		{
			get => this._settings.ContinuationIndent;
			set => this.ChangeNumber(this._settings.ContinuationIndent, value, 1, 4, v => this._settings.ContinuationIndent = v);
		}

		/// <summary>Gets or sets a value indicating whether the continued lines align with the bracket.</summary>
		public Boolean AlignContinuationToBracket
		{
			get => this._settings.AlignContinuationToBracket;
			set => this.Change(this._settings.AlignContinuationToBracket, value, v => this._settings.AlignContinuationToBracket = v);
		}

		/// <summary>Gets or sets where <c>begin</c> goes.</summary>
		public BeginPlacement Begin
		{
			get => this._settings.Begin;
			set => this.Change(this._settings.Begin, value, v => this._settings.Begin = v);
		}

		/// <summary>Gets or sets whether <c>else</c> and <c>begin</c> share a line.</summary>
		public ElseBeginStyle ElseBegin
		{
			get => this._settings.ElseBegin;
			set => this.Change(this._settings.ElseBegin, value, v => this._settings.ElseBegin = v);
		}

		/// <summary>Gets or sets where <c>else</c> goes.</summary>
		public ElsePlacement Else
		{
			get => this._settings.Else;
			set => this.Change(this._settings.Else, value, v => this._settings.Else = v);
		}

		/// <summary>Gets or sets the spaces around operators.</summary>
		public SpacingStyle OperatorSpacing
		{
			get => this._settings.OperatorSpacing;
			set => this.Change(this._settings.OperatorSpacing, value, v => this._settings.OperatorSpacing = v);
		}

		/// <summary>Gets or sets the spaces around assignments.</summary>
		public SpacingStyle AssignmentSpacing
		{
			get => this._settings.AssignmentSpacing;
			set => this.Change(this._settings.AssignmentSpacing, value, v => this._settings.AssignmentSpacing = v);
		}

		/// <summary>Gets or sets the space after commas.</summary>
		public SpacingStyle CommaSpacing
		{
			get => this._settings.CommaSpacing;
			set => this.Change(this._settings.CommaSpacing, value, v => this._settings.CommaSpacing = v);
		}

		/// <summary>Gets or sets the spaces inside parentheses.</summary>
		public InnerSpacingStyle ParenthesisSpacing
		{
			get => this._settings.ParenthesisSpacing;
			set => this.Change(this._settings.ParenthesisSpacing, value, v => this._settings.ParenthesisSpacing = v);
		}

		/// <summary>Gets or sets a value indicating whether the spacing rules apply to preprocessor lines.</summary>
		public Boolean FormatPreprocessorLines
		{
			get => this._settings.FormatPreprocessorLines;
			set => this.Change(this._settings.FormatPreprocessorLines, value, v => this._settings.FormatPreprocessorLines = v);
		}

		/// <summary>Gets or sets the number of blank lines between procedures.</summary>
		public Int32 BlankLinesBetweenProcedures
		{
			get => this._settings.BlankLinesBetweenProcedures;
			set => this.Change(this._settings.BlankLinesBetweenProcedures, value, v => this._settings.BlankLinesBetweenProcedures = v);
		}

		/// <summary>Gets or sets the most blank lines in a row.</summary>
		public Int32 MaxConsecutiveBlankLines
		{
			get => this._settings.MaxConsecutiveBlankLines;
			set => this.Change(this._settings.MaxConsecutiveBlankLines, value, v => this._settings.MaxConsecutiveBlankLines = v);
		}

		/// <summary>Gets or sets a value indicating whether trailing white space is removed.</summary>
		public Boolean TrimTrailingWhitespace
		{
			get => this._settings.TrimTrailingWhitespace;
			set => this.Change(this._settings.TrimTrailingWhitespace, value, v => this._settings.TrimTrailingWhitespace = v);
		}

		/// <summary>Gets or sets a value indicating whether the text ends with a line break.</summary>
		public Boolean EnsureFinalNewline
		{
			get => this._settings.EnsureFinalNewline;
			set => this.Change(this._settings.EnsureFinalNewline, value, v => this._settings.EnsureFinalNewline = v);
		}

		/// <summary>Gets or sets a value indicating whether the values of consecutive defines are aligned.</summary>
		public Boolean AlignDefines
		{
			get => this._settings.AlignDefines;
			set => this.Change(this._settings.AlignDefines, value, v => this._settings.AlignDefines = v);
		}

		/// <summary>Gets or sets the line length from which argument lists are wrapped (0 never wraps).</summary>
		public Double? MaxLineLength
		{
			get => this._settings.MaxLineLength;
			set
			{
				if (value.HasValue && !Double.IsNaN(value.Value))
				{
					Int32 length = (Int32)Math.Round(value.Value);
					Int32 repaired = length <= 0 ? 0 : Math.Clamp(length, FormatOptions.MinLineLength, FormatOptions.MaxLineLengthLimit);
					this.Change(this._settings.MaxLineLength, repaired, v => this._settings.MaxLineLength = v);
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether scripts and headers are formatted when they are saved.</summary>
		public Boolean FormatOnSave
		{
			get => this._settings.FormatOnSave;
			set => this.Change(this._settings.FormatOnSave, value, v => this._settings.FormatOnSave = v);
		}

		/// <summary>Copies the options of another style into the one being edited (a project that begins to override starts from the global style).</summary>
		/// <param name="settings">The style to copy.</param>
		public void Assign(FormatSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			this._settings.CopyFrom(settings);
			this.OnPropertyChanged(String.Empty);
		}

		/// <summary>Puts the options back to the defaults: the spaces around operators and after commas, nothing else.</summary>
		[RelayCommand]
		private void Reset()
		{
			this.Assign(new FormatSettings());
		}

		private static Boolean TryRound(Double? value, out Int32 result)
		{
			result = 0;
			if (!value.HasValue || Double.IsNaN(value.Value))
			{
				return false;
			}

			result = (Int32)Math.Round(value.Value);
			return true;
		}

		private void ChangeNumber(Int32 current, Double? value, Int32 minimum, Int32 maximum, Action<Int32> set, [CallerMemberName] String? name = null)
		{
			if (TryRound(value, out Int32 number))
			{
				this.Change(current, Math.Clamp(number, minimum, maximum), set, name);
			}
		}

		private void Change<T>(T current, T value, Action<T> set, [CallerMemberName] String? name = null)
		{
			if (EqualityComparer<T>.Default.Equals(current, value))
			{
				return;
			}

			set(value);
			this.OnPropertyChanged(name);
			this.OnPropertyChanged(nameof(this.PreviewText));
		}
	}
}