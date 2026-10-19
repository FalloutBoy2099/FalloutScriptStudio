namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class SettingsViewModelPathTests : SettingsViewModelTestBase
	{
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
			List<String> changed = [];
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
			this._settings.Settings.Paths.MessageSearchDirectories = [@"C:\a", @"C:\b"];
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
			this._settings.Settings.Paths.MessageSearchDirectories = [@"C:\a", @"C:\b", @"C:\c"];
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
	}
}