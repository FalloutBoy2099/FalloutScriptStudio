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
