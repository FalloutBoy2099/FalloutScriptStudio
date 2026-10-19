namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Xunit;

	/// <summary>The Replace in Files command of the advanced search window: what it asks of the service for each scope, and what it says afterwards.</summary>
	public sealed class AdvancedSearchReplaceInFilesTests
	{
		private sealed class FakeService : IReplaceInFilesService
		{
			public event EventHandler? UndoStateChanged
			{
				add { }
				remove { }
			}

			public Boolean CanUndo => false;

			public List<ReplaceInFilesRequest> Requests { get; } = [];

			public Func<ReplaceInFilesRequest, ReplaceOutcome>? Answer { get; set; }

			public Exception? Failure { get; set; }

			public Task<ReplaceOutcome> RunAsync(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
			{
				this.Requests.Add(request);
				if (this.Failure is not null)
				{
					return Task.FromException<ReplaceOutcome>(this.Failure);
				}

				ReplacePlan plan = new ReplacePlan(Array.Empty<ReplaceFilePlan>(), Array.Empty<ReplaceSkip>(), 0, false, System.Text.Encoding.UTF8);
				return Task.FromResult(this.Answer?.Invoke(request) ?? new ReplaceOutcome(ReplaceOutcomeKind.NothingFound, plan, null));
			}

			public Task<ReplaceUndoResult?> UndoLastAsync(CancellationToken cancellationToken)
			{
				return Task.FromResult<ReplaceUndoResult?>(null);
			}
		}

		private sealed class Harness : IDisposable
		{
			private readonly ScriptToolsTempDirectory _temp = new ScriptToolsTempDirectory();

			public Harness(Boolean withService = true)
			{
				this.Settings = new FakeSettingsService(this._temp.Path);
				this.Workspace = new FakeWorkspace();
				this.Service = new FakeService();
				this.ViewModel = new AdvancedSearchViewModel(this.Workspace.Interface, this.Settings, new FakeDialogService(), new ScriptSearcher(), withService ? this.Service : null);
			}

			public FakeSettingsService Settings { get; }

			public FakeWorkspace Workspace { get; }

			public FakeService Service { get; }

			public AdvancedSearchViewModel ViewModel { get; }

			public ScriptToolsTempDirectory Temp => this._temp;

			public void Dispose()
			{
				this._temp.Dispose();
			}
		}

		[Fact]
		public void WithoutTheServiceTheCommandIsNotAvailable()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness(false);

				Assert.False(harness.ViewModel.CanReplaceInFiles);
				Assert.False(harness.ViewModel.ReplaceInFilesCommand.CanExecute(null));
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void TheFolderScopeAsksForTheFilesOfTheFolder_WithTheOpenDocumentsInPlaceOfTheirFiles()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				String inFolder = harness.Temp.Write("sub\\a.ssl", "old");
				DocumentViewModel open = harness.Workspace.AddDocument(inFolder, "unsaved old");
				harness.Workspace.AddDocument(null, "never saved old");
				harness.ViewModel.Scope = SearchScope.Folder;
				harness.ViewModel.FolderPath = harness.Temp.Path;
				harness.ViewModel.FileMask = "*.ssl";
				harness.ViewModel.SearchText = "old";
				harness.ViewModel.ReplaceText = "new";
				harness.ViewModel.MatchCase = true;

				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);

				ReplaceInFilesRequest request = Assert.Single(harness.Service.Requests);
				Assert.Equal("new", request.Replacement);
				Assert.Equal("old", request.Matcher.Options.Text);
				Assert.Equal(Path.GetFullPath(harness.Temp.Path), request.Folder!.Directory);
				Assert.Equal(new[] { "*.ssl" }, request.Folder.FileMasks.ToArray());
				Assert.True(request.Folder.IncludeSubfolders);
				Assert.Empty(request.Sources);
				ReplaceTextSource source = Assert.Single(request.OpenDocuments).Value;
				Assert.Equal((inFolder, "unsaved old"), (source.Path, source.Text));
				Assert.Same(open, source.Owner);
			});
		}

		[Fact]
		public void TheOpenScriptsScopeAsksForTheTextOfEveryDocument_TheCurrentScopeForTheActiveOne()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(@"C:\m\a.ssl", "old a");
				DocumentViewModel second = harness.Workspace.AddDocument(null, "old b");
				harness.Workspace.ActiveDocument = second;
				harness.ViewModel.SearchText = "old";
				harness.ViewModel.ReplaceText = "x";

				harness.ViewModel.IsOpenScope = true;
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				harness.ViewModel.IsCurrentScope = true;
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);

				Assert.Equal(2, harness.Service.Requests.Count);
				Assert.Equal(new[] { @"C:\m\a.ssl", FalloutScriptStudio.Core.Compilation.BuildError.NoFile }, harness.Service.Requests[0].Sources.Select(item => item.Path).ToArray());
				ReplaceTextSource current = Assert.Single(harness.Service.Requests[1].Sources);
				Assert.Equal(("old b", FalloutScriptStudio.Core.Compilation.BuildError.NoFile), (current.Text, current.Path));
				Assert.Same(second, current.Owner);
			});
		}

		[Fact]
		public void RegexAndWholeWordOptionsReachTheMatcher()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(@"C:\m\a.ssl", "x");
				harness.ViewModel.SearchText = @"set\((\w+)\)";
				harness.ViewModel.ReplaceText = "put($1)";
				harness.ViewModel.UseRegex = true;
				harness.ViewModel.WholeWord = true;

				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);

				SearchOptions options = Assert.Single(harness.Service.Requests).Matcher.Options;
				Assert.True(options.UseRegex);
				Assert.False(options.WholeWord);
			});
		}

		[Fact]
		public void NothingToDoWithoutTextOrScripts_ExplainsInsteadOfCalling()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();

				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				harness.ViewModel.SearchText = "old";
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);

				Assert.Empty(harness.Service.Requests);
				Assert.True(harness.ViewModel.HasError);
			});
		}

		[Fact]
		public void AFolderThatDoesNotExistIsAnError()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.Scope = SearchScope.Folder;
				harness.ViewModel.FolderPath = Path.Combine(harness.Temp.Path, "missing");
				harness.ViewModel.SearchText = "old";

				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);

				Assert.Empty(harness.Service.Requests);
				Assert.True(harness.ViewModel.HasError);
			});
		}

		[Fact]
		public void TheStatusTellsHowTheRunEnded()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(@"C:\m\a.ssl", "old");
				harness.ViewModel.SearchText = "old";
				ReplacePlan plan = new ReplacePlan(Array.Empty<ReplaceFilePlan>(), Array.Empty<ReplaceSkip>(), 1, false, System.Text.Encoding.UTF8);

				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				Assert.Equal(FalloutScriptStudio.Localization.Loc.Get("Str.ScriptTools.SearchNotFound"), harness.ViewModel.StatusText);

				harness.Service.Answer = _ => new ReplaceOutcome(ReplaceOutcomeKind.Cancelled, plan, null);
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				Assert.Equal("Replace in Files was cancelled.", harness.ViewModel.StatusText);

				harness.Service.Answer = _ => new ReplaceOutcome(ReplaceOutcomeKind.Applied, plan, new ReplaceResult(5, 2, new[] { new ReplaceFailure("x", ReplaceFailureReason.Changed, String.Empty, 1) }, null, false));
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				Assert.Equal("5 replacement(s) in 2 file(s). 1 file(s) were not changed: see the problem list.", harness.ViewModel.StatusText);
				Assert.False(harness.ViewModel.IsSearching);
			});
		}

		[Fact]
		public void ARegexThatTimesOutIsReportedAsAnError_AndACancelledRunSaysSo()
		{
			StaTestRunner.Run(async () =>
			{
				using Harness harness = new Harness();
				harness.Workspace.AddDocument(@"C:\m\a.ssl", "old");
				harness.ViewModel.SearchText = "old";

				harness.Service.Failure = new System.Text.RegularExpressions.RegexMatchTimeoutException();
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				Assert.True(harness.ViewModel.HasError);

				harness.Service.Failure = new OperationCanceledException();
				await harness.ViewModel.ReplaceInFilesCommand.ExecuteAsync(null);
				Assert.Equal(FalloutScriptStudio.Localization.Loc.Get("Str.ScriptTools.SearchCancelled"), harness.ViewModel.StatusText);
				Assert.False(harness.ViewModel.IsSearching);
			});
		}

		[Fact]
		public void TheCommandOfTheMenuChoosesTheFolderScopeWhenAFolderIsKnown()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.FolderPath = harness.Temp.Path;

				harness.ViewModel.PrepareReplaceInFiles();

				Assert.True(harness.ViewModel.IsFolderScope);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void WithoutAKnownFolderTheScopeStaysAsItWas()
		{
			StaTestRunner.Run(() =>
			{
				using Harness harness = new Harness();
				harness.ViewModel.FolderPath = String.Empty;

				harness.ViewModel.PrepareReplaceInFiles();

				Assert.True(harness.ViewModel.IsOpenScope);
				return Task.CompletedTask;
			});
		}
	}
}