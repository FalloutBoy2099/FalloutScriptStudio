namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class SnippetManagerViewModelTests
	{
		private static SnippetDefinition Define(String trigger, String body = "x$0", Boolean enabled = true)
		{
			return new SnippetDefinition { Trigger = trigger, Description = "d " + trigger, Body = body, Enabled = enabled };
		}

		private static SnippetManagerViewModel Create(FakeSnippetService service, FakeDialogService? dialogs = null, IFunctionCatalogService? functions = null)
		{
			return new SnippetManagerViewModel(service, dialogs ?? new FakeDialogService(), functions);
		}

		private static SnippetRowViewModel Row(SnippetManagerViewModel viewModel, SnippetSource source, String trigger)
		{
			return viewModel.Rows.Single(row => row.Source == source && row.Trigger == trigger);
		}

		[Fact]
		public void TheListHoldsTheProjectsThenTheUsersThenTheBuiltInSnippets()
		{
			FakeSnippetService service = new FakeSnippetService { ProjectFilePath = @"C:\mod\fss-snippets.json" };
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("userb"), Define("usera"));
			service.SetFile(SnippetSource.Project, SnippetFileStatus.Loaded, Define("proj"));

			SnippetManagerViewModel viewModel = Create(service);

			Assert.True(viewModel.HasProject);
			Assert.Equal(new[] { SnippetSource.Project, SnippetSource.User, SnippetSource.User }, viewModel.Rows.Take(3).Select(row => row.Source));
			Assert.Equal(BuiltInSnippets.All.Count, viewModel.Rows.Count(row => row.IsBuiltIn));
			Assert.Equal("proj", viewModel.SelectedRow!.Trigger);
			Assert.False(viewModel.HasChanges);
			Assert.Equal(2, viewModel.TargetChoices.Count);
		}

		[Fact]
		public void WithoutAProject_OnlyTheUserCanHaveNewSnippets()
		{
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService());

			Assert.False(viewModel.HasProject);
			Assert.Equal(SnippetSource.User, Assert.Single(viewModel.TargetChoices).Value);
			viewModel.Target = SnippetSource.Project;
			viewModel.AddCommand.Execute(null);
			Assert.Equal(SnippetSource.User, viewModel.SelectedRow!.Source);
		}

		[Fact]
		public void TheBuiltInSnippets_CannotBeEditedOrDeleted()
		{
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService());
			SnippetRowViewModel builtIn = viewModel.Rows.First(row => row.IsBuiltIn);
			viewModel.SelectedRow = builtIn;
			String body = builtIn.Body;

			builtIn.Body = "changed";
			builtIn.Trigger = "changed";

			Assert.Equal(body, builtIn.Body);
			Assert.NotEqual("changed", builtIn.Trigger);
			Assert.False(viewModel.IsEditorEnabled);
			Assert.False(viewModel.DeleteCommand.CanExecute(null));
			Assert.True(viewModel.CustomizeCommand.CanExecute(null));
			Assert.False(viewModel.HasChanges);
		}

		[Fact]
		public void Add_CreatesAFreeTrigger_InTheChosenFile_AndSelectsTheSnippet()
		{
			FakeSnippetService service = new FakeSnippetService { ProjectFilePath = @"C:\mod\fss-snippets.json" };
			SnippetManagerViewModel viewModel = Create(service);

			viewModel.AddCommand.Execute(null);
			viewModel.AddCommand.Execute(null);
			viewModel.Target = SnippetSource.Project;
			viewModel.AddCommand.Execute(null);

			Assert.Equal(new[] { "snippet", "snippet2" }, viewModel.Rows.Where(row => row.Source == SnippetSource.User).Select(row => row.Trigger).OrderBy(trigger => trigger));
			Assert.Equal((SnippetSource.Project, "snippet"), (viewModel.SelectedRow!.Source, viewModel.SelectedRow.Trigger));
			Assert.True(viewModel.HasChanges);
			Assert.False(viewModel.HasBlockingProblems);
		}

		[Fact]
		public void TheTriggersAreChecked_OneByOne_AndTheSaveWaitsForTheFix()
		{
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService());
			viewModel.AddCommand.Execute(null);
			SnippetRowViewModel first = viewModel.SelectedRow!;
			viewModel.AddCommand.Execute(null);
			SnippetRowViewModel second = viewModel.SelectedRow!;

			first.Trigger = "same";
			second.Trigger = "SAME";
			Assert.Equal(SnippetTriggerIssue.Duplicate, first.TriggerIssue);
			Assert.Equal("Another snippet of this list has the same trigger.", second.TriggerMessage);
			Assert.True(viewModel.HasBlockingProblems);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
			Assert.Contains("Fix the triggers", viewModel.FileMessage, StringComparison.Ordinal);

			second.Trigger = "if";
			Assert.Equal(SnippetTriggerIssue.Reserved, second.TriggerIssue);
			second.Trigger = "1abc";
			Assert.Equal(SnippetTriggerIssue.InvalidCharacters, second.TriggerIssue);
			second.Trigger = String.Empty;
			Assert.Equal(SnippetTriggerIssue.Empty, second.TriggerIssue);
			second.Trigger = new String('a', SnippetValidator.MaxTriggerLength + 1);
			Assert.Equal(SnippetTriggerIssue.TooLong, second.TriggerIssue);
			Assert.Contains(SnippetValidator.MaxTriggerLength.ToString(System.Globalization.CultureInfo.InvariantCulture), second.TriggerMessage, StringComparison.Ordinal);

			second.Trigger = "other";
			Assert.False(viewModel.HasBlockingProblems);
			Assert.True(viewModel.SaveCommand.CanExecute(null));
			Assert.Equal(String.Empty, second.Message);
		}

		[Fact]
		public void ATriggerCanBeUsedOncePerFile_ButALaterFileMayReplaceAnEarlierSnippet()
		{
			FakeSnippetService service = new FakeSnippetService { ProjectFilePath = @"C:\mod\fss-snippets.json" };
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("dup"));
			service.SetFile(SnippetSource.Project, SnippetFileStatus.Loaded, Define("dup"), Define("callp"));

			SnippetManagerViewModel viewModel = Create(service);

			Assert.False(viewModel.HasBlockingProblems);
			Assert.Equal("Replaced", Row(viewModel, SnippetSource.User, "dup").StateText);
			Assert.Equal("Replaced", Row(viewModel, SnippetSource.BuiltIn, "callp").StateText);
			Assert.Equal(String.Empty, Row(viewModel, SnippetSource.Project, "dup").StateText);
		}

		[Fact]
		public void AMalformedBody_IsReportedWithoutBlockingTheSave()
		{
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService());
			viewModel.AddCommand.Execute(null);
			SnippetRowViewModel row = viewModel.SelectedRow!;

			row.Body = "if (${1:cond) then $WHAT begin ${} end";

			Assert.True(row.HasBodyProblems);
			Assert.Contains(row.BodyProblems, problem => problem.Kind == SnippetProblemKind.UnterminatedPlaceholder);
			Assert.Contains(row.BodyProblems, problem => problem.Kind == SnippetProblemKind.UnknownVariable);
			Assert.Contains("A placeholder is not closed with }", row.BodyMessage, StringComparison.Ordinal);
			Assert.Contains("Unknown variable: WHAT", row.BodyMessage, StringComparison.Ordinal);
			Assert.True(row.HasMessage);
			Assert.False(viewModel.HasBlockingProblems);

			row.Body = String.Empty;
			Assert.Equal("The body is empty.", row.BodyMessage);
			row.Body = "${1:ok}$0";
			Assert.False(row.HasMessage);
		}

		[Fact]
		public void Duplicate_CopiesTheSnippetUnderAFreeTrigger_AndABuiltInSnippetBecomesAUserSnippet()
		{
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("mine", "body$0"));
			SnippetManagerViewModel viewModel = Create(service);

			viewModel.SelectedRow = Row(viewModel, SnippetSource.User, "mine");
			viewModel.DuplicateCommand.Execute(null);
			Assert.Equal(("mine2", "body$0", SnippetSource.User), (viewModel.SelectedRow!.Trigger, viewModel.SelectedRow.Body, viewModel.SelectedRow.Source));

			viewModel.SelectedRow = Row(viewModel, SnippetSource.BuiltIn, "dispmsg");
			viewModel.DuplicateCommand.Execute(null);
			Assert.Equal(SnippetSource.User, viewModel.SelectedRow!.Source);
			Assert.Equal("dispmsg2", viewModel.SelectedRow.Trigger);
			Assert.Equal(BuiltInSnippets.All.Single(snippet => snippet.Trigger == "dispmsg").Body, viewModel.SelectedRow.Body);
			Assert.False(viewModel.HasBlockingProblems);
		}

		[Fact]
		public void Customize_MakesAUserSnippetWithTheSameTrigger_AndDeletingItBringsTheBuiltInOneBack()
		{
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService());
			viewModel.SelectedRow = Row(viewModel, SnippetSource.BuiltIn, "callp");

			viewModel.CustomizeCommand.Execute(null);

			SnippetRowViewModel copy = viewModel.SelectedRow!;
			Assert.Equal((SnippetSource.User, "callp"), (copy.Source, copy.Trigger));
			Assert.True(viewModel.IsEditorEnabled);
			Assert.Equal("Replaced", Row(viewModel, SnippetSource.BuiltIn, "callp").StateText);
			Assert.False(viewModel.HasBlockingProblems);

			viewModel.DeleteCommand.Execute(null);

			Assert.Equal(String.Empty, Row(viewModel, SnippetSource.BuiltIn, "callp").StateText);
			Assert.DoesNotContain(viewModel.Rows, row => row.Source == SnippetSource.User);
			Assert.False(viewModel.HasChanges);
		}

		[Fact]
		public void Disable_OfABuiltInSnippet_MakesADisabledUserSnippet_AndOfAUserSnippetTurnsItOff()
		{
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("mine"));
			SnippetManagerViewModel viewModel = Create(service);

			viewModel.SelectedRow = Row(viewModel, SnippetSource.BuiltIn, "def");
			viewModel.DisableCommand.Execute(null);
			Assert.False(viewModel.SelectedRow!.IsEnabled);
			Assert.Equal("Disabled", viewModel.SelectedRow.StateText);

			viewModel.SelectedRow = Row(viewModel, SnippetSource.User, "mine");
			viewModel.DisableCommand.Execute(null);
			Assert.False(Row(viewModel, SnippetSource.User, "mine").IsEnabled);
			Assert.True(viewModel.HasChanges);
		}

		[Fact]
		public void Delete_RemovesTheSnippetAndSelectsANeighbour()
		{
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("a"), Define("b"));
			SnippetManagerViewModel viewModel = Create(service);
			viewModel.SelectedRow = Row(viewModel, SnippetSource.User, "a");

			viewModel.DeleteCommand.Execute(null);

			Assert.DoesNotContain(viewModel.Rows, row => row.Trigger == "a");
			Assert.Equal("b", viewModel.SelectedRow!.Trigger);
		}

		[Fact]
		public async Task Save_WritesTheUserFileAndTheProjectFile_AndClosesWithTheChanges()
		{
			FakeSnippetService service = new FakeSnippetService { ProjectFilePath = @"C:\mod\fss-snippets.json" };
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("old"));
			SnippetManagerViewModel viewModel = Create(service);
			List<Boolean> closed = [];
			viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);
			viewModel.AddCommand.Execute(null);
			viewModel.SelectedRow!.Trigger = "fresh";
			viewModel.SelectedRow.Body = "line1\nline2$0";
			viewModel.Target = SnippetSource.Project;
			viewModel.AddCommand.Execute(null);
			viewModel.SelectedRow!.Trigger = "projectonly";

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Equal(new[] { true }, closed);
			Assert.Equal(new[] { "fresh", "old" }, service.Saved[SnippetSource.User].Select(definition => definition.Trigger).OrderBy(trigger => trigger));
			Assert.Equal("projectonly", Assert.Single(service.Saved[SnippetSource.Project]).Trigger);
			Assert.Equal("line1\nline2$0", service.Saved[SnippetSource.User].Single(definition => definition.Trigger == "fresh").Body);
		}

		[Fact]
		public async Task Save_WhenTheFileCannotBeWritten_ShowsTheErrorAndStaysOpen()
		{
			FakeSnippetService service = new FakeSnippetService { SaveError = "disk full" };
			FakeDialogService dialogs = new FakeDialogService();
			SnippetManagerViewModel viewModel = Create(service, dialogs);
			List<Boolean> closed = [];
			viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);
			viewModel.AddCommand.Execute(null);

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.Empty(closed);
			Assert.Contains("disk full", Assert.Single(dialogs.Errors), StringComparison.Ordinal);
			Assert.True(viewModel.HasChanges);
		}

		[Fact]
		public void AFileThatCannotBeRead_IsNeverOverwritten_SoTheSaveIsOff()
		{
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Unreadable);

			SnippetManagerViewModel viewModel = Create(service);

			Assert.True(viewModel.HasUnreadableFile);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
			Assert.Contains("snippets.json cannot be read", viewModel.FileMessage, StringComparison.Ordinal);
		}

		[Fact]
		public void ADamagedFile_IsReported_ButSavingIsPossible()
		{
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Damaged);

			SnippetManagerViewModel viewModel = Create(service);

			Assert.True(viewModel.SaveCommand.CanExecute(null));
			Assert.Contains("is damaged", viewModel.FileMessage, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Cancel_AsksBeforeDroppingChanges()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = false };
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService(), dialogs);
			List<Boolean> closed = [];
			viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

			await viewModel.CancelCommand.ExecuteAsync(null);
			Assert.Equal(new[] { false }, closed);

			viewModel.AddCommand.Execute(null);
			await viewModel.CancelCommand.ExecuteAsync(null);
			Assert.Single(closed);

			dialogs.ConfirmAnswer = true;
			await viewModel.CancelCommand.ExecuteAsync(null);
			Assert.Equal(new[] { false, false }, closed);
		}

		[Fact]
		public async Task ConfirmClose_AsksOnlyWhenThereAreChanges_AndRemembersTheAnswer()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = false };
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService(), dialogs);

			Assert.False(viewModel.NeedsCloseConfirmation);
			Assert.True(await viewModel.ConfirmCloseAsync());
			Assert.Equal(0, dialogs.ConfirmCount);

			viewModel.AddCommand.Execute(null);
			Assert.True(viewModel.NeedsCloseConfirmation);
			Assert.False(await viewModel.ConfirmCloseAsync());
			Assert.Equal(1, dialogs.ConfirmCount);
			Assert.True(viewModel.NeedsCloseConfirmation);

			dialogs.ConfirmAnswer = true;
			Assert.True(await viewModel.ConfirmCloseAsync());
			Assert.Equal(2, dialogs.ConfirmCount);
			Assert.False(viewModel.NeedsCloseConfirmation);

			// The window closes after the answer: closing it again (the Closing handler) must not ask a second time.
			Assert.True(await viewModel.ConfirmCloseAsync());
			Assert.Equal(2, dialogs.ConfirmCount);
		}

		[Fact]
		public async Task Cancel_AndTheCloseButtonAskTheSameQuestion_OnlyOnce()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = true };
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService(), dialogs);
			viewModel.AddCommand.Execute(null);
			List<Boolean> closed = [];
			viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

			await viewModel.CancelCommand.ExecuteAsync(null);
			Assert.Equal(new[] { false }, closed);
			Assert.Equal(1, dialogs.ConfirmCount);

			// CloseRequested closes the window, whose Closing handler asks the view model again: the answer already given counts.
			Assert.False(viewModel.NeedsCloseConfirmation);
			Assert.True(await viewModel.ConfirmCloseAsync());
			Assert.Equal(1, dialogs.ConfirmCount);
		}

		[Fact]
		public async Task Import_AddsTheSnippetsOfAFile_ReplacingTheOnesWithTheSameTrigger_AndSkipsTheInvalidTriggers()
		{
			using TestFolder folder = new TestFolder("snippet-import");
			String path = folder.Write("shared.json", "{\"version\":1,\"snippets\":[{\"trigger\":\"mine\",\"body\":[\"new\",\"body\"]},{\"trigger\":\"fresh\",\"body\":\"f\"},{\"trigger\":\"if\",\"body\":\"x\"},{\"trigger\":\"\",\"body\":\"y\"}]}");
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("mine", "old"));
			FakeDialogService dialogs = new FakeDialogService();
			dialogs.FileAnswers.Enqueue(path);
			SnippetManagerViewModel viewModel = Create(service, dialogs);

			await viewModel.ImportCommand.ExecuteAsync(null);

			Assert.Equal("new\nbody", Row(viewModel, SnippetSource.User, "mine").Body);
			Assert.Equal("f", Row(viewModel, SnippetSource.User, "fresh").Body);
			Assert.DoesNotContain(viewModel.Rows, row => row.Source == SnippetSource.User && row.Trigger == "if");
			Assert.Equal("2 snippet(s) imported, 2 skipped because of their trigger.", viewModel.StatusText);
			Assert.False(viewModel.HasBlockingProblems);
		}

		[Fact]
		public async Task Import_OfAFileThatIsNoSnippetFile_ShowsAnError_AndChangesNothing()
		{
			using TestFolder folder = new TestFolder("snippet-import-bad");
			String path = folder.Write("bad.json", "this is not json");
			FakeDialogService dialogs = new FakeDialogService();
			dialogs.FileAnswers.Enqueue(path);
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService(), dialogs);

			await viewModel.ImportCommand.ExecuteAsync(null);

			Assert.Contains("bad.json is not a valid snippet file", Assert.Single(dialogs.Errors), StringComparison.Ordinal);
			Assert.False(viewModel.HasChanges);
		}

		[Fact]
		public async Task Export_WritesTheSnippetsOfTheChosenFile_ToASnippetFileThatImportsBack()
		{
			using TestFolder folder = new TestFolder("snippet-export");
			String path = folder.Combine("out", "mine.json");
			FakeSnippetService service = new FakeSnippetService();
			service.SetFile(SnippetSource.User, SnippetFileStatus.Loaded, Define("one", "a\nb$0"), Define("two", "c"));
			FakeDialogService dialogs = new FakeDialogService();
			dialogs.SaveAnswers.Enqueue(path);
			SnippetManagerViewModel viewModel = Create(service, dialogs);

			await viewModel.ExportCommand.ExecuteAsync(null);

			Assert.True(SnippetFileStore.TryParse(File.ReadAllBytes(path), out SnippetFile? file, out _));
			Assert.Equal(new[] { "one", "two" }, file!.Snippets.Select(definition => definition.Trigger).OrderBy(trigger => trigger));
			Assert.Equal("a\nb$0", file.Snippets.Single(definition => definition.Trigger == "one").Body);
			Assert.Equal("2 snippet(s) exported.", viewModel.StatusText);
		}

		[Fact]
		public void ImportFunctions_MakesUserSnippetsOfTheCodeOfTheUsersFunctions()
		{
			FunctionNode mine = new FunctionNode(FunctionNodeKind.Function, "Heal Player", FunctionSource.User) { Description = "Heals", Code = "heal({amount})<cr>heal({amount});" };
			FunctionNode builtIn = new FunctionNode(FunctionNodeKind.Function, "Built In", FunctionSource.BuiltIn) { Code = "nothing" };
			IFunctionCatalogService functions = FakeFunctions.Create(new FunctionCatalog(new[] { mine, builtIn }, Array.Empty<FunctionCatalogIssue>()));
			SnippetManagerViewModel viewModel = Create(new FakeSnippetService(), null, functions);

			viewModel.ImportFunctionsCommand.Execute(null);

			SnippetRowViewModel row = Assert.Single(viewModel.Rows, candidate => candidate.Source == SnippetSource.User);
			Assert.Equal(("healplayer", "Heals", "heal(${1:amount})\nheal(${1:amount});"), (row.Trigger, row.Description, row.Body));
			Assert.Equal("1 snippet(s) made from the functions.", viewModel.StatusText);
			Assert.False(Create(new FakeSnippetService()).ImportFunctionsCommand.CanExecute(null));
		}

		/// <summary>A functions service that only knows its catalog.</summary>
		private class FakeFunctions : DispatchProxy
		{
			private FunctionCatalog _catalog = FunctionCatalog.Empty;

			public static IFunctionCatalogService Create(FunctionCatalog catalog)
			{
				IFunctionCatalogService service = DispatchProxy.Create<IFunctionCatalogService, FakeFunctions>();
				((FakeFunctions)(Object)service)._catalog = catalog;
				return service;
			}

			protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
			{
				return targetMethod?.Name == "get_" + nameof(IFunctionCatalogService.Catalog) ? this._catalog : null;
			}
		}
	}
}