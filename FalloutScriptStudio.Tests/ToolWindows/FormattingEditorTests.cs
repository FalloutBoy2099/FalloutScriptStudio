namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The formatting options in the Settings window and in the project settings window, with their live preview.</summary>
	public sealed class FormattingEditorTests : SettingsViewModelTestBase
	{
		[Fact]
		public void ThePreview_IsTheSampleScriptFormattedWithTheOptionsBeingEdited()
		{
			FormattingEditorViewModel editor = new FormattingEditorViewModel(new FormatSettings(), () => new EditorSettings());
			List<String> changed = [];
			((INotifyPropertyChanged)editor).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);
			String before = editor.PreviewText;

			editor.Begin = BeginPlacement.NextLine;
			editor.Reindent = true;

			Assert.NotEqual(before, editor.PreviewText);
			Assert.Contains(nameof(FormattingEditorViewModel.PreviewText), changed);
			Assert.Contains(nameof(FormattingEditorViewModel.Begin), changed);
			Assert.Equal(new CodeFormatterAccess().Format(FormatPreviewSample.Text, editor.Settings.ToOptions(new EditorSettings())), editor.PreviewText);
			Assert.Contains("then\r\n   begin", editor.PreviewText, StringComparison.Ordinal);
		}

		[Fact]
		public void ThePreview_FollowsTheIndentationOfTheEditor()
		{
			EditorSettings editor = new EditorSettings { TabsToSpaces = true, TabSize = 2 };
			FormattingEditorViewModel viewModel = new FormattingEditorViewModel(new FormatSettings { Reindent = true }, () => editor);

			Assert.Contains("\r\n  variable i", viewModel.PreviewText, StringComparison.Ordinal);
			editor.TabsToSpaces = false;
			viewModel.Indentation = IndentStyle.FromEditor;
			viewModel.IndentSize = 5;

			Assert.Contains("\r\n\tvariable i", viewModel.PreviewText, StringComparison.Ordinal);
		}

		[Fact]
		public void EveryOptionIsEditedInPlace_AndTheValuesAreRepaired()
		{
			FormatSettings settings = new FormatSettings();
			FormattingEditorViewModel editor = new FormattingEditorViewModel(settings, () => new EditorSettings());

			editor.Indentation = IndentStyle.Tabs;
			editor.IndentSize = 99;
			editor.ContinuationIndent = 9;
			editor.AlignContinuationToBracket = true;
			editor.ElseBegin = ElseBeginStyle.Join;
			editor.Else = ElsePlacement.OwnLine;
			editor.OperatorSpacing = SpacingStyle.Remove;
			editor.AssignmentSpacing = SpacingStyle.Keep;
			editor.CommaSpacing = SpacingStyle.Remove;
			editor.ParenthesisSpacing = InnerSpacingStyle.One;
			editor.FormatPreprocessorLines = false;
			editor.BlankLinesBetweenProcedures = 2;
			editor.MaxConsecutiveBlankLines = 3;
			editor.TrimTrailingWhitespace = true;
			editor.EnsureFinalNewline = true;
			editor.AlignDefines = true;
			editor.MaxLineLength = 5;
			editor.FormatOnSave = true;

			Assert.Equal(IndentStyle.Tabs, settings.Indentation);
			Assert.Equal(FormatOptions.MaxIndentSize, settings.IndentSize);
			Assert.Equal(4, settings.ContinuationIndent);
			Assert.True(settings.AlignContinuationToBracket);
			Assert.Equal(ElseBeginStyle.Join, settings.ElseBegin);
			Assert.Equal(ElsePlacement.OwnLine, settings.Else);
			Assert.Equal(SpacingStyle.Remove, settings.OperatorSpacing);
			Assert.Equal(SpacingStyle.Keep, settings.AssignmentSpacing);
			Assert.Equal(SpacingStyle.Remove, settings.CommaSpacing);
			Assert.Equal(InnerSpacingStyle.One, settings.ParenthesisSpacing);
			Assert.False(settings.FormatPreprocessorLines);
			Assert.Equal(2, settings.BlankLinesBetweenProcedures);
			Assert.Equal(3, settings.MaxConsecutiveBlankLines);
			Assert.True(settings.TrimTrailingWhitespace);
			Assert.True(settings.EnsureFinalNewline);
			Assert.True(settings.AlignDefines);
			Assert.Equal(FormatOptions.MinLineLength, settings.MaxLineLength);
			Assert.True(settings.FormatOnSave);
		}

		[Fact]
		public void Reset_PutsEveryOptionBackToItsDefault_InPlace()
		{
			FormatSettings settings = new FormatSettings { Reindent = true, Begin = BeginPlacement.NextLine, OperatorSpacing = SpacingStyle.Remove, FormatOnSave = true, MaxLineLength = 90 };
			FormattingEditorViewModel editor = new FormattingEditorViewModel(settings, () => new EditorSettings());
			List<String> changed = [];
			((INotifyPropertyChanged)editor).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

			editor.ResetCommand.Execute(null);

			Assert.Equal(new FormatSettings().ToOptions(new EditorSettings()), settings.ToOptions(new EditorSettings()));
			Assert.False(settings.FormatOnSave);
			Assert.Contains(String.Empty, changed);
			Assert.False(editor.Reindent);
		}

		[Fact]
		public void TheChoicesCoverEveryValue()
		{
			FormattingEditorViewModel editor = new FormattingEditorViewModel(new FormatSettings(), () => new EditorSettings());

			Assert.Equal(Enum.GetValues<IndentStyle>(), editor.IndentChoices.Select(choice => choice.Value));
			Assert.Equal(Enum.GetValues<BeginPlacement>(), editor.BeginChoices.Select(choice => choice.Value));
			Assert.Equal(Enum.GetValues<ElseBeginStyle>(), editor.ElseBeginChoices.Select(choice => choice.Value));
			Assert.Equal(Enum.GetValues<ElsePlacement>(), editor.ElseChoices.Select(choice => choice.Value));
			Assert.Equal(Enum.GetValues<SpacingStyle>(), editor.SpacingChoices.Select(choice => choice.Value));
			Assert.Equal(Enum.GetValues<InnerSpacingStyle>(), editor.InnerSpacingChoices.Select(choice => choice.Value));
			Assert.Equal(new[] { -1, 0, 1, 2, 3 }, editor.ProcedureBlankChoices.Select(choice => choice.Value));
			Assert.Equal(new[] { 0, 1, 2, 3 }, editor.MaxBlankChoices.Select(choice => choice.Value));
			Assert.All(editor.IndentChoices.Select(choice => choice.Text).Concat(editor.SpacingChoices.Select(choice => choice.Text)), text => Assert.False(String.IsNullOrWhiteSpace(text)));
		}

		[Fact]
		public void TheSettingsWindow_EditsTheDraft_AndCommitCopiesTheStyleIntoTheSettings()
		{
			SettingsViewModel viewModel = this.Create();
			FormatSettings live = this._settings.Settings.Editor.Formatting;

			viewModel.FormattingEditor.Reindent = true;
			viewModel.FormattingEditor.Begin = BeginPlacement.SameLine;

			Assert.True(viewModel.Draft.Editor.Formatting.Reindent);
			Assert.False(this._settings.Settings.Editor.Formatting.Reindent);
			viewModel.Commit();

			Assert.True(this._settings.Settings.Editor.Formatting.Reindent);
			Assert.Equal(BeginPlacement.SameLine, this._settings.Settings.Editor.Formatting.Begin);
			Assert.NotSame(viewModel.Draft.Editor.Formatting, this._settings.Settings.Editor.Formatting);
			Assert.NotNull(live);
		}

		[Fact]
		public void TheDraft_StartsWithTheCurrentStyle()
		{
			this._settings.Settings.Editor.Formatting.MaxLineLength = 120;

			SettingsViewModel viewModel = this.Create();

			Assert.Equal(120, viewModel.FormattingEditor.MaxLineLength);
			Assert.NotSame(this._settings.Settings.Editor.Formatting, viewModel.FormattingEditor.Settings);
		}
	}

	/// <summary>The project settings window and the style of the formatter.</summary>
	public sealed class ProjectFormattingSettingsTests : IDisposable
	{
		private readonly TempFolder _root = new TempFolder();
		private readonly SettingsService _settings;
		private readonly ProjectService _projects;
		private readonly String _folder;

		public ProjectFormattingSettingsTests()
		{
			this._settings = new SettingsService(
				AppPaths.FromBaseDirectory(this._root.Combine("app")),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			this._settings.Load();
			this._projects = new ProjectService(this._settings, NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled);
			this._folder = this._root.Combine("mod");
			Directory.CreateDirectory(this._folder);
		}

		public void Dispose()
		{
			this._projects.Dispose();
			this._settings.Dispose();
			this._root.Dispose();
		}

		private ProjectSettingsViewModel Open(String projectFile = "{ }")
		{
			File.WriteAllText(Path.Combine(this._folder, ProjectSettings.FileName), projectFile);
			Assert.True(this._projects.Open(this._folder));
			return new ProjectSettingsViewModel(this._projects, this._settings, new FakeDialogService());
		}

		[Fact]
		public void TheProject_InheritsTheGlobalStyle_AndShowsIt()
		{
			this._settings.Settings.Editor.Formatting.Reindent = true;
			this._settings.Settings.Editor.Formatting.Begin = BeginPlacement.NextLine;

			ProjectSettingsViewModel viewModel = this.Open();

			Assert.False(viewModel.FormattingMode.IsOverridden);
			Assert.True(viewModel.FormattingEditor.Reindent);
			Assert.Equal(BeginPlacement.NextLine, viewModel.FormattingEditor.Begin);
			Assert.Null(viewModel.ToSettings().Formatting);
		}

		[Fact]
		public void OverridingTheStyle_SavesItToTheProjectFile_AndTheGlobalStyleIsUntouched()
		{
			ProjectSettingsViewModel viewModel = this.Open();

			viewModel.FormattingMode.IsOverridden = true;
			viewModel.FormattingEditor.Reindent = true;
			viewModel.FormattingEditor.MaxLineLength = 100;
			viewModel.SaveCommand.Execute(null);

			Assert.True(this._projects.Settings!.Formatting!.Reindent);
			Assert.Equal(100, this._projects.Settings.Formatting.MaxLineLength);
			Assert.Contains("formatting", File.ReadAllText(Path.Combine(this._folder, ProjectSettings.FileName)), StringComparison.Ordinal);
			Assert.False(this._settings.Settings.Editor.Formatting.Reindent);
			Assert.True(this._settings.Effective.ToFormatOptions().Reindent);
			Assert.Equal(100, this._settings.Effective.ToFormatOptions().MaxLineLength);
		}

		[Fact]
		public void GoingBackToInherit_ShowsTheGlobalStyleAgain_AndRemovesTheStyleOfTheProject()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"formatting\": { \"reindent\": true, \"begin\": \"sameLine\" } }");

			Assert.True(viewModel.FormattingMode.IsOverridden);
			Assert.True(viewModel.FormattingEditor.Reindent);
			Assert.Equal(BeginPlacement.SameLine, viewModel.FormattingEditor.Begin);

			viewModel.FormattingMode.IsOverridden = false;

			Assert.False(viewModel.FormattingEditor.Reindent);
			Assert.Null(viewModel.ToSettings().Formatting);
			viewModel.SaveCommand.Execute(null);
			Assert.Null(this._projects.Settings!.Formatting);
			Assert.DoesNotContain("formatting", File.ReadAllText(Path.Combine(this._folder, ProjectSettings.FileName)), StringComparison.Ordinal);
		}

		[Fact]
		public void TheProjectStartsFromTheGlobalStyle_WhenItBeginsToOverride()
		{
			this._settings.Settings.Editor.Formatting.Else = ElsePlacement.OwnLine;
			ProjectSettingsViewModel viewModel = this.Open();

			viewModel.FormattingMode.IsOverridden = true;

			Assert.Equal(ElsePlacement.OwnLine, viewModel.FormattingEditor.Else);
			Assert.Equal(ElsePlacement.OwnLine, viewModel.ToSettings().Formatting!.Else);
		}
	}

	/// <summary>Gives the tests the formatter without a using directive for its namespace in every file.</summary>
	internal sealed class CodeFormatterAccess
	{
		private readonly FalloutScriptStudio.Core.TextTools.CodeFormatter _formatter = new FalloutScriptStudio.Core.TextTools.CodeFormatter();

		public String Format(String text, FormatOptions options)
		{
			return this._formatter.Format(text, options);
		}
	}
}