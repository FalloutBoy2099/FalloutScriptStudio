namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Xunit;

	public class MessageTextViewModelTests
	{
		[Fact]
		public void Accept_ReturnsTheEditedText()
		{
			MessageTextViewModel viewModel = new MessageTextViewModel();
			Boolean? accepted = null;
			viewModel.CloseRequested += (_, result) => accepted = result;
			viewModel.Initialize("Message file: a.msg", "Hello");

			viewModel.Text = "Hello again";
			viewModel.Accept();

			Assert.True(accepted);
			Assert.Equal("Hello again", viewModel.Result);
			Assert.Equal("Message file: a.msg", viewModel.Caption);
		}

		[Theory]
		[InlineData("a{b")]
		[InlineData("a}b")]
		public void Accept_WithBraces_IsRefused(String text)
		{
			MessageTextViewModel viewModel = new MessageTextViewModel();
			Boolean? accepted = null;
			viewModel.CloseRequested += (_, result) => accepted = result;
			viewModel.Initialize("caption", "Hello");

			viewModel.Text = text;
			viewModel.Accept();

			Assert.True(viewModel.HasBraces);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));
			Assert.Null(accepted);
			Assert.Null(viewModel.Result);

			viewModel.Text = "fixed";
			Assert.False(viewModel.HasBraces);
			Assert.True(viewModel.AcceptCommand.CanExecute(null));
		}

		[Fact]
		public void Cancel_HasNoResult()
		{
			MessageTextViewModel viewModel = new MessageTextViewModel();
			Boolean? accepted = null;
			viewModel.CloseRequested += (_, result) => accepted = result;
			viewModel.Initialize("caption", "Hello");

			viewModel.Text = "changed";
			viewModel.Cancel();

			Assert.False(accepted);
			Assert.Null(viewModel.Result);
		}
	}
}
