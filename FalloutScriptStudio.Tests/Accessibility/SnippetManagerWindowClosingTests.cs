namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using FalloutScriptStudio.Tests.ToolWindows;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.ToolWindows;
	using Xunit;

	/// <summary>The close button of the snippet manager asks about unsaved changes like the Cancel button and Esc do (the window is shown off-screen).</summary>
	public sealed class SnippetManagerWindowClosingTests
	{
		[Fact]
		public void TheCloseButtonAsksBeforeDroppingChanges_AndStaysWhenTheUserRefuses()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = false };
			SnippetManagerViewModel viewModel = new SnippetManagerViewModel(new FakeSnippetService(), dialogs);
			viewModel.AddCommand.Execute(null);
			Boolean closed = false;

			WindowHost.Run(
				() => new SnippetManagerWindow(viewModel),
				window =>
				{
					window.Closed += (_, _) => closed = true;

					window.Close();
					WindowHost.Pump();

					Assert.Equal(1, dialogs.ConfirmCount);
					Assert.False(closed);
					Assert.True(window.IsVisible);

					dialogs.ConfirmAnswer = true;
					window.Close();
					WindowHost.Pump();

					Assert.Equal(2, dialogs.ConfirmCount);
					Assert.True(closed);
				});
		}

		[Fact]
		public void TheCloseButtonDoesNotAskWhenNothingChanged()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = false };
			SnippetManagerViewModel viewModel = new SnippetManagerViewModel(new FakeSnippetService(), dialogs);
			Boolean closed = false;

			WindowHost.Run(
				() => new SnippetManagerWindow(viewModel),
				window =>
				{
					window.Closed += (_, _) => closed = true;

					window.Close();
					WindowHost.Pump();

					Assert.Equal(0, dialogs.ConfirmCount);
					Assert.True(closed);
				});
		}

		[Fact]
		public void TheCancelCommandClosesTheWindowAfterOneQuestion()
		{
			FakeDialogService dialogs = new FakeDialogService { ConfirmAnswer = true };
			SnippetManagerViewModel viewModel = new SnippetManagerViewModel(new FakeSnippetService(), dialogs);
			viewModel.AddCommand.Execute(null);
			Boolean closed = false;

			WindowHost.Run(
				() => new SnippetManagerWindow(viewModel),
				window =>
				{
					window.Closed += (_, _) => closed = true;

					viewModel.CancelCommand.Execute(null);
					WindowHost.Pump();

					Assert.Equal(1, dialogs.ConfirmCount);
					Assert.True(closed);
				});
		}
	}
}