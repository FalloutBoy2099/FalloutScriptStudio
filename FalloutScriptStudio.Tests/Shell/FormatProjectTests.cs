namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Formatting;
	using Xunit;

	/// <summary>"Format Project": the preview, the safe application of the formatting to files and open documents, and the window's view model.</summary>
	public sealed class FormatProjectTests
	{
		private const String Messy = "procedure start begin\r\n  variable x:=1+2;\r\n  display_msg(\"a+b\");\r\nend\r\n";
		private const String Clean = "procedure start begin\r\n  variable x := 1 + 2;\r\n  display_msg(\"a+b\");\r\nend\r\n";

		private static ProjectFormatService CreateService(ShellHarness harness)
		{
			return new ProjectFormatService(harness.Settings, harness.Workspace, null, harness.Problems);
		}

		private static FormatProjectViewModel CreateViewModel(ShellHarness harness)
		{
			return new FormatProjectViewModel(CreateService(harness), harness.Operations, harness.Settings, harness.Workspace);
		}

		private static void SetProject(ShellHarness harness, String folder)
		{
			harness.Settings.Update(settings => settings.Paths.SolutionFolder = folder);
		}

		[Fact]
		public void Preview_OfTheOpenDocuments_ListsTheScriptsThatWouldChange()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel messy = await harness.OpenFileAsync("messy.ssl", Messy);
				await harness.OpenFileAsync("clean.ssl", Clean);
				await harness.OpenFileAsync("notes.msg", "{100}{}{a+b,c}\r\n");

				ProjectFormatPreview preview = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.OpenDocuments, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);

				FormatFileChange change = Assert.Single(preview.Changes);
				Assert.Equal(messy.FilePath, change.Path);
				Assert.Equal(1, change.ChangedLines);
				Assert.True(change.IsOpen);
				Assert.Equal(2, preview.Examined);
				Assert.Equal(1, preview.Unchanged);
				Assert.False(preview.Cancelled);
			});
		}

		[Fact]
		public void Preview_OfTheProjectFolder_FindsTheFilesByMask_AndNamesThemRelativeToTheProject()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Folder.Write(@"mod\scripts\a.ssl", Messy);
				harness.Folder.Write(@"mod\scripts\b.ssl", Clean);
				harness.Folder.Write(@"mod\headers\c.h", "#define A 1+2\r\n");
				harness.Folder.Write(@"mod\text\d.msg", "{100}{}{a+b}\r\n");
				harness.Folder.Write(@"mod\scripts\e.int", "not text");
				SetProject(harness, harness.Folder.Combine("mod"));

				ProjectFormatPreview all = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl;*.h"), null, CancellationToken.None);
				ProjectFormatPreview onlyHeaders = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.h"), null, CancellationToken.None);

				Assert.Equal(new[] { @"headers\c.h", @"scripts\a.ssl" }, all.Changes.Select(change => change.DisplayName).ToArray());
				Assert.Equal(3, all.Examined);
				Assert.Equal(@"headers\c.h", Assert.Single(onlyHeaders.Changes).DisplayName);
			});
		}

		[Fact]
		public void Preview_UsesTheTextOfAnOpenDocument_NotTheOneOnDisk()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Write(@"mod\scripts\a.ssl", Clean);
				SetProject(harness, harness.Folder.Combine("mod"));
				DocumentViewModel document = (await harness.Workspace.OpenAsync(path, new OpenDocumentOptions()))!;
				document.TextDocument.Text = Messy;

				ProjectFormatPreview preview = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);

				Assert.True(Assert.Single(preview.Changes).IsOpen);
			});
		}

		[Fact]
		public void Preview_ReportsAFileThatIsTooBig_AndGoesOn()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Folder.Write(@"mod\a.ssl", Messy);
				harness.Folder.Write(@"mod\big.ssl", Messy + new String('/', 200));
				SetProject(harness, harness.Folder.Combine("mod"));

				ProjectFormatPreview preview = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl") { MaxFileSize = 150 }, null, CancellationToken.None);

				Assert.Equal("a.ssl", Assert.Single(preview.Changes).DisplayName);
				Assert.Equal(1, preview.Unreadable);
				Problem problem = Assert.Single(harness.Problems.Problems, p => p.Origin == ProblemOrigins.FormatProject);
				Assert.Equal("Str.Shell.Problem.FormatFailed", problem.MessageKey);
			});
		}

		[Fact]
		public void Apply_FormatsTheFilesOnDisk_KeepingTheEncodingTheByteOrderMarkAndTheLineBreaks()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Encoding ansi = Encoding.Latin1;
				String crlf = harness.Folder.Combine(@"mod\crlf.ssl");
				String lf = harness.Folder.Combine(@"mod\lf.ssl");
				String bom = harness.Folder.Combine(@"mod\bom.ssl");
				String latin = harness.Folder.Combine(@"mod\latin.ssl");
				Directory.CreateDirectory(harness.Folder.Combine("mod"));
				File.WriteAllBytes(crlf, ansi.GetBytes(Messy));
				File.WriteAllBytes(lf, ansi.GetBytes(Messy.Replace("\r\n", "\n", StringComparison.Ordinal)));
				File.WriteAllBytes(bom, new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(Messy + "// é\r\n")).ToArray());
				File.WriteAllBytes(latin, ansi.GetBytes(Messy + "// café\r\n"));
				SetProject(harness, harness.Folder.Combine("mod"));
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				Assert.Equal(4, result.Formatted);
				Assert.Equal(0, result.Skipped);
				Assert.Equal(ansi.GetBytes(Clean), File.ReadAllBytes(crlf));
				Assert.Equal(ansi.GetBytes(Clean.Replace("\r\n", "\n", StringComparison.Ordinal)), File.ReadAllBytes(lf));
				Assert.Equal(new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(Clean + "// é\r\n")).ToArray(), File.ReadAllBytes(bom));
				Assert.Equal(ansi.GetBytes(Clean + "// café\r\n"), File.ReadAllBytes(latin));
				Assert.DoesNotContain(harness.Problems.Problems, p => p.Origin == ProblemOrigins.FormatProject);
			});
		}

		[Fact]
		public void Apply_LeavesAFileAloneThatChangedSinceThePreview()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Write(@"mod\a.ssl", Messy);
				SetProject(harness, harness.Folder.Combine("mod"));
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);
				File.WriteAllText(path, Messy + "// added by another program\r\n");

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				Assert.Equal(0, result.Formatted);
				Assert.Equal(1, result.Skipped);
				Assert.Equal(Messy + "// added by another program\r\n", File.ReadAllText(path));
				Problem problem = Assert.Single(harness.Problems.Problems, p => p.Origin == ProblemOrigins.FormatProject);
				Assert.Equal("Str.Shell.Problem.FormatFileChanged", problem.MessageKey);
				Assert.Equal(path, problem.File);
			});
		}

		[Fact]
		public void Apply_SkipsAReadOnlyFileWithAProblem_AndFormatsTheOthers()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String locked = harness.Folder.Write(@"mod\locked.ssl", Messy);
				String free = harness.Folder.Write(@"mod\free.ssl", Messy);
				SetProject(harness, harness.Folder.Combine("mod"));
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);
				File.SetAttributes(locked, FileAttributes.ReadOnly);
				try
				{
					ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

					Assert.Equal(1, result.Formatted);
					Assert.Equal(1, result.Skipped);
					Assert.Equal(Messy, File.ReadAllText(locked));
					Assert.Equal(Clean, File.ReadAllText(free));
					Problem problem = Assert.Single(harness.Problems.Problems, p => p.Origin == ProblemOrigins.FormatProject);
					Assert.Equal("Str.Shell.Problem.FormatReadOnly", problem.MessageKey);
				}
				finally
				{
					File.SetAttributes(locked, FileAttributes.Normal);
				}
			});
		}

		[Fact]
		public void Apply_ChangesAnOpenDocumentThroughTheDocument_AsOneUndoStep_AndDoesNotWriteTheFile()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.OpenDocuments, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				Assert.Equal(1, result.Formatted);
				Assert.Equal(Clean, document.TextDocument.Text);
				Assert.True(document.IsModified);
				Assert.Equal(Messy, File.ReadAllText(document.FilePath!));
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Messy, document.TextDocument.Text);
				Assert.False(document.TextDocument.UndoStack.CanUndo && document.IsModified);
			});
		}

		[Fact]
		public void Apply_LeavesAnOpenDocumentAloneThatWasEditedSinceThePreview()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.OpenDocuments, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);
				document.TextDocument.Insert(0, "// typed meanwhile\r\n");

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				Assert.Equal(0, result.Formatted);
				Assert.Equal(1, result.Skipped);
				Assert.StartsWith("// typed meanwhile", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Contains("x:=1+2", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Apply_StopsWhenCancelled_AndTheFilesAlreadyDoneStayFormatted()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				for (Int32 i = 0; i < 5; i++)
				{
					harness.Folder.Write(@"mod\f" + i + ".ssl", Messy);
				}

				SetProject(harness, harness.Folder.Combine("mod"));
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);
				using CancellationTokenSource cancellation = new CancellationTokenSource();
				SynchronousProgress progress = new SynchronousProgress(report =>
				{
					if (report.Index == 2)
					{
						cancellation.Cancel();
					}
				});

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, progress, cancellation.Token);

				Assert.True(result.Cancelled);
				Assert.Equal(2, result.Formatted);
				Assert.Equal(2, preview.Changes.Count(change => File.ReadAllText(change.Path) == Clean));
				Assert.Equal(3, preview.Changes.Count(change => File.ReadAllText(change.Path) == Messy));
			});
		}

		[Fact]
		public void Preview_StopsWhenCancelled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				for (Int32 i = 0; i < 40; i++)
				{
					harness.Folder.Write(@"mod\f" + i + ".ssl", Messy);
				}

				SetProject(harness, harness.Folder.Combine("mod"));
				using CancellationTokenSource cancellation = new CancellationTokenSource();
				cancellation.Cancel();

				ProjectFormatPreview preview = await CreateService(harness).PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, cancellation.Token);

				Assert.True(preview.Cancelled);
				Assert.True(preview.Changes.Count < 40);
			});
		}

		[Fact]
		public void Apply_UsesTheStyleOfTheProject_WhenItHasOne()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Write(@"mod\a.ssl", "procedure start begin\r\nx:=1;\r\nend\r\n");
				harness.Folder.Write(@"mod\" + FalloutScriptStudio.Core.Configuration.ProjectSettings.FileName, "{ \"formatting\": { \"reindent\": true, \"begin\": \"nextLine\" } }");
				SetProject(harness, harness.Folder.Combine("mod"));
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, "*.ssl"), null, CancellationToken.None);

				await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				Assert.Equal("procedure start\r\nbegin\r\n   x := 1;\r\nend\r\n", File.ReadAllText(path));
			});
		}

		[Fact]
		public void GetDiff_ShowsWhatWouldChange_AsAUnifiedDiff()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Messy);
				ProjectFormatService service = CreateService(harness);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.OpenDocuments, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);

				String diff = await service.GetDiffAsync(preview.Changes[0], CancellationToken.None);

				Assert.Contains("-  variable x:=1+2;", diff, StringComparison.Ordinal);
				Assert.Contains("+  variable x := 1 + 2;", diff, StringComparison.Ordinal);
				Assert.StartsWith("@@ ", diff, StringComparison.Ordinal);
				Assert.NotNull(document);
			});
		}

		[Fact]
		public void TheViewModel_PreviewsListsAndFormatsTheCheckedFiles_AsAnOperationOfTheStatusBar()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String first = harness.Folder.Write(@"mod\a.ssl", Messy);
				String second = harness.Folder.Write(@"mod\b.ssl", Messy);
				SetProject(harness, harness.Folder.Combine("mod"));
				FormatProjectViewModel viewModel = CreateViewModel(harness);
				List<OperationInfo?> seen = [];
				harness.Operations.Changed += (_, _) => seen.Add(harness.Operations.Current);

				Assert.True(viewModel.IsProjectScope);
				Assert.False(viewModel.ApplyCommand.CanExecute(null));
				await viewModel.PreviewCommand.ExecuteAsync(null);

				Assert.Equal(2, viewModel.Items.Count);
				Assert.All(viewModel.Items, item => Assert.True(item.IsSelected));
				Assert.NotNull(viewModel.SelectedItem);
				Assert.Equal(2, viewModel.SelectedCount);
				Assert.True(viewModel.ApplyCommand.CanExecute(null));
				Assert.Contains(seen, info => info?.Kind == OperationKind.FormatProject);
				Assert.False(harness.Operations.IsRunning);

				viewModel.Items[1].IsSelected = false;
				Assert.Equal(1, viewModel.SelectedCount);
				await viewModel.ApplyCommand.ExecuteAsync(null);

				Assert.Equal(Clean, File.ReadAllText(first));
				Assert.Equal(Messy, File.ReadAllText(second));
				Assert.Empty(viewModel.Items);
				Assert.False(harness.Operations.IsRunning);
				Assert.False(viewModel.IsBusy);
			});
		}

		[Fact]
		public void TheViewModel_ShowsTheDiffOfTheSelectedFile()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Folder.Write(@"mod\a.ssl", Messy);
				SetProject(harness, harness.Folder.Combine("mod"));
				FormatProjectViewModel viewModel = CreateViewModel(harness);

				await viewModel.PreviewCommand.ExecuteAsync(null);
				Assert.True(await Eventually.TrueAsync(() => viewModel.DiffText.Length > 0));

				Assert.Contains("+  variable x := 1 + 2;", viewModel.DiffText, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void TheViewModel_CannotStartWhileAnotherOperationRuns()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", Messy);
				FormatProjectViewModel viewModel = CreateViewModel(harness);
				viewModel.IsProjectScope = false;
				using IOperation? other = harness.Operations.TryBegin(OperationKind.Compile, "x.ssl", 0);
				Assert.NotNull(other);

				await viewModel.PreviewCommand.ExecuteAsync(null);

				Assert.Empty(viewModel.Items);
				Assert.Contains("operation", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
			});
		}

		[Fact]
		public void TheViewModel_CanBeCancelledFromTheStatusBar()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				for (Int32 i = 0; i < 30; i++)
				{
					harness.Folder.Write(@"mod\f" + i + ".ssl", Messy);
				}

				SetProject(harness, harness.Folder.Combine("mod"));
				FormatProjectViewModel viewModel = CreateViewModel(harness);
				harness.Operations.Changed += (_, _) =>
				{
					if (harness.Operations.IsRunning)
					{
						harness.Operations.Cancel();
					}
				};

				await viewModel.PreviewCommand.ExecuteAsync(null);

				Assert.True(viewModel.Items.Count < 30);
				Assert.False(harness.Operations.IsRunning);
				Assert.False(viewModel.IsBusy);
			});
		}

		[Fact]
		public void TheScopeOfTheProjectNeedsAProjectFolder()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FormatProjectViewModel viewModel = CreateViewModel(harness);

				Assert.False(viewModel.HasProjectFolder);
				Assert.False(viewModel.IsProjectScope);
				viewModel.IsProjectScope = true;
				Assert.False(viewModel.PreviewCommand.CanExecute(null));
				viewModel.IsOpenDocumentsScope = true;
				Assert.True(viewModel.PreviewCommand.CanExecute(null));
				await Task.CompletedTask;
			});
		}

		[Fact]
		public void TheCodeMenuCommand_OpensTheWindow_AndTheExplorerAsksTheMainWindowToDoIt()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.FormatProjectCommand.ExecuteAsync(null);
				await harness.Project.FormatProjectCommand.ExecuteAsync(null);

				Assert.Equal(2, harness.ToolWindows.Calls.Count(call => call == "FormatProject"));
			});
		}

		[Fact]
		public void TheManageSnippetsCommand_OpensTheSnippetManager()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.ManageSnippetsCommand.ExecuteAsync(null);

				Assert.Equal("SnippetManager", Assert.Single(harness.ToolWindows.Calls));
			});
		}

		private sealed class SynchronousProgress : IProgress<FormatProgress>
		{
			private readonly Action<FormatProgress> _action;

			public SynchronousProgress(Action<FormatProgress> action)
			{
				this._action = action;
			}

			public void Report(FormatProgress value)
			{
				this._action(value);
			}
		}
	}
}