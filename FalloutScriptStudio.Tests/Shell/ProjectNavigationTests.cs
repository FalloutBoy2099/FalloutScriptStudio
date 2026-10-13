namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>Go to definition, declaration and references across the files of the project, and the "Go to symbol" list.</summary>
	public sealed class ProjectNavigationTests
	{
		private const String Caller =
			"#include \"..\\headers\\common.h\"\r\n" +
			"procedure start begin\r\n" +
			"   remote_proc(REMOTE_MACRO);\r\n" +
			"end\r\n";

		private const String Remote =
			"procedure remote_proc;\r\n" +
			"\r\n" +
			"procedure other begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure remote_proc(variable a) begin\r\n" +
			"   display_msg(a);\r\n" +
			"end\r\n";

		[Fact]
		public void GoToDefinition_OpensTheScriptThatDefinesAProcedureEvenWhenItIsNotIncluded()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String remote = harness.Write("project/scripts/b.ssl", Remote);
				harness.Write("project/headers/common.h", "#define REMOTE_MACRO 5\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "remote_proc", ProjectNavigationKind.Definition);

				DocumentViewModel target = Assert.IsType<DocumentViewModel>(harness.Workspace.ActiveDocument);
				Assert.Equal(remote, target.FilePath);
				Assert.Equal(6, target.ViewState.PendingLine);
				Assert.Equal(11, target.ViewState.PendingColumn);
				Assert.Equal("remote_proc".Length, target.ViewState.PendingSelectLength);
				Assert.Empty(harness.ToolWindows.SymbolRequests);
			});
		}

		[Fact]
		public void GoToDefinition_OfAMacroOfAHeaderThatTheScriptDoesNotIncludeStillWorks()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", "procedure start begin\r\n   display_msg(LIMIT);\r\nend\r\n");
				String header = harness.Write("project/headers/limits.h", "// limits\r\n#define LIMIT 7\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "LIMIT", ProjectNavigationKind.Definition);

				Assert.Equal(header, harness.Workspace.ActiveDocument?.FilePath);
				Assert.Equal(2, harness.Workspace.ActiveDocument?.ViewState.PendingLine);
			});
		}

		[Fact]
		public void SeveralCandidates_AreOfferedInAListNearestFirst_AndTheChosenOneIsOpened()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String sameFolder = harness.Write("project/scripts/b.ssl", Remote);
				String deeper = harness.Write("project/scripts/sub/deep/c.ssl", Remote);
				String elsewhere = harness.Write("project/zzz/d.ssl", Remote);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);
				harness.ToolWindows.SymbolAnswer = request => request.Candidates[1];

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "remote_proc", ProjectNavigationKind.Definition);

				SymbolPickerRequest shown = Assert.Single(harness.ToolWindows.SymbolRequests);
				Assert.Equal(SymbolPickerMode.Choose, shown.Mode);
				Assert.Equal("remote_proc", shown.Name);
				Assert.Equal(3, shown.Candidates.Count);
				Assert.Equal(sameFolder, shown.Candidates[0].File);
				Assert.Equal(new[] { sameFolder, deeper, elsewhere }.ToHashSet(StringComparer.OrdinalIgnoreCase), shown.Candidates.Select(c => c.File).ToHashSet(StringComparer.OrdinalIgnoreCase));
				Assert.Equal(shown.Candidates[1].File, harness.Workspace.ActiveDocument?.FilePath);
			});
		}

		[Fact]
		public void TheCandidateInAnIncludedHeaderComesBeforeTheOthers()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String included = harness.Write("project/headers/common.h", "#define REMOTE_MACRO 5\r\n");
				harness.Write("project/scripts/near.h", "#define REMOTE_MACRO 6\r\n");
				harness.Write("project/other/far.h", "#define REMOTE_MACRO 7\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);
				harness.ToolWindows.SymbolAnswer = request => request.Candidates[0];

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "REMOTE_MACRO", ProjectNavigationKind.Definition);

				SymbolPickerRequest shown = Assert.Single(harness.ToolWindows.SymbolRequests);
				Assert.Equal(3, shown.Candidates.Count);
				Assert.Equal(included, shown.Candidates[0].File);
				Assert.Equal("near.h", shown.Candidates[1].FileName);
				Assert.Equal(included, harness.Workspace.ActiveDocument?.FilePath);
			});
		}

		[Fact]
		public void CancellingTheListOpensNothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				harness.Write("project/scripts/b.ssl", Remote);
				harness.Write("project/scripts/c.ssl", Remote);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "remote_proc", ProjectNavigationKind.Definition);

				Assert.Single(harness.ToolWindows.SymbolRequests);
				Assert.Same(document, harness.Workspace.ActiveDocument);
				Assert.Single(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void AnUnknownNameIsReportedInTheStatusBar()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "nothing_here", ProjectNavigationKind.Definition);

				Assert.Contains("nothing_here was not found in the project.", harness.Output.Statuses);
				Assert.False(harness.Workspace.HasProjectSymbol("nothing_here"));
				Assert.True(harness.Workspace.HasProjectSymbol("START"));
			});
		}

		[Fact]
		public void GoToDeclaration_UsesTheForwardDeclarationLine()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String remote = harness.Write("project/scripts/b.ssl", Remote);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "remote_proc", ProjectNavigationKind.Declaration);

				Assert.Equal(remote, harness.Workspace.ActiveDocument?.FilePath);
				Assert.Equal(1, harness.Workspace.ActiveDocument?.ViewState.PendingLine);
			});
		}

		[Fact]
		public void GoToDefinition_PrefersTheBodyToAnImportOrAForwardDeclaration()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", "import procedure api;\r\nprocedure start begin\r\n   api();\r\nend\r\n");
				String exporter = harness.Write("project/scripts/b.ssl", "export procedure api begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "api", ProjectNavigationKind.Definition);

				Assert.Equal(exporter, harness.Workspace.ActiveDocument?.FilePath);
				Assert.Empty(harness.ToolWindows.SymbolRequests);
			});
		}

		[Fact]
		public void TheReferencesOfAMacroAreFoundInTheFilesThatIncludeItsHeader()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String second = harness.Write("project/scripts/b.ssl", "#include \"..\\headers\\common.h\"\r\nprocedure other begin\r\n   display_msg(REMOTE_MACRO);\r\nend\r\n");
				harness.Write("project/scripts/c.ssl", "procedure third begin\r\n   display_msg(REMOTE_MACRO);\r\nend\r\n");
				String header = harness.Write("project/headers/common.h", "#define REMOTE_MACRO 5\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);
				ScriptMacro macro = document.ProgramInfo!.FindMacro("REMOTE_MACRO")!;

				await harness.Workspace.ShowProjectReferencesAsync(document, macro, "REMOTE_MACRO");

				Assert.Equal(
					new[] { (header, 1), (caller, 3), (second, 3) }.Select(item => (Path.GetFileName(item.Item1), item.Item2)).ToArray(),
					harness.Output.SearchResults.Select(result => (Path.GetFileName(result.FileName), result.Line)).ToArray());
				Assert.Contains("REMOTE_MACRO", harness.Output.SearchTitle ?? String.Empty, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void TheReferencesOfAnExportedProcedureAreFoundInEveryScript()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String exporter = harness.Write("project/scripts/b.ssl", "export procedure api begin\r\nend\r\n");
				String importer = harness.Write("project/other/a.ssl", "import procedure api;\r\nprocedure start begin\r\n   api();\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(exporter);
				IScriptSymbol symbol = document.ProgramInfo!.GetProcedure("api")!;

				await harness.Workspace.ShowProjectReferencesAsync(document, symbol, "api");

				Assert.Contains(harness.Output.SearchResults, result => String.Equals(result.FileName, importer, StringComparison.OrdinalIgnoreCase) && result.Line == 3);
			});
		}

		[Fact]
		public void TheReferencesOfAnUnknownNameCanBeListedFromTheIndex()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String caller = harness.Write("project/scripts/a.ssl", Caller);
				String remote = harness.Write("project/scripts/b.ssl", Remote);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(caller);

				await harness.Workspace.NavigateToProjectSymbolAsync(document, "other", ProjectNavigationKind.References);

				Assert.Contains(harness.Output.SearchResults, result => String.Equals(result.FileName, remote, StringComparison.OrdinalIgnoreCase) && result.Line == 3);
			});
		}

		[Fact]
		public void GoToSymbol_ShowsTheListOfTheProjectAndOpensTheChosenSymbol()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/scripts/a.ssl", Caller);
				String remote = harness.Write("project/scripts/b.ssl", Remote);
				await harness.OpenProjectAsync("project");
				harness.ToolWindows.SymbolAnswer = request => request.Index!.Search("other", 5).First();

				await harness.Workspace.GoToSymbolAsync();

				SymbolPickerRequest shown = Assert.Single(harness.ToolWindows.SymbolRequests);
				Assert.Equal(SymbolPickerMode.Search, shown.Mode);
				Assert.Same(harness.Index, shown.Index);
				Assert.Equal(harness.Projects.Folder, shown.RootFolder);
				Assert.Equal(remote, harness.Workspace.ActiveDocument?.FilePath);
				Assert.Equal(3, harness.Workspace.ActiveDocument?.ViewState.PendingLine);
				Assert.Equal(11, harness.Workspace.ActiveDocument?.ViewState.PendingColumn);
			});
		}

		[Fact]
		public void GoToSymbol_Cancelled_OpensNothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/scripts/a.ssl", Caller);
				await harness.OpenProjectAsync("project");

				await harness.Workspace.GoToSymbolAsync();

				Assert.Empty(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void TheSymbolListOpensTheFileOfASymbolOfAnOpenDocumentWithoutReadingTheDisk()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String remote = harness.Write("project/scripts/b.ssl", Remote);
				await harness.OpenProjectAsync("project");
				DocumentViewModel open = await harness.OpenAsync(remote);
				FakeEditorSurface editor = harness.AttachEditor(open);
				harness.ToolWindows.SymbolAnswer = request => request.Index!.Find("other").First();

				await harness.Workspace.GoToSymbolAsync();

				Assert.Same(open, harness.Workspace.ActiveDocument);
				Assert.Equal((3, 11, 5), editor.LastGoTo);
			});
		}
	}
}
