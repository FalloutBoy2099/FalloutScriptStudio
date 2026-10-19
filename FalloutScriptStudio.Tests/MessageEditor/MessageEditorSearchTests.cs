namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorSearchTests
	{
		private static readonly String[] _sample =
		{
			"# Greetings",
			"{100}{}{Hello there} # friendly",
			"{101}{lipfile}{Goodbye}",
			"{102}{}{Hello again}",
		};

		[Fact]
		public void FindNextMovesToTheFollowingMatchAndWraps()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(0);
				harness.Editor.SearchText = "hello";

				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
				Assert.Equal(MessageGridColumn.Text, harness.FocusRequests[^1].Column);

				harness.Select(1);
				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[3], harness.FocusedRows[0]);

				harness.Select(3);
				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
				Assert.Equal("Search wrapped around.", harness.Editor.StatusMessage);
			});
		}

		[Fact]
		public void FindPreviousGoesBackwards()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(3);
				harness.Editor.SearchText = "HELLO";

				harness.Editor.FindPreviousCommand.Execute(null);

				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void FindLooksAtTheNumberAndTheCommentColumns()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(0);

				harness.Editor.SearchText = "102";
				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[3], harness.FocusedRows[0]);
				Assert.Equal(MessageGridColumn.Line, harness.FocusRequests[^1].Column);

				harness.Select(0);
				harness.Editor.SearchText = "friendly";
				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
				Assert.Equal(MessageGridColumn.Comment, harness.FocusRequests[^1].Column);
			});
		}

		[Fact]
		public void TheSoundColumnIsSearchedOnlyWhenItIsShown()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(0);
				harness.Editor.SearchText = "lipfile";

				harness.Editor.FindNextCommand.Execute(null);
				Assert.Equal(new[] { MessageEditorBeep.NotFound }, harness.Platform.Beeps);

				harness.Editor.ShowLipColumn = true;
				harness.Editor.FindNextCommand.Execute(null);
				Assert.Same(harness.Rows[2], harness.FocusedRows[0]);
				Assert.Equal(MessageGridColumn.Sound, harness.FocusRequests[^1].Column);
			});
		}

		[Fact]
		public void NothingFoundBeepsAndSaysSo()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Editor.SearchText = "zebra";

				harness.Editor.FindNextCommand.Execute(null);

				Assert.Equal("Not found.", harness.Editor.StatusMessage);
				Assert.Equal(new[] { MessageEditorBeep.NotFound }, harness.Platform.Beeps);
			});
		}

		[Fact]
		public void AnEmptySearchTextAsksForText()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Editor.FindNextCommand.Execute(null);

				Assert.Equal("Enter the text to find.", harness.Editor.StatusMessage);
			});
		}

		[Fact]
		public void MatchesAreCountedAndMarked()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Editor.SearchText = "hello";

				Assert.Equal(2, harness.Editor.MatchCount);
				Assert.Equal("2 matches", harness.Editor.SearchStatus);
				Assert.Equal(new[] { false, true, false, true }, harness.Rows.Select(row => row.IsSearchMatch).ToArray());

				harness.Editor.ClearSearchCommand.Execute(null);
				Assert.Equal(0, harness.Editor.MatchCount);
				Assert.Equal(String.Empty, harness.Editor.SearchStatus);
			});
		}

		[Fact]
		public void TheFilterShowsOnlyMatchingRows()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Editor.SearchText = "hello";

				harness.Editor.IsFilterActive = true;

				Assert.Equal(new[] { harness.Rows[1], harness.Rows[3] }, harness.Editor.RowsView.Cast<MessageRowViewModel>().ToArray());

				harness.Editor.SearchText = String.Empty;
				Assert.Equal(4, harness.Editor.RowsView.Cast<MessageRowViewModel>().Count());

				harness.Editor.SearchText = "again";
				harness.Editor.IsFilterActive = false;
				Assert.Equal(4, harness.Editor.RowsView.Cast<MessageRowViewModel>().Count());
			});
		}

		[Fact]
		public void RowsAddedWhileFilteringStayVisible()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Editor.SearchText = "hello";
				harness.Editor.IsFilterActive = true;
				harness.Select(1);

				harness.Editor.InsertBlankBelowCommand.Execute(null);

				Assert.Contains(harness.Rows[2], harness.Editor.RowsView.Cast<MessageRowViewModel>());
				Assert.Equal(3, harness.Editor.RowsView.Cast<MessageRowViewModel>().Count());
			});
		}

		[Fact]
		public void RowsCannotBeMovedWhileFiltering()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Editor.SearchText = "hello";
				harness.Editor.IsFilterActive = true;
				harness.Select(1);

				harness.Editor.MoveDownCommand.Execute(null);

				Assert.Equal("100", harness.Rows[1].LineText);
				Assert.False(harness.Editor.IsModified);
				Assert.NotEqual(String.Empty, harness.Editor.NotificationMessage);
			});
		}

		[Fact]
		public void FindOnlyVisitsFilteredRows()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Editor.SearchText = "hello";
				harness.Editor.IsFilterActive = true;
				harness.Select(1);

				harness.Editor.FindNextCommand.Execute(null);

				Assert.Same(harness.Rows[3], harness.FocusedRows[0]);
			});
		}
	}
}