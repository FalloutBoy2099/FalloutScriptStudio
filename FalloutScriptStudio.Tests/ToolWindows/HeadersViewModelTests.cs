namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class HeadersViewModelTests : IDisposable
	{
		private readonly String _directory = TestFiles.CreateWorkFolder("headers");

		public HeadersViewModelTests()
		{
			Directory.CreateDirectory(Path.Combine(this._directory, "sub"));
			File.WriteAllText(Path.Combine(this._directory, "SCRIPTS.H"), String.Empty);
			File.WriteAllText(Path.Combine(this._directory, "command.h"), String.Empty);
			File.WriteAllText(Path.Combine(this._directory, "readme.txt"), String.Empty);
			File.WriteAllText(Path.Combine(this._directory, "sub", "Sfall.H"), String.Empty);
		}

		public void Dispose()
		{
			TestFiles.DeleteWorkFolder(this._directory);
		}

		[Theory]
		[InlineData("scripts.h", "Scripts.h")]
		[InlineData("SCRIPTS.H", "Scripts.h")]
		[InlineData("a.h", "A.h")]
		[InlineData("", "")]
		public void FormatDisplayName_CapitalizesFirstLetterOnly(String input, String expected)
		{
			Assert.Equal(expected, HeadersViewModel.FormatDisplayName(input));
		}

		[Fact]
		public void FindHeaders_FindsHeaderFilesRecursivelySorted()
		{
			List<HeaderFileItem> items = HeadersViewModel.FindHeaders(this._directory);

			Assert.Equal(new[] { "Command.h", "Scripts.h", "Sfall.h" }, items.Select(item => item.DisplayName).ToArray());
			Assert.Equal(Path.Combine("sub", "Sfall.H"), items[2].RelativePath);
			Assert.Equal(Path.Combine(this._directory, "sub", "Sfall.H"), items[2].FullPath);
		}

		[Fact]
		public void FindHeaders_MissingFolderGivesEmptyList()
		{
			Assert.Empty(HeadersViewModel.FindHeaders(Path.Combine(this._directory, "nope")));
		}

		[Fact]
		public async Task LoadAsync_FillsListAndSelectsFirst()
		{
			HeadersViewModel viewModel = new HeadersViewModel();

			await viewModel.LoadAsync(this._directory);

			Assert.Equal(3, viewModel.Items.Count);
			Assert.Same(viewModel.Items[0], viewModel.SelectedItem);
			Assert.Contains("3 of 3", viewModel.StatusText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Filter_NarrowsListByNameOrFolder()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);

			viewModel.Filter = "SCR";
			Assert.Single(viewModel.Items);
			Assert.Equal("Scripts.h", viewModel.SelectedItem?.DisplayName);

			viewModel.Filter = "sub";
			Assert.Single(viewModel.Items);
			Assert.Equal("Sfall.h", viewModel.Items[0].DisplayName);

			viewModel.Filter = "zzz";
			Assert.Empty(viewModel.Items);
			Assert.Null(viewModel.SelectedItem);
			Assert.False(viewModel.AcceptCommand.CanExecute(null));

			viewModel.Filter = String.Empty;
			Assert.Equal(3, viewModel.Items.Count);
		}

		[Fact]
		public async Task Filter_KeepsSelectionWhenStillVisible()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			viewModel.SelectedItem = viewModel.Items[2];

			viewModel.Filter = "h";

			Assert.Equal("Sfall.h", viewModel.SelectedItem?.DisplayName);
		}

		[Fact]
		public async Task MoveSelection_StaysInsideTheList()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);

			viewModel.MoveSelection(-1);
			Assert.Same(viewModel.Items[0], viewModel.SelectedItem);
			viewModel.MoveSelection(1);
			Assert.Same(viewModel.Items[1], viewModel.SelectedItem);
			viewModel.MoveSelection(10);
			Assert.Same(viewModel.Items[2], viewModel.SelectedItem);
		}

		[Fact]
		public async Task Accept_ReturnsSelectedPathAndCloses()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.SelectedItem = viewModel.Items[1];

			viewModel.AcceptCommand.Execute(null);

			Assert.Equal(Path.Combine(this._directory, "SCRIPTS.H"), viewModel.SelectedPath);
			Assert.True(closed);
		}

		[Fact]
		public async Task Choose_WithCtrlHandsTheHeaderToTheHandlerAndKeepsTheWindowOpen()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			List<String> chosen = new List<String>();
			viewModel.KeepOpenHandler = path =>
			{
				chosen.Add(path);
				return Task.CompletedTask;
			};
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.SelectedItem = viewModel.Items[0];
			await viewModel.ChooseAsync(true);
			viewModel.SelectedItem = viewModel.Items[2];
			await viewModel.ChooseAsync(true);

			Assert.Equal(new[] { Path.Combine(this._directory, "command.h"), Path.Combine(this._directory, "sub", "Sfall.H") }, chosen);
			Assert.Null(closed);
			Assert.Null(viewModel.SelectedPath);
		}

		[Fact]
		public async Task Choose_WithoutCtrlClosesTheWindowAndReturnsThePath()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			Int32 handled = 0;
			viewModel.KeepOpenHandler = _ =>
			{
				handled++;
				return Task.CompletedTask;
			};
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;
			viewModel.SelectedItem = viewModel.Items[1];

			await viewModel.ChooseAsync(false);

			Assert.Equal(0, handled);
			Assert.True(closed);
			Assert.Equal(Path.Combine(this._directory, "SCRIPTS.H"), viewModel.SelectedPath);
		}

		[Fact]
		public async Task Choose_WithCtrlButWithoutAHandlerBehavesLikeAPlainChoice()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.ChooseAsync(true);

			Assert.True(closed);
			Assert.NotNull(viewModel.SelectedPath);
		}

		[Fact]
		public async Task Choose_WithNothingSelectedDoesNothing()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);
			viewModel.Filter = "zzz";
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			await viewModel.ChooseAsync(false);

			Assert.Null(closed);
			Assert.Null(viewModel.SelectedPath);
		}

		[Fact]
		public async Task Cancel_ReturnsNull()
		{
			HeadersViewModel viewModel = new HeadersViewModel();
			await viewModel.LoadAsync(this._directory);

			viewModel.CancelCommand.Execute(null);

			Assert.Null(viewModel.SelectedPath);
		}
	}
}
