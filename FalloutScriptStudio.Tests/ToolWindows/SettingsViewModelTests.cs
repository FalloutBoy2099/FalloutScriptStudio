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

	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class SettingsViewModelTests : SettingsViewModelTestBase
	{
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
