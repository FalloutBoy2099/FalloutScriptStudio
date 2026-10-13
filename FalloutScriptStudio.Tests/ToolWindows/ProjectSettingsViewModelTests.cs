namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The project settings window: inherit or override each option, saved to fss-project.json and applied at once.</summary>
	public sealed class ProjectSettingsViewModelTests : IDisposable
	{
		private readonly TempFolder _root = new TempFolder();
		private readonly SettingsService _settings;
		private readonly ProjectService _projects;
		private readonly FakeDialogService _dialogs = new FakeDialogService();
		private readonly String _folder;

		public ProjectSettingsViewModelTests()
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

		private String ProjectFile => Path.Combine(this._folder, ProjectSettings.FileName);

		private ProjectSettingsViewModel Open(String? projectFile = "{ }")
		{
			if (projectFile is not null)
			{
				File.WriteAllText(this.ProjectFile, projectFile);
			}

			Assert.True(this._projects.Open(this._folder));
			return new ProjectSettingsViewModel(this._projects, this._settings, this._dialogs);
		}

		[Fact]
		public void WithoutASettingsFile_NothingCanBeEdited_UntilTheFileIsCreated()
		{
			ProjectSettingsViewModel viewModel = this.Open(null);

			Assert.True(viewModel.IsMissing);
			Assert.True(viewModel.CanCreate);
			Assert.False(viewModel.IsEditable);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
			Assert.Equal(this._folder, viewModel.ProjectFolder);
			Assert.Equal(this.ProjectFile, viewModel.ProjectFilePath);

			viewModel.CreateSettingsCommand.Execute(null);

			Assert.True(File.Exists(this.ProjectFile));
			Assert.True(viewModel.IsEditable);
			Assert.False(viewModel.CanCreate);
			Assert.True(viewModel.SaveCommand.CanExecute(null));
		}

		[Fact]
		public void ADamagedFile_CanBeReplacedByANewOne()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ not json");

			Assert.True(viewModel.IsDamaged);
			Assert.True(viewModel.CanCreate);

			viewModel.CreateSettingsCommand.Execute(null);

			Assert.True(viewModel.IsEditable);
			Assert.False(viewModel.IsDamaged);
		}

		[Fact]
		public void Options_ThatInherit_ShowTheGlobalValue()
		{
			this._settings.Update(settings =>
			{
				settings.Paths.OutputDirectory = @"C:\game\scripts";
				settings.Compiler.Optimization = OptimizationLevel.Full;
				settings.Compiler.ShortCircuit = true;
				settings.Compiler.PreprocessorDefine = "GLOBAL";
				settings.Paths.MessageSearchDirectories = new List<String> { @"C:\msg" };
			});

			ProjectSettingsViewModel viewModel = this.Open();

			Assert.False(viewModel.OutputDirectory.IsOverridden);
			Assert.Equal(0, viewModel.OutputDirectory.ModeIndex);
			Assert.Equal(@"C:\game\scripts", viewModel.OutputDirectory.Value);
			Assert.Equal(OptimizationLevel.Full, viewModel.Optimization.Value);
			Assert.True(viewModel.ShortCircuit.Value);
			Assert.Equal("GLOBAL", viewModel.SelectedDefine.Value);
			Assert.False(viewModel.MessagePathsMode.IsOverridden);
			Assert.Equal(new[] { @"C:\msg" }, viewModel.MessagePaths.ToArray());
		}

		[Fact]
		public void Options_ThatTheProjectOverrides_ShowTheProjectValue()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"outputDirectory\": \"out\", \"optimization\": \"none\", \"shortCircuit\": true, \"preprocessor\": \"watcom\", \"preprocessorDefine\": \"\", \"messageSearchDirectories\": [ \"msg\" ], \"defines\": [ \"MOD\" ], \"name\": \"The Mod\" }");

			Assert.True(viewModel.OutputDirectory.IsOverridden);
			Assert.Equal(1, viewModel.OutputDirectory.ModeIndex);
			Assert.Equal(Path.Combine(this._folder, "out"), viewModel.OutputDirectory.Value);
			Assert.Equal(OptimizationLevel.None, viewModel.Optimization.Value);
			Assert.True(viewModel.ShortCircuit.Value);
			Assert.Equal(PreprocessorKind.Watcom, viewModel.Preprocessor.Value);
			Assert.True(viewModel.SelectedDefine.IsOverridden);
			Assert.Equal(String.Empty, viewModel.SelectedDefine.Value);
			Assert.True(viewModel.MessagePathsMode.IsOverridden);
			Assert.Equal(new[] { Path.Combine(this._folder, "msg") }, viewModel.MessagePaths.ToArray());
			Assert.Equal(new[] { "MOD" }, viewModel.ProjectDefines.ToArray());
			Assert.Equal("The Mod", viewModel.ProjectName);
			Assert.False(viewModel.HeadersDirectory.IsOverridden);
		}

		[Fact]
		public void SwitchingToOverride_StartsFromTheGlobalValue_AndSwitchingBackShowsItAgain()
		{
			this._settings.Update(settings => settings.Paths.HeadersDirectory = @"C:\headers");
			ProjectSettingsViewModel viewModel = this.Open();

			viewModel.HeadersDirectory.ModeIndex = 1;
			Assert.True(viewModel.HeadersDirectory.IsOverridden);
			Assert.Equal(@"C:\headers", viewModel.HeadersDirectory.Value);

			viewModel.HeadersDirectory.Value = @"D:\mine";
			viewModel.HeadersDirectory.ModeIndex = 0;

			Assert.False(viewModel.HeadersDirectory.IsOverridden);
			Assert.Equal(@"C:\headers", viewModel.HeadersDirectory.Value);
			Assert.Equal(@"C:\headers", viewModel.HeadersDirectory.EffectiveValue);
		}

		[Fact]
		public void Save_WritesOnlyTheOverriddenOptions_AppliesThemAtOnce_AndClosesTheWindow()
		{
			this._settings.Update(settings => settings.Compiler.Optimization = OptimizationLevel.Basic);
			ProjectSettingsViewModel viewModel = this.Open();
			Boolean? closedWith = null;
			viewModel.CloseRequested += (_, accepted) => closedWith = accepted;

			viewModel.Optimization.IsOverridden = true;
			viewModel.Optimization.Value = OptimizationLevel.Full;
			viewModel.OutputDirectory.IsOverridden = true;
			viewModel.OutputDirectory.Value = Path.Combine(this._folder, "build");
			viewModel.ShortCircuit.IsOverridden = true;
			viewModel.ShortCircuit.Value = true;
			viewModel.ProjectName = "  The Mod  ";
			viewModel.SaveCommand.Execute(null);

			Assert.True(viewModel.Applied);
			Assert.True(closedWith);
			String json = File.ReadAllText(this.ProjectFile);
			Assert.Contains("\"optimization\": \"full\"", json, StringComparison.Ordinal);
			Assert.Contains("\"outputDirectory\": \"build\"", json, StringComparison.Ordinal);
			Assert.Contains("\"shortCircuit\": true", json, StringComparison.Ordinal);
			Assert.Contains("\"name\": \"The Mod\"", json, StringComparison.Ordinal);
			Assert.DoesNotContain("headersDirectory", json, StringComparison.Ordinal);
			Assert.DoesNotContain("showWarnings", json, StringComparison.Ordinal);
			Assert.DoesNotContain("messageSearchDirectories", json, StringComparison.Ordinal);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Equal(Path.Combine(this._folder, "build"), this._settings.Effective.Settings.Paths.OutputDirectory);
			Assert.Equal(OptimizationLevel.Basic, this._settings.Settings.Compiler.Optimization);
			Assert.Equal("The Mod", this._projects.DisplayName);
		}

		[Fact]
		public void Save_NeverWritesTheGlobalSettings()
		{
			ProjectSettingsViewModel viewModel = this.Open();
			viewModel.ShowWarnings.IsOverridden = true;
			viewModel.ShowWarnings.Value = false;

			viewModel.SaveCommand.Execute(null);

			Assert.True(this._settings.Settings.Compiler.ShowWarnings);
			Assert.False(this._settings.Effective.Settings.Compiler.ShowWarnings);
			Assert.False(File.Exists(this._settings.Paths.SettingsFile));
		}

		[Fact]
		public void SettingAnOptionBackToInherit_RemovesItFromTheFile()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"debugInfo\": false, \"shortCircuit\": true }");
			viewModel.DebugInfo.ModeIndex = 0;

			viewModel.SaveCommand.Execute(null);

			String json = File.ReadAllText(this.ProjectFile);
			Assert.DoesNotContain("debugInfo", json, StringComparison.Ordinal);
			Assert.Contains("shortCircuit", json, StringComparison.Ordinal);
			Assert.True(this._settings.Effective.Settings.Compiler.DebugInfo);
		}

		[Fact]
		public void Save_KeepsThePropertiesThisVersionDoesNotKnow()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"futureThing\": [1, 2, 3] }");
			viewModel.Preprocessor.IsOverridden = true;
			viewModel.Preprocessor.Value = PreprocessorKind.Builtin;

			viewModel.SaveCommand.Execute(null);

			String json = File.ReadAllText(this.ProjectFile);
			Assert.Contains("futureThing", json, StringComparison.Ordinal);
			Assert.Contains("\"preprocessor\": \"builtin\"", json, StringComparison.Ordinal);
		}

		[Fact]
		public void Save_ThenOpeningTheFileAgain_ShowsTheSameState()
		{
			ProjectSettingsViewModel first = this.Open();
			first.CompileToSourceDirectory.IsOverridden = true;
			first.CompileToSourceDirectory.Value = false;
			first.MessagePathsMode.IsOverridden = true;
			first.MessagePaths.Add(Path.Combine(this._folder, "msg"));
			first.ProjectDefines.Add("MOD");
			first.SelectedDefine.IsOverridden = true;
			first.SelectedDefine.Value = "MOD";
			first.SaveCommand.Execute(null);

			ProjectSettingsViewModel second = new ProjectSettingsViewModel(this._projects, this._settings, this._dialogs);

			Assert.True(second.CompileToSourceDirectory.IsOverridden);
			Assert.False(second.CompileToSourceDirectory.Value);
			Assert.True(second.MessagePathsMode.IsOverridden);
			Assert.Equal(new[] { Path.Combine(this._folder, "msg") }, second.MessagePaths.ToArray());
			Assert.Equal("MOD", second.SelectedDefine.Value);
			Assert.Contains(second.DefineChoices, option => option.Value == "MOD");
		}

		[Fact]
		public void Cancel_WritesNothing_AndClosesTheWindow()
		{
			ProjectSettingsViewModel viewModel = this.Open();
			Boolean? closedWith = null;
			viewModel.CloseRequested += (_, accepted) => closedWith = accepted;
			viewModel.ShortCircuit.IsOverridden = true;
			viewModel.ShortCircuit.Value = true;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closedWith);
			Assert.False(viewModel.Applied);
			Assert.Equal("{ }", File.ReadAllText(this.ProjectFile));
			Assert.False(this._settings.Effective.Settings.Compiler.ShortCircuit);
		}

		[Fact]
		public void AFileThatCannotBeWritten_ShowsAnError_AndKeepsTheWindowOpen()
		{
			ProjectSettingsViewModel viewModel = this.Open();
			Boolean closed = false;
			viewModel.CloseRequested += (_, _) => closed = true;
			viewModel.ShortCircuit.IsOverridden = true;

			using (new FileStream(this.ProjectFile, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				viewModel.SaveCommand.Execute(null);
			}

			Assert.False(closed);
			Assert.False(viewModel.Applied);
			Assert.Contains(ProjectSettings.FileName, Assert.Single(this._dialogs.Errors), StringComparison.Ordinal);
			Assert.Equal("{ }", File.ReadAllText(this.ProjectFile));
		}

		[Fact]
		public void TheFolderPickers_FillTheOverriddenValues()
		{
			ProjectSettingsViewModel viewModel = this.Open();
			String headers = this._root.Combine("headers");
			this._dialogs.FolderAnswers.Enqueue(headers);
			this._dialogs.FolderAnswers.Enqueue(this._root.Combine("out"));
			this._dialogs.FileAnswers.Enqueue(this._root.Combine("custom", "scripts.lst"));
			viewModel.HeadersDirectory.IsOverridden = true;
			viewModel.OutputDirectory.IsOverridden = true;
			viewModel.ScriptsListFile.IsOverridden = true;

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);
			viewModel.BrowseOutputDirectoryCommand.Execute(null);
			viewModel.BrowseScriptsListFileCommand.Execute(null);

			Assert.Equal(headers, viewModel.HeadersDirectory.Value);
			Assert.Equal(this._root.Combine("out"), viewModel.OutputDirectory.Value);
			Assert.Equal(this._root.Combine("custom", "scripts.lst"), viewModel.ScriptsListFile.Value);
		}

		[Fact]
		public void ACancelledPicker_ChangesNothing()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"headersDirectory\": \"headers\" }");
			String before = viewModel.HeadersDirectory.Value;

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);
			viewModel.BrowseScriptsHeaderFileCommand.Execute(null);

			Assert.Equal(before, viewModel.HeadersDirectory.Value);
			Assert.False(viewModel.ScriptsHeaderFile.IsOverridden);
		}

		[Fact]
		public void ChoosingTheHeadersFolder_ProposesScriptsH_WhenNoScriptsHeaderIsConfigured()
		{
			ProjectSettingsViewModel viewModel = this.Open();
			String headers = this._root.Combine("headers");
			this._dialogs.FolderAnswers.Enqueue(headers);
			viewModel.HeadersDirectory.IsOverridden = true;

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);

			Assert.True(viewModel.ScriptsHeaderFile.IsOverridden);
			Assert.Equal(Path.Combine(headers, "SCRIPTS.H"), viewModel.ScriptsHeaderFile.Value);
		}

		[Fact]
		public void ChoosingTheHeadersFolder_KeepsTheScriptsHeaderThatIsAlreadyConfigured()
		{
			this._settings.Update(settings => settings.Paths.ScriptsHeaderFile = @"C:\old\SCRIPTS.H");
			ProjectSettingsViewModel viewModel = this.Open();
			this._dialogs.FolderAnswers.Enqueue(this._root.Combine("headers"));
			viewModel.HeadersDirectory.IsOverridden = true;

			viewModel.BrowseHeadersDirectoryCommand.Execute(null);

			Assert.False(viewModel.ScriptsHeaderFile.IsOverridden);
		}

		[Fact]
		public void TheDefinitionList_AddsValidNewDefinitions_AndRefusesBuiltInOnesAndBlanks()
		{
			ProjectSettingsViewModel viewModel = this.Open();

			viewModel.NewProjectDefine = "DEBUG";
			Assert.False(viewModel.AddProjectDefineCommand.CanExecute(null));
			viewModel.NewProjectDefine = "has space";
			Assert.False(viewModel.AddProjectDefineCommand.CanExecute(null));
			viewModel.NewProjectDefine = "  MOD  ";
			Assert.True(viewModel.AddProjectDefineCommand.CanExecute(null));
			viewModel.AddProjectDefineCommand.Execute(null);
			viewModel.NewProjectDefine = "MOD";
			viewModel.AddProjectDefineCommand.Execute(null);

			Assert.Equal(new[] { "MOD" }, viewModel.ProjectDefines.ToArray());
			Assert.Equal("MOD", viewModel.SelectedProjectDefine);
			Assert.Equal(String.Empty, viewModel.NewProjectDefine);
		}

		[Fact]
		public void TheDefineChoices_ListNoneTheBuiltInTheGlobalAndTheProjectDefinitions()
		{
			this._settings.PreprocessorDefines.Items.Add("GLOBALDEF");
			ProjectSettingsViewModel viewModel = this.Open();

			viewModel.NewProjectDefine = "MOD";
			viewModel.AddProjectDefineCommand.Execute(null);

			Assert.Equal(new[] { String.Empty, "DEBUG", "RELEASE", "GLOBALDEF", "MOD" }, viewModel.DefineChoices.Select(option => option.Value).ToArray());
		}

		[Fact]
		public void RemovingTheSelectedProjectDefinition_ResetsTheSelectedDefinitionToNone()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"defines\": [ \"MOD\", \"OTHER\" ], \"preprocessorDefine\": \"MOD\" }");
			viewModel.SelectedProjectDefine = "MOD";

			viewModel.RemoveProjectDefineCommand.Execute(null);

			Assert.Equal(new[] { "OTHER" }, viewModel.ProjectDefines.ToArray());
			Assert.Equal(String.Empty, viewModel.SelectedDefine.Value);
			Assert.DoesNotContain(viewModel.DefineChoices, option => option.Value == "MOD");
			Assert.Equal("OTHER", viewModel.SelectedProjectDefine);
		}

		[Fact]
		public void TheMessageFolders_InheritTheGlobalList_AndAreEditableOnlyWhileOverridden()
		{
			this._settings.Update(settings => settings.Paths.MessageSearchDirectories = new List<String> { @"C:\a", @"C:\b" });
			ProjectSettingsViewModel viewModel = this.Open();
			viewModel.SelectedMessagePath = @"C:\b";
			Assert.False(viewModel.MoveMessagePathUpCommand.CanExecute(null));
			Assert.False(viewModel.RemoveMessagePathCommand.CanExecute(null));

			viewModel.MessagePathsMode.IsOverridden = true;
			viewModel.SelectedMessagePath = @"C:\b";
			Assert.True(viewModel.MoveMessagePathUpCommand.CanExecute(null));
			viewModel.MoveMessagePathUpCommand.Execute(null);
			Assert.Equal(new[] { @"C:\b", @"C:\a" }, viewModel.MessagePaths.ToArray());
			Assert.False(viewModel.MoveMessagePathUpCommand.CanExecute(null));
			Assert.True(viewModel.MoveMessagePathDownCommand.CanExecute(null));

			this._dialogs.FolderAnswers.Enqueue(@"C:\c");
			viewModel.AddMessagePathCommand.Execute(null);
			Assert.Equal(@"C:\c", viewModel.MessagePaths[0]);
			viewModel.RemoveMessagePathCommand.Execute(null);
			Assert.Equal(new[] { @"C:\b", @"C:\a" }, viewModel.MessagePaths.ToArray());
		}

		[Fact]
		public void GoingBackToInheritForTheMessageFolders_RestoresTheGlobalListAndDropsTheOverride()
		{
			this._settings.Update(settings => settings.Paths.MessageSearchDirectories = new List<String> { @"C:\a" });
			ProjectSettingsViewModel viewModel = this.Open("{ \"messageSearchDirectories\": [ \"msg\" ] }");
			Assert.Equal(new[] { Path.Combine(this._folder, "msg") }, viewModel.MessagePaths.ToArray());

			viewModel.MessagePathsMode.ModeIndex = 0;

			Assert.Equal(new[] { @"C:\a" }, viewModel.MessagePaths.ToArray());
			Assert.Null(viewModel.ToSettings().MessageSearchDirectories);
		}

		[Fact]
		public void ToSettings_OfAnUntouchedWindow_ChangesNothingInTheFile()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"name\": \"Mod\", \"debugInfo\": false, \"defines\": [ \"A\" ] }");

			ProjectSettings settings = viewModel.ToSettings();

			Assert.Equal("Mod", settings.Name);
			Assert.False(settings.DebugInfo);
			Assert.Equal(new[] { "A" }, settings.Defines.ToArray());
			Assert.Null(settings.HeadersDirectory);
			Assert.Null(settings.Optimization);
			Assert.Null(settings.PreprocessorDefine);
			Assert.Null(settings.MessageSearchDirectories);
		}

		[Fact]
		public void AnOverrideThatIsBlank_IsKeptAsNotSet()
		{
			this._settings.Update(settings => settings.Paths.OutputDirectory = @"C:\game\scripts");
			ProjectSettingsViewModel viewModel = this.Open();
			viewModel.OutputDirectory.IsOverridden = true;
			viewModel.OutputDirectory.Value = "   ";

			viewModel.SaveCommand.Execute(null);

			Assert.Contains("\"outputDirectory\": \"\"", File.ReadAllText(this.ProjectFile), StringComparison.Ordinal);
			Assert.Null(this._settings.Effective.Settings.Paths.OutputDirectory);
		}

		[Fact]
		public void ReloadFromFile_ReadsAHandEditedFileAndDiscardsWhatWasEditedInTheWindow()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"name\": \"Before\", \"optimization\": \"none\" }");
			viewModel.ProjectName = "Typed in the window";
			File.WriteAllText(this.ProjectFile, "{ \"name\": \"By hand\", \"optimization\": \"full\", \"defines\": [ \"EDITED\" ] }");

			viewModel.ReloadFromFileCommand.Execute(null);

			Assert.Equal("By hand", viewModel.ProjectName);
			Assert.True(viewModel.Optimization.IsOverridden);
			Assert.Equal(OptimizationLevel.Full, viewModel.Optimization.Value);
			Assert.Equal(new[] { "EDITED" }, viewModel.ProjectDefines.ToArray());
			Assert.Equal("By hand", this._projects.DisplayName);
			Assert.False(viewModel.Applied);
		}

		[Fact]
		public void ReloadFromFile_ShowsAFileThatWasCreatedOrDamagedMeanwhile()
		{
			ProjectSettingsViewModel viewModel = this.Open(null);
			Assert.True(viewModel.IsMissing);

			File.WriteAllText(this.ProjectFile, "{ \"name\": \"Hand made\" }");
			viewModel.ReloadFromFileCommand.Execute(null);
			Assert.True(viewModel.IsEditable);
			Assert.Equal("Hand made", viewModel.ProjectName);

			File.WriteAllText(this.ProjectFile, "{ not json");
			viewModel.ReloadFromFileCommand.Execute(null);
			Assert.True(viewModel.IsDamaged);
			Assert.False(viewModel.IsEditable);
		}

		[Fact]
		public void OpenProjectFile_ClosesTheWindowWithoutSavingAndAsksForTheFileToBeOpened()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"name\": \"Mod\" }");
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.ProjectName = "Unsaved";

			Assert.True(viewModel.OpenProjectFileCommand.CanExecute(null));
			viewModel.OpenProjectFileCommand.Execute(null);

			Assert.False(closed);
			Assert.True(viewModel.OpenFileRequested);
			Assert.False(viewModel.Applied);
			Assert.Equal("{ \"name\": \"Mod\" }", File.ReadAllText(this.ProjectFile));
			Assert.Equal("Mod", this._projects.DisplayName);
		}

		[Fact]
		public void OpenProjectFile_IsOnlyPossibleWhenThereIsAFile()
		{
			ProjectSettingsViewModel viewModel = this.Open(null);
			Assert.False(viewModel.HasFile);
			Assert.False(viewModel.OpenProjectFileCommand.CanExecute(null));

			viewModel.CreateSettingsCommand.Execute(null);

			Assert.True(viewModel.HasFile);
			Assert.True(viewModel.OpenProjectFileCommand.CanExecute(null));
		}

		[Fact]
		public void TheButtonNamesTheFile_AndTheWindowOpensOnTheOptionItWasAskedFor()
		{
			ProjectSettingsViewModel viewModel = this.Open();

			Assert.Equal("Open the file fss-project.json", viewModel.OpenFileText);
			Assert.Null(viewModel.FocusOptionId);

			viewModel.FocusOptionId = ProjectOptions.Optimization;
			Assert.Equal("Optimization", viewModel.FocusOptionId);
		}
	}
}
