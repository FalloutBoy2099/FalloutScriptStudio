namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class SnippetPickerViewModelTests
	{
		private static readonly Snippet[] Snippets =
		{
			new Snippet("callp", "A call of a procedure.", "call x();") { Source = SnippetSource.BuiltIn },
			new Snippet("ifb", "if ... then begin ... end", "if (1) then begin\n${SELECTION}\nend") { Source = SnippetSource.BuiltIn },
			new Snippet("logit", "Writes a line to the log", "debug_msg(${1});") { Source = SnippetSource.User },
			new Snippet("zproj", "Of the project", "z") { Source = SnippetSource.Project },
		};

		private static SnippetPickerViewModel Create(Boolean surround = false)
		{
			SnippetPickerViewModel viewModel = new SnippetPickerViewModel();
			viewModel.Initialize(new SnippetPickerRequest(Snippets, surround));
			return viewModel;
		}

		[Fact]
		public void TheListStartsWithEverySnippet_SelectsTheFirst_AndCountsThem()
		{
			SnippetPickerViewModel viewModel = Create();

			Assert.Equal("Insert Snippet", viewModel.Title);
			Assert.Equal("Insert", viewModel.AcceptText);
			Assert.Equal(new[] { "callp", "ifb", "logit", "zproj" }, viewModel.Items.Select(item => item.Trigger));
			Assert.Same(viewModel.Items[0], viewModel.SelectedItem);
			Assert.Equal("4 of 4 snippets", viewModel.StatusText);
		}

		[Fact]
		public void SurroundWith_HasItsOwnTitleAndButton()
		{
			SnippetPickerViewModel viewModel = Create(true);

			Assert.Equal("Surround With", viewModel.Title);
			Assert.Equal("Surround", viewModel.AcceptText);
		}

		[Fact]
		public void TypingFiltersByTriggerOrDescription_AndTheTriggerComesFirst()
		{
			SnippetPickerViewModel viewModel = Create();

			viewModel.Filter = "log";
			Assert.Equal("logit", viewModel.Items[0].Trigger);

			viewModel.Filter = "call";
			Assert.Equal("callp", viewModel.Items[0].Trigger);
			Assert.Same(viewModel.Items[0], viewModel.SelectedItem);

			viewModel.Filter = "of the proj";
			Assert.Equal("zproj", Assert.Single(viewModel.Items).Trigger);
			Assert.Equal("1 of 4 snippets", viewModel.StatusText);
		}

		[Fact]
		public void WhenNothingMatches_TheListIsEmpty_NothingIsSelected_AndTheStatusSaysSo()
		{
			SnippetPickerViewModel viewModel = Create();

			viewModel.Filter = "qqqq";

			Assert.Empty(viewModel.Items);
			Assert.Null(viewModel.SelectedItem);
			Assert.Equal("No snippet matches the filter.", viewModel.StatusText);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void TheRowsTellTheTriggerTheDescriptionAndTheSource_ToAScreenReader()
		{
			SnippetPickerViewModel viewModel = Create();

			Assert.Equal("logit, Writes a line to the log, User", viewModel.Items[2].AccessibleName);
			Assert.Equal("Project", viewModel.Items[3].SourceText);
			Assert.Equal("Built-in", viewModel.Items[0].SourceText);
			Assert.Equal("if (1) then begin ${SELECTION} end", viewModel.Items[1].Preview);
		}

		[Fact]
		public void TheArrowKeysMoveTheSelection_WithinTheList()
		{
			SnippetPickerViewModel viewModel = Create();

			viewModel.MoveSelection(2);
			Assert.Equal("logit", viewModel.SelectedItem!.Trigger);
			viewModel.MoveSelection(10);
			Assert.Equal("zproj", viewModel.SelectedItem!.Trigger);
			viewModel.MoveSelection(-10);
			Assert.Equal("callp", viewModel.SelectedItem!.Trigger);
		}

		[Fact]
		public void Accept_ChoosesTheSelectedSnippetAndCloses_Cancel_ChoosesNothing()
		{
			SnippetPickerViewModel viewModel = Create();
			List<Boolean> closed = [];
			viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

			viewModel.MoveSelection(1);
			viewModel.AcceptCommand.Execute(null);
			Assert.Equal("ifb", viewModel.ChosenSnippet!.Trigger);

			viewModel.CancelCommand.Execute(null);
			Assert.Null(viewModel.ChosenSnippet);
			Assert.Equal(new[] { true, false }, closed);
		}
	}
}