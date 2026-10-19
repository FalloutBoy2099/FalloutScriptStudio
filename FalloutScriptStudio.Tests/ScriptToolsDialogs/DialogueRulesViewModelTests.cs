namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Xunit;

	public sealed class DialogueRulesViewModelTests
	{
		private static DialogueFunctionRule NOption()
		{
			return DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3);
		}

		private static async Task<DialogueRulesViewModel> CreateAsync(FakeDialogueRulesService rules, FakeDialogService? dialogs = null)
		{
			DialogueRulesViewModel viewModel = new DialogueRulesViewModel(rules, dialogs ?? new FakeDialogService());
			await viewModel.LoadAsync();
			return viewModel;
		}

		[Fact]
		public async Task Load_ShowsOnlyUserRulesWithOneBasedPositions()
		{
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService(NOption()));

			DialogueRuleItemViewModel row = Assert.Single(viewModel.Rules);
			Assert.Equal("Option", row.Kind);
			Assert.Equal("NOption", row.Name);
			Assert.Equal(3, row.TotalArgs);
			Assert.Equal(1, row.MessageArg);
			Assert.Equal(2, row.NodeArg);
			Assert.Equal(3, row.IqArg);
			Assert.Equal(0, row.FileArg);
			Assert.False(viewModel.IsModified);
			Assert.False(viewModel.HasError);
			Assert.Contains("gsay_reply", viewModel.BuiltInSummary);
		}

		[Fact]
		public async Task AddRule_AddsAnEmptyNamedRowThatBlocksSaving()
		{
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService());

			viewModel.AddRuleCommand.Execute(null);

			Assert.Single(viewModel.Rules);
			Assert.Same(viewModel.Rules[0], viewModel.SelectedRule);
			Assert.True(viewModel.HasError);
			Assert.StartsWith("Row 1:", viewModel.ErrorMessage);
			Assert.True(viewModel.Rules[0].HasError);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
			Assert.True(viewModel.IsModified);
		}

		[Fact]
		public async Task Editing_RevalidatesLive()
		{
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService());
			viewModel.AddRuleCommand.Execute(null);

			viewModel.Rules[0].Name = "MyOption";

			Assert.False(viewModel.HasError);
			Assert.True(viewModel.SaveCommand.CanExecute(null));

			viewModel.Rules[0].NodeArg = 9;

			Assert.True(viewModel.HasError);
			Assert.False(viewModel.SaveCommand.CanExecute(null));
		}

		[Fact]
		public async Task DuplicateNames_AreFlaggedCaseInsensitivelyOnTheLaterRow()
		{
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService(NOption()));
			viewModel.AddRuleCommand.Execute(null);

			viewModel.Rules[1].Name = "noption";

			Assert.False(viewModel.Rules[0].HasError);
			Assert.True(viewModel.Rules[1].HasError);
			Assert.Contains("already", viewModel.Rules[1].Error);

			viewModel.Rules[1].Name = "other";

			Assert.False(viewModel.HasError);
		}

		[Fact]
		public async Task BuiltInNames_AreRejected()
		{
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService());
			viewModel.AddRuleCommand.Execute(null);

			viewModel.Rules[0].Name = "gsay_reply";

			Assert.True(viewModel.HasError);
		}

		[Fact]
		public async Task RemoveRule_RemovesTheSelectedRowAndSelectsANeighbour()
		{
			FakeDialogueRulesService rules = new FakeDialogueRulesService(
				NOption(),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "NReply", messageArg: 1));
			DialogueRulesViewModel viewModel = await CreateAsync(rules);
			viewModel.SelectedRule = viewModel.Rules[0];

			viewModel.RemoveRuleCommand.Execute(null);

			DialogueRuleItemViewModel left = Assert.Single(viewModel.Rules);
			Assert.Equal("NReply", left.Name);
			Assert.Same(left, viewModel.SelectedRule);
			Assert.True(viewModel.IsModified);

			viewModel.RemoveRuleCommand.Execute(null);

			Assert.Empty(viewModel.Rules);
			Assert.Null(viewModel.SelectedRule);
			Assert.False(viewModel.RemoveRuleCommand.CanExecute(null));
		}

		[Fact]
		public async Task Save_WritesTheRulesInGridOrderAndCloses()
		{
			FakeDialogueRulesService rules = new FakeDialogueRulesService(NOption());
			DialogueRulesViewModel viewModel = await CreateAsync(rules);
			viewModel.AddRuleCommand.Execute(null);
			DialogueRuleItemViewModel added = viewModel.Rules[1];
			added.Name = "NCall";
			added.Kind = "Call";
			added.TotalArgs = 1;
			added.MessageArg = 0;
			added.NodeArg = 1;
			added.IqArg = 0;
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.True(closed);
			Assert.True(viewModel.IsSaved);
			Assert.Equal(1, rules.SaveCount);
			Assert.Equal(new[] { "NOption", "NCall" }, rules.Stored.Select(r => r.Name).ToArray());
			DialogueFunctionRule call = rules.Stored[1];
			Assert.Equal(DialogueOpcode.CallMacro, call.Opcode);
			Assert.Equal(-1, call.MessageArg);
			Assert.Equal(0, call.NodeArg);
			Assert.Equal(-1, call.IqArg);
			Assert.Equal(-1, call.MessageFileArg);
			Assert.False(call.IsDefault);
		}

		[Fact]
		public async Task Save_WithoutChanges_ClosesAsCancelledAndDoesNotWrite()
		{
			FakeDialogueRulesService rules = new FakeDialogueRulesService(NOption());
			DialogueRulesViewModel viewModel = await CreateAsync(rules);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.False(closed);
			Assert.Equal(0, rules.SaveCount);
		}

		[Fact]
		public async Task Save_WhenTheFileCannotBeWritten_ReportsAndStaysOpen()
		{
			FakeDialogueRulesService rules = new FakeDialogueRulesService { FailOnSave = true };
			FakeDialogService dialogs = new FakeDialogService();
			DialogueRulesViewModel viewModel = await CreateAsync(rules, dialogs);
			viewModel.AddRuleCommand.Execute(null);
			viewModel.Rules[0].Name = "MyOption";
			Boolean closeRaised = false;
			viewModel.CloseRequested += (_, _) => closeRaised = true;

			await viewModel.SaveCommand.ExecuteAsync(null);

			Assert.False(closeRaised);
			Assert.False(viewModel.IsSaved);
			Assert.False(viewModel.IsBusy);
			Assert.Contains("disk is full", Assert.Single(dialogs.Errors));
		}

		[Fact]
		public async Task Cancel_WithChanges_AsksBeforeDiscarding()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = false };
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService(), dialogs);
			viewModel.AddRuleCommand.Execute(null);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.CancelCommand.ExecuteAsync(null);

			Assert.Null(closed);
			Assert.Equal(1, dialogs.ConfirmCount);

			dialogs.ConfirmAnswer = true;
			await viewModel.CancelCommand.ExecuteAsync(null);

			Assert.False(closed);
		}

		[Fact]
		public async Task Cancel_WithoutChanges_ClosesWithoutAsking()
		{
			FakeDialogService dialogs = new FakeDialogService();
			DialogueRulesViewModel viewModel = await CreateAsync(new FakeDialogueRulesService(NOption()), dialogs);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.CancelCommand.ExecuteAsync(null);

			Assert.False(closed);
			Assert.Equal(0, dialogs.ConfirmCount);
		}
	}
}