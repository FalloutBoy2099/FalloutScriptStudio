namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class GoToLineViewModelTests
	{
		[Theory]
		[InlineData("5", 100, 5)]
		[InlineData("  42  ", 100, 42)]
		[InlineData("100", 100, 100)]
		[InlineData("101", 100, 100)]
		[InlineData("99999999999999", 100, 100)]
		[InlineData("0", 100, 1)]
		[InlineData("007", 100, 7)]
		public void ParseLine_LimitsToDocument(String text, Int32 lineCount, Int32 expected)
		{
			Assert.Equal(expected, GoToLineViewModel.ParseLine(text, lineCount));
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("abc")]
		[InlineData("-3")]
		[InlineData("1.5")]
		[InlineData("1 2")]
		[InlineData(null)]
		public void ParseLine_RejectsNonNumbers(String? text)
		{
			Assert.Null(GoToLineViewModel.ParseLine(text, 100));
		}

		[Fact]
		public void ParseLine_EmptyDocumentAllowsLineOne()
		{
			Assert.Equal(1, GoToLineViewModel.ParseLine("7", 0));
		}

		[Fact]
		public void Initialize_ShowsCurrentLineLimitedToRange()
		{
			GoToLineViewModel viewModel = new GoToLineViewModel();
			viewModel.Initialize(500, 120);

			Assert.Equal("120", viewModel.LineText);
			Assert.Equal(120, viewModel.LineCount);
			Assert.Contains("120", viewModel.Prompt, StringComparison.Ordinal);
			Assert.True(viewModel.IsValid);
		}

		[Fact]
		public void Go_ClosesWithSelectedLine()
		{
			GoToLineViewModel viewModel = new GoToLineViewModel();
			viewModel.Initialize(1, 50);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.LineText = "73";
			Assert.Equal(50, viewModel.ParsedLine);
			viewModel.LineText = "12";
			Assert.True(viewModel.GoCommand.CanExecute(null));
			viewModel.GoCommand.Execute(null);

			Assert.Equal(12, viewModel.SelectedLine);
			Assert.True(closed);
		}

		[Fact]
		public void Go_IsDisabledForInvalidText()
		{
			GoToLineViewModel viewModel = new GoToLineViewModel();
			viewModel.Initialize(1, 50);

			viewModel.LineText = "x";

			Assert.False(viewModel.IsValid);
			Assert.False(viewModel.GoCommand.CanExecute(null));
		}

		[Fact]
		public void Cancel_ReturnsNoLine()
		{
			GoToLineViewModel viewModel = new GoToLineViewModel();
			viewModel.Initialize(3, 50);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.Null(viewModel.SelectedLine);
			Assert.False(closed);
		}
	}
}