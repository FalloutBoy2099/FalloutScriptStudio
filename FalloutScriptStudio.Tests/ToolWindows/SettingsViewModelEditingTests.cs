namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class SettingsViewModelEditingTests : SettingsViewModelTestBase
	{
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
	}
}
