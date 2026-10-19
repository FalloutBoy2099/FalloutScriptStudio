namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Xunit;

	public sealed class AdvancedSearchViewModelTests
	{
		private static readonly String PathA = Path.Combine(TestFiles.Work, "search-a.ssl");
		private static readonly String PathB = Path.Combine(TestFiles.Work, "search-b.ssl");

		private sealed class Harness : IDisposable
		{
			private readonly ScriptToolsTempDirectory _temp = new ScriptToolsTempDirectory();

			public Harness()
			{
				this.Settings = new FakeSettingsService(this._temp.Path);
				this.Workspace = new FakeWorkspace();
				this.Dialogs = new FakeDialogService();
				this.ViewModel = new AdvancedSearchViewModel(this.Workspace.Interface, this.Settings, this.Dialogs, new ScriptSearcher());
			}

			public FakeSettingsService Settings { get; }

			public FakeWorkspace Workspace { get; }

			public FakeDialogService Dialogs { get; }

			public AdvancedSearchViewModel ViewModel { get; }

			public ScriptToolsTempDirectory Temp => this._temp;

			public void Dispose()
			{
				this._temp.Dispose();
			}
		}

		[Fact]
		public void Constructor_LoadsOptionsAndHistoryFromTheSettings()
		{
			StaTestRunner.Run(() =>
			{
				using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
				FakeSettingsService settings = new FakeSettingsService(temp.Path);
				settings.Settings.Editor.SearchIgnoreCase = true;
				settings.Settings.Editor.SearchWholeWord = true;
				settings.Settings.Paths.LastSearchDirectory = "C:\\last";
				settings.SearchHistory.AddQuery("first");
				settings.SearchHistory.AddQuery("second");
				settings.SearchHistory.Directories.AddRange(new[] { "C:\\zeta", "C:\\alpha" });

				AdvancedSearchViewModel viewModel = new AdvancedSearchViewModel(new FakeWorkspace().Interface, settings, new FakeDialogService(), new ScriptSearcher());

				Assert.False(viewModel.MatchCase);
				Assert.True(viewModel.WholeWord);
				Assert.Equal(new[] { "second", "first" }, viewModel.QueryHistory.ToArray());
				Assert.Equal(new[] { "C:\\alpha", "C:\\zeta" }, viewModel.Folders.ToArray());
				Assert.Equal("C:\\last", viewModel.FolderPath);
				Assert.True(viewModel.IsOpenScope);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void TogglingTheOptions_UpdatesTheSettings()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();

				harness.ViewModel.MatchCase = false;
				harness.ViewModel.WholeWord = true;

				Assert.True(harness.Settings.Settings.Editor.SearchIgnoreCase);
				Assert.True(harness.Settings.Settings.Editor.SearchWholeWord);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void FindAll_OpenScripts_ListsEveryLineAndSendsThemToTheOutputPanel()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "procedure Foo begin\r\n  call foo;\r\nend\r\n");
				harness.Workspace.AddDocument(PathB, "nothing\r\n  FOO again\r\n");
				harness.Workspace.AddDocument(null, "unsaved foo\r\n");
				harness.ViewModel.MatchCase = false;
				harness.ViewModel.SearchText = "  foo ";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.Equal(4, harness.ViewModel.Results.Count);
				IReadOnlyList<BuildError> sent = Assert.Single(harness.Workspace.SearchResults);
				Assert.Equal(4, sent.Count);
				Assert.All(sent, error => Assert.Equal(BuildErrorType.Search, error.Type));
				Assert.Equal(PathA, sent[0].FileName);
				Assert.Equal(1, sent[0].Line);
				Assert.Equal(11, sent[0].Column);
				Assert.Equal(3, sent[0].Length);
				Assert.Equal("call foo;", sent[1].Message);
				Assert.Equal(BuildError.NoFile, sent[3].FileName);
				Assert.Equal("foo", harness.ViewModel.SearchText);
				Assert.Equal("foo", harness.Settings.SearchHistory.Queries[0]);
				Assert.Equal("foo", harness.ViewModel.QueryHistory[0]);
				Assert.Contains("4 matching lines in 3 files", harness.ViewModel.StatusText, StringComparison.Ordinal);
				Assert.False(harness.ViewModel.IsSearching);
			});
		}

		[Fact]
		public void FindAll_CurrentScript_SearchesOnlyTheActiveDocument()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "foo\r\n");
				DocumentViewModel second = harness.Workspace.AddDocument(PathB, "foo\r\nfoo\r\n");
				harness.Workspace.ActiveDocument = second;
				harness.ViewModel.IsCurrentScope = true;
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.Equal(2, harness.ViewModel.Results.Count);
				Assert.All(harness.ViewModel.Results, item => Assert.Equal(PathB, item.Hit.FilePath));
			});
		}

		[Fact]
		public void FindAll_NothingFound_SaysSoAndKeepsTheOutputPanelUntouched()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "bar\r\n");
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.Empty(harness.ViewModel.Results);
				Assert.Empty(harness.Workspace.SearchResults);
				Assert.Equal("Search string not found.", harness.ViewModel.StatusText);
			});
		}

		[Fact]
		public void FindAll_InvalidRegularExpression_ShowsTheErrorAndDoesNotSearch()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "foo\r\n");
				harness.ViewModel.UseRegex = true;
				harness.ViewModel.SearchText = "(foo";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.True(harness.ViewModel.HasError);
				Assert.Empty(harness.Workspace.SearchResults);
			});
		}

		[Fact]
		public void FindAll_WithoutOpenScripts_ShowsAnError()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.True(harness.ViewModel.HasError);
			});
		}

		[Fact]
		public void FindAll_Folder_WithoutAPath_ReportsNoSearchPath()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.IsFolderScope = true;
				harness.ViewModel.FolderPath = Path.Combine(harness.Temp.Path, "missing");
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.Equal("No search path set.", harness.ViewModel.ErrorMessage);
			});
		}

		[Fact]
		public void FindAll_Folder_SearchesFilesAndPrefersOpenTextAndRemembersTheFolder()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				String saved = harness.Temp.Write("a.ssl", "disk text\r\n");
				harness.Temp.Write("sub/b.h", "#define NEEDLE 1\r\n");
				harness.Temp.Write("c.txt", "needle in txt\r\n");
				harness.Workspace.AddDocument(saved, "edited needle\r\n");
				harness.ViewModel.MatchCase = false;
				harness.ViewModel.IsFolderScope = true;
				harness.ViewModel.FolderPath = harness.Temp.Path;
				harness.ViewModel.SearchText = "needle";
				Int32 settingsChanges = 0;
				harness.Settings.SettingsChanged += (_, _) => settingsChanges++;

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				// The folder is stored through the settings Update API, so the change is announced (and saved) like any other.
				Assert.True(settingsChanges > 0);
				Assert.Equal(2, harness.ViewModel.Results.Count);
				Assert.Equal("edited needle", harness.ViewModel.Results[0].Text);
				Assert.EndsWith("b.h", harness.ViewModel.Results[1].Hit.FilePath, StringComparison.Ordinal);
				Assert.Equal(Path.GetFullPath(harness.Temp.Path), harness.Settings.Settings.Paths.LastSearchDirectory);
				Assert.Contains(Path.GetFullPath(harness.Temp.Path), harness.Settings.SearchHistory.Directories);
			});
		}

		[Fact]
		public void FindAll_Folder_HonorsMaskAndSubfolderOptions()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Temp.Write("a.ssl", "needle\r\n");
				harness.Temp.Write("sub/b.ssl", "needle\r\n");
				harness.Temp.Write("c.txt", "needle\r\n");
				harness.ViewModel.IsFolderScope = true;
				harness.ViewModel.FolderPath = harness.Temp.Path;
				harness.ViewModel.SearchText = "needle";
				harness.ViewModel.FileMask = "*.txt;*.ssl";
				harness.ViewModel.IncludeSubfolders = false;

				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				Assert.Equal(new[] { "a.ssl", "c.txt" }, harness.ViewModel.Results.Select(item => item.FileName).ToArray());
			});
		}

		[Fact]
		public void FindNext_WalksThroughOpenScriptsAndWraps()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "foo\r\nbar foo\r\n");
				harness.Workspace.AddDocument(PathB, "xx foo\r\n");
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);
				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);
				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);
				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);

				Assert.Equal(
					new (String?, Int32, Int32, Int32)[] { (PathA, 1, 1, 3), (PathA, 2, 5, 3), (PathB, 1, 4, 3), (PathA, 1, 1, 3) },
					harness.Workspace.Navigations.ToArray());
			});
		}

		[Fact]
		public void FindNext_NothingFound_SetsTheStatus()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "bar\r\n");
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);

				Assert.Empty(harness.Workspace.Navigations);
				Assert.Equal("Search string not found.", harness.ViewModel.StatusText);
			});
		}

		[Fact]
		public void FindNext_InAnUnsavedDocument_SelectsTheMatchInTheEditor()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				DocumentViewModel unsaved = harness.Workspace.AddDocument(null, "one\r\ntwo foo\r\n");
				harness.ViewModel.SearchText = "foo";

				await harness.ViewModel.FindNextCommand.ExecuteAsync(null);

				Assert.Same(unsaved, harness.Workspace.ActiveDocument);
				Assert.Equal("foo", unsaved.Editor!.SelectedText);
			});
		}

		[Fact]
		public void Replace_ReplacesTheSelectedMatchAndMovesToTheNext()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				DocumentViewModel document = harness.Workspace.AddDocument(PathA, "foo bar foo\r\n");
				harness.ViewModel.SearchText = "foo";
				harness.ViewModel.ReplaceText = "baz";

				await harness.ViewModel.ReplaceCommand.ExecuteAsync(null);
				Assert.Equal("foo bar foo\r\n", document.TextDocument.Text);
				Assert.Equal("foo", document.Editor!.SelectedText);

				await harness.ViewModel.ReplaceCommand.ExecuteAsync(null);
				Assert.Equal("baz bar foo\r\n", document.TextDocument.Text);
				Assert.Equal(8, document.Editor.SelectionStart);

				await harness.ViewModel.ReplaceCommand.ExecuteAsync(null);
				Assert.Equal("baz bar baz\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void ReplaceAll_CurrentScript_ReplacesEverythingInOneUndoStep()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				DocumentViewModel first = harness.Workspace.AddDocument(PathA, "foo foo\r\nfoo\r\n");
				DocumentViewModel second = harness.Workspace.AddDocument(PathB, "foo\r\n");
				harness.ViewModel.IsCurrentScope = true;
				harness.ViewModel.SearchText = "foo";
				harness.ViewModel.ReplaceText = "x";

				await harness.ViewModel.ReplaceAllCommand.ExecuteAsync(null);

				Assert.Equal("x x\r\nx\r\n", first.TextDocument.Text);
				Assert.Equal("foo\r\n", second.TextDocument.Text);
				Assert.Equal(0, harness.Dialogs.ConfirmCount);
				first.TextDocument.UndoStack.Undo();
				Assert.Equal("foo foo\r\nfoo\r\n", first.TextDocument.Text);
			});
		}

		[Fact]
		public void ReplaceAll_OpenScripts_AsksForConfirmationFirst()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				DocumentViewModel first = harness.Workspace.AddDocument(PathA, "foo\r\n");
				DocumentViewModel second = harness.Workspace.AddDocument(PathB, "a foo b\r\n");
				harness.ViewModel.SearchText = "foo";
				harness.ViewModel.ReplaceText = "x";
				harness.Dialogs.ConfirmAnswer = false;

				await harness.ViewModel.ReplaceAllCommand.ExecuteAsync(null);
				Assert.Equal("foo\r\n", first.TextDocument.Text);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.ViewModel.ReplaceAllCommand.ExecuteAsync(null);

				Assert.Equal(2, harness.Dialogs.ConfirmCount);
				Assert.Equal("x\r\n", first.TextDocument.Text);
				Assert.Equal("a x b\r\n", second.TextDocument.Text);
				Assert.Equal("Replaced 2 occurrences in 2 scripts.", Assert.Single(harness.Workspace.Statuses));
			});
		}

		[Fact]
		public void ReplaceAll_Regex_ExpandsGroups()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				DocumentViewModel document = harness.Workspace.AddDocument(PathA, "set(a, 1)\r\n");
				harness.ViewModel.UseRegex = true;
				harness.ViewModel.SearchText = "set\\((\\w+), (\\d+)\\)";
				harness.ViewModel.ReplaceText = "$1 = $2";
				harness.ViewModel.IsCurrentScope = true;

				await harness.ViewModel.ReplaceAllCommand.ExecuteAsync(null);

				Assert.Equal("a = 1\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void ReplaceAll_WithARegexThatTimesOut_ReportsTheErrorAndChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				String text = new String('a', 40) + "!\r\n";
				DocumentViewModel document = harness.Workspace.AddDocument(PathA, text);
				harness.ViewModel.UseRegex = true;
				harness.ViewModel.SearchText = "(a|aa)+$";
				harness.ViewModel.ReplaceText = "x";
				harness.ViewModel.IsCurrentScope = true;

				await harness.ViewModel.ReplaceAllCommand.ExecuteAsync(null);

				Assert.Contains("too long", harness.ViewModel.ErrorMessage, StringComparison.Ordinal);
				Assert.Equal(text, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Replace_IsNotAvailableForFolders()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.IsFolderScope = true;

				Assert.False(harness.ViewModel.CanReplace);
				Assert.False(harness.ViewModel.ReplaceCommand.CanExecute(null));
				Assert.False(harness.ViewModel.ReplaceAllCommand.CanExecute(null));
				Assert.False(harness.ViewModel.FindNextCommand.CanExecute(null));
				harness.ViewModel.IsOpenScope = true;
				Assert.True(harness.ViewModel.ReplaceAllCommand.CanExecute(null));
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void GoToResult_NavigatesToTheFileLineAndColumn()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(PathA, "x\r\n  foo\r\n");
				harness.Workspace.AddDocument(null, "foo\r\n");
				harness.ViewModel.SearchText = "foo";
				await harness.ViewModel.FindAllCommand.ExecuteAsync(null);

				await harness.ViewModel.GoToResultCommand.ExecuteAsync(harness.ViewModel.Results[0]);
				await harness.ViewModel.GoToResultCommand.ExecuteAsync(harness.ViewModel.Results[1]);

				Assert.Equal(
					new (String?, Int32, Int32, Int32)[] { (PathA, 2, 3, 3), (null, 1, 1, 3) },
					harness.Workspace.Navigations.ToArray());
			});
		}

		[Fact]
		public void BrowseFolder_AddsTheFolderToTheSavedListAndRemoveDeletesIt()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();
				harness.Dialogs.FolderAnswers.Enqueue(harness.Temp.Path);

				harness.ViewModel.BrowseFolderCommand.Execute(null);

				Assert.Equal(harness.Temp.Path, harness.ViewModel.FolderPath);
				Assert.Equal(new[] { harness.Temp.Path }, harness.ViewModel.Folders.ToArray());
				Assert.Equal(new[] { harness.Temp.Path }, harness.Settings.SearchHistory.Directories.ToArray());

				harness.ViewModel.RemoveSavedFolderCommand.Execute(null);

				Assert.Empty(harness.ViewModel.Folders);
				Assert.Empty(harness.Settings.SearchHistory.Directories);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void BrowseFolder_Cancelled_ChangesNothing()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();
				harness.Dialogs.FolderAnswers.Enqueue(null);

				harness.ViewModel.BrowseFolderCommand.Execute(null);

				Assert.Empty(harness.ViewModel.Folders);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Search_InFolderScope_RunsFindAll_OtherwiseFindNext()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Temp.Write("a.ssl", "needle\r\n");
				harness.Workspace.AddDocument(PathA, "needle\r\n");
				harness.ViewModel.SearchText = "needle";

				await harness.ViewModel.SearchCommand.ExecuteAsync(null);
				Assert.Single(harness.Workspace.Navigations);
				Assert.Empty(harness.Workspace.SearchResults);

				harness.ViewModel.IsFolderScope = true;
				harness.ViewModel.FolderPath = harness.Temp.Path;
				await harness.ViewModel.SearchCommand.ExecuteAsync(null);

				Assert.Single(harness.Workspace.SearchResults);
			});
		}
	}
}