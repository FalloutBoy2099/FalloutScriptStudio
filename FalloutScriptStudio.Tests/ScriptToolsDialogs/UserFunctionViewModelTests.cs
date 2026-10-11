namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Xunit;

	public sealed class UserFunctionViewModelTests
	{
		private static UserFunctionViewModel Create(FunctionDefinition? existing = null, Boolean highlight = false)
		{
			UserFunctionViewModel viewModel = new UserFunctionViewModel();
			viewModel.Initialize(existing, highlight);
			return viewModel;
		}

		[Fact]
		public void Initialize_Add_StartsEmptyAndInvalidWithoutAMessage()
		{
			UserFunctionViewModel viewModel = Create();

			Assert.True(viewModel.IsAdding);
			Assert.Equal("Add function", viewModel.Title);
			Assert.Equal("Add", viewModel.OkText);
			Assert.False(viewModel.IsValid);
			Assert.False(viewModel.HasError);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
			Assert.False(viewModel.CanHighlight);
		}

		[Fact]
		public void Initialize_Edit_ShowsTheFunctionAndDisablesHighlight()
		{
			UserFunctionViewModel viewModel = Create(new FunctionDefinition("Say", "Says it", "display_msg(\"x\");\r\nplay();"), highlight: true);

			Assert.False(viewModel.IsAdding);
			Assert.Equal("Edit function", viewModel.Title);
			Assert.Equal("Save", viewModel.OkText);
			Assert.Equal("Say", viewModel.FunctionName);
			Assert.Equal("Says it", viewModel.Description);
			Assert.Contains("play();", viewModel.Code);
			Assert.True(viewModel.IsValid);
			Assert.False(viewModel.AddToHighlight);
			Assert.False(viewModel.CanHighlight);
			Assert.True(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void Accept_ReturnsTrimmedDefinitionWithDescriptionOnOneLine()
		{
			UserFunctionViewModel viewModel = Create();
			viewModel.FunctionName = "  Debug  ";
			viewModel.Description = " first\r\nsecond ";
			viewModel.Code = "\r\n  DEBUGMSG(\"a\");\r\n  DEBUGMSG(\"b\");\r\n";
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.AcceptCommand.Execute(null);

			Assert.True(closed);
			Assert.NotNull(viewModel.Result);
			Assert.Equal("Debug", viewModel.Result.Name);
			Assert.Equal("first second", viewModel.Result.Description);
			Assert.Equal("DEBUGMSG(\"a\");\r\n  DEBUGMSG(\"b\");", viewModel.Result.Code);
		}

		[Fact]
		public void Cancel_ClosesWithoutAResult()
		{
			UserFunctionViewModel viewModel = Create(new FunctionDefinition("Say", String.Empty, "x"));
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CancelCommand.Execute(null);

			Assert.False(closed);
			Assert.Null(viewModel.Result);
		}

		[Theory]
		[InlineData("a<d>b", "", "code")]
		[InlineData("name", "a<s>b", "code")]
		[InlineData("name", "", "x<cr>y")]
		public void ReservedMarkers_AreReportedAndBlockAccept(String name, String description, String code)
		{
			UserFunctionViewModel viewModel = Create();
			viewModel.FunctionName = name;
			viewModel.Description = description;
			viewModel.Code = code;

			Assert.True(viewModel.HasError);
			Assert.NotNull(viewModel.ErrorMessage);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void MissingValues_BlockAcceptWithoutAnErrorMessage()
		{
			UserFunctionViewModel viewModel = Create();
			viewModel.FunctionName = "Name";

			Assert.False(viewModel.IsValid);
			Assert.False(viewModel.HasError);

			viewModel.Code = "   ";

			Assert.False(viewModel.AcceptCommand.CanExecute(null));

			viewModel.Code = "foo();";

			Assert.True(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void Highlight_FollowsTheFirstWordOfTheCode()
		{
			UserFunctionViewModel viewModel = Create(null, highlight: true);

			Assert.True(viewModel.AddToHighlight);
			Assert.False(viewModel.CanHighlight);

			viewModel.Code = "  DEBUGMSG(\"x\")";

			Assert.Equal("DEBUGMSG", viewModel.Keyword);
			Assert.True(viewModel.CanHighlight);
			Assert.Contains("DEBUGMSG", viewModel.HighlightText);

			viewModel.Code = "(1 + 2)";

			Assert.Equal(String.Empty, viewModel.Keyword);
			Assert.False(viewModel.CanHighlight);
		}
	}
}
