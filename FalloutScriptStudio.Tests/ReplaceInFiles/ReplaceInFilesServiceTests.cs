namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;

	/// <summary>The three steps together: the plan, the choice in the preview, the replacement; the problems it reports and its undo.</summary>
	public sealed class ReplaceInFilesServiceTests
	{
		private sealed class FakePresenter : IReplacePreviewPresenter
		{
			public Func<ReplacePlan, IReadOnlyList<ReplaceSelection>?> Answer { get; set; } = plan => plan.Files.Select(file => new ReplaceSelection(file, file.Matches)).ToList();

			public List<ReplacePlan> Plans { get; } = [];

			public List<ReplaceInFilesRequest> Requests { get; } = [];

			public Task<IReadOnlyList<ReplaceSelection>?> ShowAsync(ReplacePlan plan, ReplaceInFilesRequest request)
			{
				this.Plans.Add(plan);
				this.Requests.Add(request);
				return Task.FromResult(this.Answer(plan));
			}
		}

		private sealed class Harness
		{
			public Harness()
			{
				this.Workspace = new FakeWorkspace();
				this.Problems = new ProblemService();
				this.Presenter = new FakePresenter();
				this.Service = new ReplaceInFilesService(new ReplaceInFilesPlanner(), new ReplaceInFilesApplier(this.Workspace.Interface), this.Presenter, this.Workspace.Interface, this.Problems);
			}

			public FakeWorkspace Workspace { get; }

			public ProblemService Problems { get; }

			public FakePresenter Presenter { get; }

			public ReplaceInFilesService Service { get; }
		}

		[Fact]
		public async Task TheThreeStepsReplaceWhatWasTicked_AndTheStatusSaysHowMany()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String a = folder.Write("a.ssl", "old old\r\n");
			String b = folder.Write("b.ssl", "old\r\n");
			Harness harness = new Harness();
			harness.Presenter.Answer = plan => new[] { new ReplaceSelection(plan.Files[0], plan.Files[0].Matches.Take(1).ToList()) };

			ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Assert.Equal(ReplaceOutcomeKind.Applied, outcome.Kind);
			Assert.Equal("new old\r\n", File.ReadAllText(a));
			Assert.Equal("old\r\n", File.ReadAllText(b));
			Assert.Equal(3, outcome.Plan.MatchCount);
			Assert.Equal("1 replacement(s) in 1 file(s).", Assert.Single(harness.Workspace.Statuses));
		}

		[Fact]
		public async Task TheUserSeesThePlanAndTheRequest()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			folder.Write("a.ssl", "old");
			Harness harness = new Harness();
			ReplaceInFilesRequest request = ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new");

			await harness.Service.RunAsync(request, null, CancellationToken.None);

			Assert.Same(request, Assert.Single(harness.Presenter.Requests));
			Assert.Single(Assert.Single(harness.Presenter.Plans).Files);
		}

		[Fact]
		public async Task NothingFound_ShowsNoPreview()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			folder.Write("a.ssl", "nothing");
			Harness harness = new Harness();

			ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Assert.Equal(ReplaceOutcomeKind.NothingFound, outcome.Kind);
			Assert.Empty(harness.Presenter.Plans);
			Assert.False(harness.Service.CanUndo);
		}

		[Fact]
		public async Task ACancelledPreviewChangesNothing()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			Harness harness = new Harness();
			harness.Presenter.Answer = _ => null;

			ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Assert.Equal(ReplaceOutcomeKind.Cancelled, outcome.Kind);
			Assert.Equal("old", File.ReadAllText(path));
			Assert.False(harness.Service.CanUndo);
			Assert.Empty(harness.Workspace.Statuses);
		}

		[Fact]
		public async Task APreviewWithNothingTickedIsACancel()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			Harness harness = new Harness();
			harness.Presenter.Answer = _ => Array.Empty<ReplaceSelection>();

			ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Assert.Equal(ReplaceOutcomeKind.Cancelled, outcome.Kind);
			Assert.Equal("old", File.ReadAllText(path));
		}

		[Fact]
		public async Task AReadOnlyFileBecomesAProblem_BeforeThePreview_AndTheOthersAreOffered()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String readOnly = folder.Write("ro.ssl", "old old");
			folder.Write("rw.ssl", "old");
			File.SetAttributes(readOnly, FileAttributes.ReadOnly);
			Harness harness = new Harness();
			try
			{
				ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

				Assert.Equal(ReplaceOutcomeKind.Applied, outcome.Kind);
				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal((ProblemSeverity.Warning, ProblemSource.File, ProblemOrigins.ReplaceInFiles, readOnly), (problem.Severity, problem.Source, problem.Origin, problem.File));
				Assert.Equal("Str.Shell.Problem.ReplaceReadOnly", problem.MessageKey);
				Assert.Equal(2, problem.MessageArguments[1]);
				Assert.Equal("old old", File.ReadAllText(readOnly));
			}
			finally
			{
				File.SetAttributes(readOnly, FileAttributes.Normal);
			}
		}

		[Fact]
		public async Task AFileThatChangedBeforeTheReplacementBecomesAProblem_AndTheStatusMentionsIt()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String changed = folder.Write("a.ssl", "old");
			folder.Write("b.ssl", "old");
			Harness harness = new Harness();
			harness.Presenter.Answer = plan =>
			{
				File.WriteAllText(changed, "edited while the preview was open");
				return plan.Files.Select(file => new ReplaceSelection(file, file.Matches)).ToList();
			};

			ReplaceOutcome outcome = await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Problem problem = Assert.Single(harness.Problems.Problems);
			Assert.Equal("Str.Shell.Problem.ReplaceChanged", problem.MessageKey);
			Assert.Equal(changed, problem.File);
			Assert.Equal("1 replacement(s) in 1 file(s). 1 file(s) were not changed: see the problem list.", Assert.Single(harness.Workspace.Statuses));
			Assert.Single(outcome.Result!.Failures);
		}

		[Fact]
		public async Task TheProblemsOfTheLastRunAreReplacedByTheNextRun()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String readOnly = folder.Write("ro.ssl", "old");
			File.SetAttributes(readOnly, FileAttributes.ReadOnly);
			Harness harness = new Harness();
			try
			{
				await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);
				Assert.Single(harness.Problems.Problems);
			}
			finally
			{
				File.SetAttributes(readOnly, FileAttributes.Normal);
			}

			await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);

			Assert.Empty(harness.Problems.Problems);
		}

		[Fact]
		public async Task UndoLastPutsTheFilesBack_OnceOnly()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old\r\n");
			Harness harness = new Harness();
			Int32 changes = 0;
			harness.Service.UndoStateChanged += (_, _) => changes++;
			await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);
			Assert.True(harness.Service.CanUndo);

			ReplaceUndoResult? undo = await harness.Service.UndoLastAsync(CancellationToken.None);

			Assert.Equal("old\r\n", File.ReadAllText(path));
			Assert.Equal(1, undo!.Restored);
			Assert.False(harness.Service.CanUndo);
			Assert.Equal(2, changes);
			Assert.Equal("Restored 1 file(s).", harness.Workspace.Statuses.Last());
			Assert.Null(await harness.Service.UndoLastAsync(CancellationToken.None));
		}

		[Fact]
		public async Task UndoOfAFileEditedSinceBecomesAProblem()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old\r\n");
			Harness harness = new Harness();
			await harness.Service.RunAsync(ReplaceInFilesPlannerTests.FolderRequest(folder.Path, "old", "new"), null, CancellationToken.None);
			File.AppendAllText(path, "// mine");

			ReplaceUndoResult? undo = await harness.Service.UndoLastAsync(CancellationToken.None);

			Assert.Equal(0, undo!.Restored);
			Problem problem = Assert.Single(harness.Problems.Problems);
			Assert.Equal("Str.Shell.Problem.ReplaceChanged", problem.MessageKey);
			Assert.Equal("Restored 0 file(s); 1 could not be restored: see the problem list.", harness.Workspace.Statuses.Last());
		}

		[Fact]
		public async Task ARegexThatTimesOutSurfacesToTheCaller()
		{
			Harness harness = new Harness();
			ReplaceInFilesRequest request = new ReplaceInFilesRequest
			{
				Matcher = ReplaceInFilesPlannerTests.Matcher(@"^(a+)+$", true, false, true, TimeSpan.FromMilliseconds(100)),
				Replacement = "x",
				Sources = new[] { new ReplaceTextSource("a", new String('a', 40) + "!", new Object()) },
			};

			await Assert.ThrowsAsync<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => harness.Service.RunAsync(request, null, CancellationToken.None));
		}

		[Fact]
		public async Task TheProblemFactoryKeysExistInEnglishForEveryReason()
		{
			foreach (ReplaceFailureReason reason in Enum.GetValues<ReplaceFailureReason>())
			{
				Problem problem = ProblemFactory.ReplaceFileSkipped(@"C:\mods\a.ssl", reason, 3, "boom");

				Assert.True(FalloutScriptStudio.Localization.Loc.TryGet(problem.MessageKey!, out String? message), problem.MessageKey);
				Assert.True(FalloutScriptStudio.Localization.Loc.TryGet(problem.HintKey!, out _), problem.HintKey);
				Assert.DoesNotContain("{3}", message, StringComparison.Ordinal);
			}

			await Task.CompletedTask;
		}
	}
}