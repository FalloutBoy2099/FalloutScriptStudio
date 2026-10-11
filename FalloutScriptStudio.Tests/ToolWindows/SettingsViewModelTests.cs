namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class SettingsViewModelTests
	{
		private readonly FakeSettingsService _settings = new FakeSettingsService(@"C:\fss");
		private readonly FakeThemeService _theme = new FakeThemeService();
		private readonly FakeDialogService _dialogs = new FakeDialogService();

		private SettingsViewModel Create(params String[] fonts)
		{
			return new SettingsViewModel(this._settings, this._theme, this._dialogs, new FileAssociationService(), new FakeFontCatalog(fonts));
		}

		[Fact]
		public void Draft_IsACopyOfTheSettings()
		{
			this._settings.Settings.Compiler.Optimization = OptimizationLevel.Full;
			SettingsViewModel viewModel = this.Create();

			Assert.NotSame(this._settings.Settings, viewModel.Draft);
			Assert.Equal(OptimizationLevel.Full, viewModel.Draft.Compiler.Optimization);

			viewModel.Draft.Compiler.Optimization = OptimizationLevel.None;
			Assert.Equal(OptimizationLevel.Full, this._settings.Settings.Compiler.Optimization);
		}

		[Fact]
		public void FontChoices_StartWithTheDefaultFontFollowedByTheBundledOnes()
		{
			SettingsViewModel viewModel = this.Create("Consolas", "Ubuntu Mono");

			Assert.Equal(new[] { "Courier New", "Consolas", "Ubuntu Mono" }, viewModel.FontChoices.Select(choice => choice.Text).ToArray());
			Assert.Equal(new[] { 0, 1, 2 }, viewModel.FontChoices.Select(choice => choice.Value).ToArray());
		}

		[Fact]
		public void FontIndex_BeyondTheAvailableFontsFallsBackToTheDefault()
		{
			this._settings.Settings.Editor.FontIndex = 5;

			SettingsViewModel viewModel = this.Create("Consolas");

			Assert.Equal(0, viewModel.Draft.Editor.FontIndex);
		}

		[Fact]
		public void Pages_SelectionControlsWhichPageIsVisible()
		{
			SettingsViewModel viewModel = this.Create();
			Assert.True(viewModel.IsAppearancePage);
			List<String> changed = new List<String>();
			((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

			viewModel.SelectedPageItem = viewModel.Pages.Single(page => page.Page == SettingsPage.Compiler);

			Assert.True(viewModel.IsCompilerPage);
			Assert.False(viewModel.IsAppearancePage);
			Assert.Contains(nameof(SettingsViewModel.IsCompilerPage), changed);
			Assert.Contains(nameof(SettingsViewModel.IsAppearancePage), changed);
			Assert.Equal(Enum.GetValues<SettingsPage>().Length, viewModel.Pages.Count);
		}

		[Fact]
		public void EnableParser_ControlsTheParserWarningsOption()
		{
			SettingsViewModel viewModel = this.Create();
			List<String> changed = new List<String>();
			((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

			viewModel.EnableParser = false;

			Assert.False(viewModel.IsParserWarningsEnabled);
			Assert.False(viewModel.Draft.Editor.EnableParser);
			Assert.Contains(nameof(SettingsViewModel.IsParserWarningsEnabled), changed);
		}

		[Fact]
		public void UserCommand_DisablesThePreprocessorChoice()
		{
			SettingsViewModel viewModel = this.Create();
			Assert.True(viewModel.IsPreprocessorEnabled);

			viewModel.UseUserCommand = true;

			Assert.False(viewModel.IsPreprocessorEnabled);
			Assert.True(viewModel.Draft.Compiler.UseUserCommand);
		}

		[Fact]
		public void TabSize_IsLimitedToTheAllowedRange()
		{
			SettingsViewModel viewModel = this.Create();

			viewModel.TabSize = 100;
			Assert.Equal(EditorSettings.MaxTabSize, viewModel.Draft.Editor.TabSize);

			viewModel.TabSize = -4;
			Assert.Equal(EditorSettings.MinTabSize, viewModel.Draft.Editor.TabSize);

			viewModel.TabSize = 4.4;
			Assert.Equal(4, viewModel.Draft.Editor.TabSize);

			viewModel.TabSize = null;
			Assert.Equal(4, viewModel.Draft.Editor.TabSize);
		}

		[Fact]
		public void BrowseHeadersDirectory_SuggestsScriptsHeaderWhenUnset()
		{
			SettingsViewModel viewModel = this.Create();
			this._dialogs.FolderAnswers.Enqueue(@"C:\game\headers");

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);

			Assert.Equal(@"C:\game\headers", viewModel.HeadersDirectory);
			Assert.Equal(Path.Combine(@"C:\game\headers", "SCRIPTS.H"), viewModel.ScriptsHeaderFile);
		}

		[Fact]
		public void BrowseHeadersDirectory_KeepsAnExistingScriptsHeader()
		{
			this._settings.Settings.Paths.ScriptsHeaderFile = @"D:\other\scripts.h";
			SettingsViewModel viewModel = this.Create();
			this._dialogs.FolderAnswers.Enqueue(@"C:\game\headers");

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);

			Assert.Equal(@"D:\other\scripts.h", viewModel.ScriptsHeaderFile);
		}

		[Fact]
		public void BrowseOutputDirectory_CancelledPickerChangesNothing()
		{
			this._settings.Settings.Paths.OutputDirectory = @"C:\out";
			SettingsViewModel viewModel = this.Create();
			this._dialogs.FolderAnswers.Enqueue(null);

			viewModel.BrowseOutputDirectoryCommand.Execute(null);

			Assert.Equal(@"C:\out", viewModel.OutputDirectory);
		}

		[Fact]
		public void BrowseScriptsHeaderFile_SetsTheChosenFile()
		{
			SettingsViewModel viewModel = this.Create();
			this._dialogs.FileAnswers.Enqueue(@"C:\h\scripts.h");

			viewModel.BrowseScriptsHeaderFileCommand.Execute(null);
			Assert.Equal(@"C:\h\scripts.h", viewModel.ScriptsHeaderFile);

			viewModel.ClearScriptsHeaderFileCommand.Execute(null);
			Assert.Equal(String.Empty, viewModel.ScriptsHeaderFile);
		}

		[Fact]
		public void Decompiler_TheBuiltInOneIsTheDefault_AndTheExternalOptionsApplyOnlyToTheExternalOne()
		{
			SettingsViewModel viewModel = this.Create();
			List<String> changed = new List<String>();
			((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

			Assert.Equal(DecompilerKind.BuiltIn, viewModel.Decompiler);
			Assert.False(viewModel.IsExternalDecompilerEnabled);
			Assert.Equal(new[] { DecompilerKind.BuiltIn, DecompilerKind.ExternalInt2Ssl }, viewModel.DecompilerChoices.Select(choice => choice.Value).ToArray());

			viewModel.Decompiler = DecompilerKind.ExternalInt2Ssl;

			Assert.True(viewModel.IsExternalDecompilerEnabled);
			Assert.Contains(nameof(SettingsViewModel.IsExternalDecompilerEnabled), changed);
			Assert.Equal(DecompilerKind.ExternalInt2Ssl, viewModel.Draft.Compiler.Decompiler);
		}

		[Fact]
		public void BrowseExternalDecompiler_SetsTheChosenProgram_AndClearRemovesIt()
		{
			SettingsViewModel viewModel = this.Create();
			this._dialogs.FileAnswers.Enqueue(@"C:\tools\int2ssl.exe");

			viewModel.BrowseExternalDecompilerCommand.Execute(null);
			Assert.Equal(@"C:\tools\int2ssl.exe", viewModel.ExternalDecompilerPath);
			Assert.Equal(@"C:\tools\int2ssl.exe", viewModel.Draft.Compiler.ExternalDecompilerPath);

			viewModel.ClearExternalDecompilerPathCommand.Execute(null);
			Assert.Equal(String.Empty, viewModel.ExternalDecompilerPath);
		}

		[Fact]
		public void Normalize_ABlankExternalDecompilerPathBecomesUnset()
		{
			AppSettings settings = new AppSettings();
			settings.Compiler.ExternalDecompilerPath = " C:\\tools\\int2ssl.exe ";
			SettingsViewModel.Normalize(settings, Array.Empty<String>(), 1);
			Assert.Equal(@"C:\tools\int2ssl.exe", settings.Compiler.ExternalDecompilerPath);

			settings.Compiler.ExternalDecompilerPath = "   ";
			SettingsViewModel.Normalize(settings, Array.Empty<String>(), 1);
			Assert.Null(settings.Compiler.ExternalDecompilerPath);
		}

		[Fact]
		public void MessagePaths_AddInsertsAtTheTopAndIgnoresDuplicates()
		{
			this._settings.Settings.Paths.MessageSearchDirectories = new List<String> { @"C:\a", @"C:\b" };
			SettingsViewModel viewModel = this.Create();

			this._dialogs.FolderAnswers.Enqueue(@"C:\new");
			viewModel.AddMessagePathCommand.Execute(null);
			Assert.Equal(new[] { @"C:\new", @"C:\a", @"C:\b" }, viewModel.MessagePaths.ToArray());
			Assert.Equal(@"C:\new", viewModel.SelectedMessagePath);

			this._dialogs.FolderAnswers.Enqueue(@"c:\A");
			viewModel.AddMessagePathCommand.Execute(null);
			Assert.Equal(3, viewModel.MessagePaths.Count);
			Assert.Equal(@"C:\a", viewModel.SelectedMessagePath);
		}

		[Fact]
		public void MessagePaths_MoveAndRemove()
		{
			this._settings.Settings.Paths.MessageSearchDirectories = new List<String> { @"C:\a", @"C:\b", @"C:\c" };
			SettingsViewModel viewModel = this.Create();

			Assert.False(viewModel.RemoveMessagePathCommand.CanExecute(null));

			viewModel.SelectedMessagePath = @"C:\a";
			Assert.False(viewModel.MoveMessagePathUpCommand.CanExecute(null));
			Assert.True(viewModel.MoveMessagePathDownCommand.CanExecute(null));

			viewModel.MoveMessagePathDownCommand.Execute(null);
			Assert.Equal(new[] { @"C:\b", @"C:\a", @"C:\c" }, viewModel.MessagePaths.ToArray());
			Assert.Equal(@"C:\a", viewModel.SelectedMessagePath);

			viewModel.MoveMessagePathUpCommand.Execute(null);
			Assert.Equal(new[] { @"C:\a", @"C:\b", @"C:\c" }, viewModel.MessagePaths.ToArray());

			viewModel.SelectedMessagePath = @"C:\c";
			Assert.False(viewModel.MoveMessagePathDownCommand.CanExecute(null));

			viewModel.RemoveMessagePathCommand.Execute(null);
			Assert.Equal(new[] { @"C:\a", @"C:\b" }, viewModel.MessagePaths.ToArray());
			Assert.Equal(@"C:\b", viewModel.SelectedMessagePath);
		}

		[Fact]
		public void Normalize_RepairsTypedValues()
		{
			AppSettings settings = new AppSettings();
			settings.Messages.Language = "   ";
			settings.Editor.TabSize = 99;
			settings.Editor.FontIndex = 9;
			settings.Paths.OutputDirectory = "  ";
			settings.Paths.HeadersDirectory = @" C:\h ";
			settings.Paths.ScriptsHeaderFile = String.Empty;

			SettingsViewModel.Normalize(settings, new[] { @" C:\m ", String.Empty, @"c:\M", @"C:\n" }, 3);

			Assert.Equal("english", settings.Messages.Language);
			Assert.Equal(EditorSettings.DefaultTabSize, settings.Editor.TabSize);
			Assert.Equal(0, settings.Editor.FontIndex);
			Assert.Null(settings.Paths.OutputDirectory);
			Assert.Null(settings.Paths.ScriptsHeaderFile);
			Assert.Equal(@"C:\h", settings.Paths.HeadersDirectory);
			Assert.Equal(new[] { @"C:\m", @"C:\n" }, settings.Paths.MessageSearchDirectories.ToArray());
		}

		[Fact]
		public async Task Ok_AppliesTheEditedCopyAndSaves()
		{
			SettingsViewModel viewModel = this.Create();
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.Draft.Compiler.Preprocessor = PreprocessorKind.Watcom;
			viewModel.Draft.Editor.TabsToSpaces = false;
			viewModel.OutputDirectory = @"C:\out";
			viewModel.Language = String.Empty;
			this._dialogs.FolderAnswers.Enqueue(@"C:\msg");
			viewModel.AddMessagePathCommand.Execute(null);

			await viewModel.OkCommand.ExecuteAsync(null);

			Assert.True(closed);
			Assert.True(viewModel.Applied);
			Assert.Equal(1, this._settings.UpdateCount);
			Assert.Equal(0, this._settings.ApplyCount);
			Assert.Equal(1, this._settings.SaveCount);
			Assert.NotSame(viewModel.Draft, this._settings.Settings);
			Assert.Equal(PreprocessorKind.Watcom, this._settings.Settings.Compiler.Preprocessor);
			Assert.False(this._settings.Settings.Editor.TabsToSpaces);
			Assert.Equal(@"C:\out", this._settings.Settings.Paths.OutputDirectory);
			Assert.Equal("english", this._settings.Settings.Messages.Language);
			Assert.Equal(new[] { @"C:\msg" }, this._settings.Settings.Paths.MessageSearchDirectories.ToArray());
			Assert.Equal(0, this._theme.ApplyCount);
		}

		[Fact]
		public async Task Ok_KeepsValuesThatOtherComponentsWroteWhileTheDialogWasOpen()
		{
			AppSettings live = this._settings.Settings;
			SettingsViewModel viewModel = this.Create();
			viewModel.Draft.Compiler.Optimization = OptimizationLevel.Full;

			// Written by other components after the dialog took its copy.
			live.Paths.LastSearchDirectory = @"C:\searched";
			live.Paths.LastMassCompileDirectory = @"C:\mass";
			live.Paths.SolutionFolder = @"C:\project";
			live.Compiler.PreprocessorDefine = "REL";
			live.Editor.SearchWholeWord = true;
			live.Editor.ZoomPercent = 150;
			live.Window.ShowLog = false;
			live.Window.SetPlacement(WindowKind.Flowchart, new WindowPlacement { X = 3, Y = 4, Width = 500, Height = 400 });
			live.IsFirstRunCompleted = true;

			await viewModel.OkCommand.ExecuteAsync(null);

			Assert.Same(live, this._settings.Settings);
			Assert.Equal(OptimizationLevel.Full, live.Compiler.Optimization);
			Assert.Equal(@"C:\searched", live.Paths.LastSearchDirectory);
			Assert.Equal(@"C:\mass", live.Paths.LastMassCompileDirectory);
			Assert.Equal(@"C:\project", live.Paths.SolutionFolder);
			Assert.Equal("REL", live.Compiler.PreprocessorDefine);
			Assert.True(live.Editor.SearchWholeWord);
			Assert.Equal(150, live.Editor.ZoomPercent);
			Assert.False(live.Window.ShowLog);
			Assert.Equal(500, live.Window.GetPlacement(WindowKind.Flowchart)!.Width);
			Assert.True(live.IsFirstRunCompleted);
		}

		[Fact]
		public void OutputDirectory_IsDisabledWhileCompilingNextToTheSources()
		{
			SettingsViewModel viewModel = this.Create();
			List<String> changed = new List<String>();
			((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);

			Assert.True(viewModel.CompileToSourceDirectory);
			Assert.False(viewModel.IsOutputDirectoryEnabled);

			viewModel.CompileToSourceDirectory = false;

			Assert.True(viewModel.IsOutputDirectoryEnabled);
			Assert.False(viewModel.Draft.Compiler.CompileToSourceDirectory);
			Assert.Contains(nameof(SettingsViewModel.IsOutputDirectoryEnabled), changed);

			viewModel.CompileToSourceDirectory = true;
			Assert.False(viewModel.IsOutputDirectoryEnabled);
		}

		[Fact]
		public async Task ExternalEditor_CanBeChosenChangedAndCleared()
		{
			this._settings.Settings.Paths.ExternalEditorPath = @"C:\old\editor.exe";
			SettingsViewModel viewModel = this.Create();
			Assert.Equal(@"C:\old\editor.exe", viewModel.ExternalEditorPath);

			this._dialogs.FileAnswers.Enqueue(@"C:\npp\notepad++.exe");
			viewModel.BrowseExternalEditorCommand.Execute(null);
			Assert.Equal(@"C:\npp\notepad++.exe", viewModel.Draft.Paths.ExternalEditorPath);

			this._dialogs.FileAnswers.Enqueue(null);
			viewModel.BrowseExternalEditorCommand.Execute(null);
			Assert.Equal(@"C:\npp\notepad++.exe", viewModel.ExternalEditorPath);

			viewModel.ClearExternalEditorPathCommand.Execute(null);
			Assert.Equal(String.Empty, viewModel.ExternalEditorPath);

			await viewModel.OkCommand.ExecuteAsync(null);
			Assert.Null(this._settings.Settings.Paths.ExternalEditorPath);
		}

		[Fact]
		public async Task ExternalEditor_TypedPathIsStoredTrimmed()
		{
			SettingsViewModel viewModel = this.Create();
			viewModel.ExternalEditorPath = @"  C:\tools\edit.exe ";

			await viewModel.OkCommand.ExecuteAsync(null);

			Assert.Equal(@"C:\tools\edit.exe", this._settings.Settings.Paths.ExternalEditorPath);
		}

		[Fact]
		public async Task PreprocessorDefines_AreAddedRemovedAndStoredOnOk()
		{
			this._settings.PreprocessorDefines.Items.AddRange(new[] { "ALPHA", "BETA" });
			SettingsViewModel viewModel = this.Create();
			Assert.Equal(new[] { "ALPHA", "BETA" }, viewModel.PreprocessorDefineItems.ToArray());
			Assert.False(viewModel.AddPreprocessorDefineCommand.CanExecute(null));
			Assert.False(viewModel.RemovePreprocessorDefineCommand.CanExecute(null));

			viewModel.NewPreprocessorDefine = " GAMMA=1 ";
			Assert.True(viewModel.AddPreprocessorDefineCommand.CanExecute(null));
			viewModel.AddPreprocessorDefineCommand.Execute(null);
			Assert.Equal(new[] { "ALPHA", "BETA", "GAMMA=1" }, viewModel.PreprocessorDefineItems.ToArray());
			Assert.Equal("GAMMA=1", viewModel.SelectedPreprocessorDefine);
			Assert.Equal(String.Empty, viewModel.NewPreprocessorDefine);

			viewModel.NewPreprocessorDefine = "ALPHA";
			viewModel.AddPreprocessorDefineCommand.Execute(null);
			Assert.Equal(3, viewModel.PreprocessorDefineItems.Count);
			Assert.Equal("ALPHA", viewModel.SelectedPreprocessorDefine);

			viewModel.RemovePreprocessorDefineCommand.Execute(null);
			Assert.Equal(new[] { "BETA", "GAMMA=1" }, viewModel.PreprocessorDefineItems.ToArray());
			Assert.Equal("BETA", viewModel.SelectedPreprocessorDefine);

			// Nothing is stored before the dialog is confirmed.
			Assert.Equal(new[] { "ALPHA", "BETA" }, this._settings.PreprocessorDefines.Items);

			await viewModel.OkCommand.ExecuteAsync(null);
			Assert.Equal(new[] { "BETA", "GAMMA=1" }, this._settings.PreprocessorDefines.Items);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("<Not Preset>")]
		[InlineData("two words")]
		[InlineData("DEBUG")]
		[InlineData("release")]
		public void PreprocessorDefines_RejectUnusableDefinitions(String text)
		{
			SettingsViewModel viewModel = this.Create();

			viewModel.NewPreprocessorDefine = text;

			// "release" is a different (valid) definition from the built-in RELEASE.
			Assert.Equal(String.Equals(text, "release", StringComparison.Ordinal), viewModel.AddPreprocessorDefineCommand.CanExecute(null));
		}

		[Fact]
		public void PreprocessorDefines_AnExistingLabelInTheStoredListIsNotShown()
		{
			this._settings.PreprocessorDefines.Items.AddRange(new[] { "<Not Preset>", "KEEP", " " });

			SettingsViewModel viewModel = this.Create();

			Assert.Equal(new[] { "KEEP" }, viewModel.PreprocessorDefineItems.ToArray());
		}

		[Fact]
		public void MergeCopiesEveryOptionExceptTheOnesTheWindowDoesNotOwn()
		{
			AppSettings live = new AppSettings();
			AppSettings edited = new AppSettings();
			SettingsOptionMutator.ChangeEveryOption(edited);
			AppSettings before = live.Clone();

			SettingsDialogMerge.CopyOwnedOptions(live, edited);

			foreach ((String section, Object liveSection, Object editedSection, Object beforeSection) in new (String, Object, Object, Object)[]
			{
				("Paths", live.Paths, edited.Paths, before.Paths),
				("Compiler", live.Compiler, edited.Compiler, before.Compiler),
				("Editor", live.Editor, edited.Editor, before.Editor),
				("Messages", live.Messages, edited.Messages, before.Messages),
				("Flowchart", live.Flowchart, edited.Flowchart, before.Flowchart),
			})
			{
				foreach (System.Reflection.PropertyInfo property in liveSection.GetType().GetProperties().Where(p => p.CanWrite))
				{
					Boolean foreign = SettingsDialogMerge.ForeignOptions.Contains(section + "." + property.Name);
					Object? expected = (foreign ? beforeSection : editedSection).GetType().GetProperty(property.Name)!.GetValue(foreign ? beforeSection : editedSection);
					Object? actual = property.GetValue(liveSection);
					if (expected is List<String> expectedList)
					{
						Assert.Equal(expectedList, (List<String>)actual!);
					}
					else
					{
						Assert.True(Equals(expected, actual), section + "." + property.Name + (foreign ? " must stay untouched" : " must be copied"));
					}
				}
			}
		}

		[Fact]
		public void MergeForeignOptionsNameRealProperties()
		{
			AppSettings settings = new AppSettings();
			foreach (String name in SettingsDialogMerge.ForeignOptions)
			{
				String[] parts = name.Split('.');
				Object section = typeof(AppSettings).GetProperty(parts[0])!.GetValue(settings)!;
				Assert.NotNull(section.GetType().GetProperty(parts[1]));
			}
		}

		[Fact]
		public async Task Ok_ReportsAFailedSaveButStillApplies()
		{
			this._settings.SaveFailure = new IOException("disk full");
			SettingsViewModel viewModel = this.Create();
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.OkCommand.ExecuteAsync(null);

			Assert.True(closed);
			Assert.Equal(1, this._settings.UpdateCount);
			Assert.Single(this._dialogs.Errors);
			Assert.Contains("disk full", this._dialogs.Errors[0], StringComparison.Ordinal);
		}

		[Fact]
		public void Cancel_DiscardsTheChanges()
		{
			SettingsViewModel viewModel = this.Create();
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.Draft.Compiler.Optimization = OptimizationLevel.Experimental;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closed);
			Assert.False(viewModel.Applied);
			Assert.Equal(0, this._settings.UpdateCount);
			Assert.Equal(OptimizationLevel.Basic, this._settings.Settings.Compiler.Optimization);
		}

		[Fact]
		public async Task Theme_ChangeIsAppliedOnOkAndUpdatesTheEditorScheme()
		{
			this._theme.Apply(AppTheme.Dark);
			this._theme.SystemIsDark = false;
			Int32 appliedBefore = this._theme.ApplyCount;
			this._settings.Settings.Editor.Theme = HighlightTheme.Dark;
			SettingsViewModel viewModel = this.Create();
			Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme);

			viewModel.SelectedTheme = AppTheme.System;
			Assert.Equal(appliedBefore, this._theme.ApplyCount);

			await viewModel.OkCommand.ExecuteAsync(null);

			Assert.Equal(AppTheme.System, this._theme.Current);
			Assert.Equal(appliedBefore + 1, this._theme.ApplyCount);
			Assert.Equal(HighlightTheme.Light, this._settings.Settings.Editor.Theme);
		}

		[Fact]
		public async Task Theme_CancelDoesNotApplyIt()
		{
			SettingsViewModel viewModel = this.Create();

			viewModel.SelectedTheme = AppTheme.Light;
			viewModel.CancelCommand.Execute(null);
			await Task.CompletedTask;

			Assert.Equal(0, this._theme.ApplyCount);
		}
	}
}
