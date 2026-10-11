namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorEditingTests
	{
		private static readonly String[] _numbered =
		{
			"{100}{}{a}",
			"{101}{}{b}",
			"# comment",
			"{110}{}{c}",
		};

		[Fact]
		public void AddNextLineUsesTheNumberAfterThePrecedingMessage()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(1);

				Boolean added = harness.Editor.TryAddNextLine();

				Assert.True(added);
				Assert.Equal(5, harness.Rows.Count);
				Assert.Equal("102", harness.Rows[2].LineText);
				Assert.Same(harness.Rows[2], harness.FocusedRows[0]);
				Assert.True(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void AddNextLineAfterACommentRoundsToTensAndAddsTheStep()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Editor.CommentStep = 5;
				harness.Select(2);

				Assert.True(harness.Editor.TryAddNextLine());

				Assert.Equal("105", harness.Rows[3].LineText);
			});
		}

		[Fact]
		public void AddNextLineIsRefusedWhenTheNumberIsTaken()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(2);

				Boolean added = harness.Editor.TryAddNextLine();

				Assert.False(added);
				Assert.Equal(4, harness.Rows.Count);
				Assert.Equal(new[] { MessageEditorBeep.Refused }, harness.Platform.Beeps);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void AddNextLineOnAnEmptyLineInsertsBeforeIt()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{a}", String.Empty);
				harness.Select(1);

				Assert.True(harness.Editor.TryAddNextLine());

				Assert.Equal("101", harness.Rows[1].LineText);
				Assert.True(harness.Rows[2].IsBlank);
			});
		}

		[Fact]
		public void AddNextLineKeepsEditingWhenACellWasBeingEdited()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(1);
				harness.Editor.IsCellEditing = true;

				harness.Editor.TryAddNextLine();

				Assert.True(harness.FocusRequests[^1].BeginEdit);
				Assert.Equal(MessageGridColumn.Text, harness.FocusRequests[^1].Column);
			});
		}

		[Fact]
		public void EnterOnAnEmptyCellAddsALineAndOnATextCellStartsEditing()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{}", "{105}{}{filled}");

				harness.Select(0);
				harness.Editor.ActivateCurrentCell();
				Assert.Equal(3, harness.Rows.Count);

				harness.Select(2);
				Int32 before = harness.Rows.Count;
				harness.Editor.ActivateCurrentCell();
				Assert.Equal(before, harness.Rows.Count);
				Assert.True(harness.FocusRequests[^1].BeginEdit);
			});
		}

		[Fact]
		public void InsertBlankBelowSelectsTheNewLine()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0);

				harness.Editor.InsertBlankBelowCommand.Execute(null);

				Assert.True(harness.Rows[1].IsBlank);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void InsertBlankAboveKeepsTheCurrentLineSelected()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(1);
				MessageRowViewModel current = harness.Rows[1];

				harness.Editor.InsertBlankAboveCommand.Execute(null);

				Assert.True(harness.Rows[1].IsBlank);
				Assert.Same(current, harness.Rows[2]);
				Assert.Same(current, harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void InsertCommentAddsABelowLineAndStartsEditingIt()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0);

				harness.Editor.InsertCommentCommand.Execute(null);

				Assert.True(harness.Rows[1].IsComment);
				Assert.Equal("#", harness.Rows[1].Text);
				Assert.True(harness.FocusRequests[^1].BeginEdit);
			});
		}

		[Fact]
		public void InsertCommentTurnsAnEmptyLineIntoTheComment()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{a}", String.Empty);
				harness.Select(1);

				harness.Editor.InsertCommentCommand.Execute(null);

				Assert.Equal(2, harness.Rows.Count);
				Assert.True(harness.Rows[1].IsComment);
			});
		}

		[Fact]
		public void InsertCommentDoesNothingWhileACellIsEdited()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0);
				harness.Editor.IsCellEditing = true;

				harness.Editor.InsertCommentCommand.Execute(null);

				Assert.Equal(4, harness.Rows.Count);
			});
		}

		[Fact]
		public void DeleteRemovesAllSelectedRows()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0, MessageGridColumn.Text, 2);

				harness.Editor.DeleteRowsCommand.Execute(null);

				Assert.Equal(new[] { "101", "110" }, harness.Rows.Select(row => row.LineText).ToArray());
				Assert.True(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void DeletingEverythingLeavesOneEmptyLine()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{a}", "{101}{}{b}");
				harness.Select(0, MessageGridColumn.Text, 1);

				harness.Editor.DeleteRowsCommand.Execute(null);

				Assert.Single(harness.Rows);
				Assert.True(harness.Rows[0].IsBlank);

				harness.Editor.DeleteRowsCommand.Execute(null);
				Assert.Single(harness.Rows);
			});
		}

		[Fact]
		public void MoveUpShiftsTheSelectedBlock()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(2, MessageGridColumn.Text, 3);

				harness.Editor.MoveUpCommand.Execute(null);

				Assert.Equal(new[] { "100", "-", "110", "101" }, harness.Rows.Select(row => row.LineText).ToArray());
				Assert.Equal(2, harness.FocusedRows.Count);
			});
		}

		[Fact]
		public void MoveDownAtTheEndChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(3);

				harness.Editor.MoveDownCommand.Execute(null);

				Assert.Equal("110", harness.Rows[3].LineText);
				Assert.False(harness.Editor.IsModified);
				Assert.False(harness.Editor.UndoCommand.CanExecute(null));
			});
		}

		[Fact]
		public void MoveDownShiftsASingleRow()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0);

				harness.Editor.MoveDownCommand.Execute(null);

				Assert.Equal(new[] { "101", "100", "-", "110" }, harness.Rows.Select(row => row.LineText).ToArray());
			});
		}

		[Fact]
		public void DuplicateGivesCopiesOfMessagesTheNextFreeNumbers()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0, MessageGridColumn.Text, 1);

				harness.Editor.DuplicateRowsCommand.Execute(null);

				Assert.Equal(new[] { "100", "101", "102", "103", "-", "110" }, harness.Rows.Select(row => row.LineText).ToArray());
				Assert.Equal("a", harness.Rows[2].Text);
				Assert.Equal(2, harness.FocusedRows.Count);
			});
		}

		[Fact]
		public void DuplicateCopiesCommentsUnchanged()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(2);

				harness.Editor.DuplicateRowsCommand.Execute(null);

				Assert.True(harness.Rows[3].IsComment);
				Assert.Equal("# comment", harness.Rows[3].Text);
			});
		}

		[Fact]
		public void TogglePlayerLineAppliesToAllSelectedMessages()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0, MessageGridColumn.Text, 1, 2);

				harness.Editor.TogglePlayerLineCommand.Execute(null);

				Assert.True(harness.Rows[0].IsPlayerLine);
				Assert.True(harness.Rows[1].IsPlayerLine);
				Assert.False(harness.Rows[3].IsPlayerLine);

				harness.Editor.TogglePlayerLineCommand.Execute(null);
				Assert.False(harness.Rows[0].IsPlayerLine);
				Assert.False(harness.Rows[1].IsPlayerLine);
			});
		}

		[Fact]
		public void EditCommentStoresThePromptedText()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(0);
				harness.Dialogs.PromptAnswer = "needs a voice";

				await harness.Editor.EditCommentCommand.ExecuteAsync(null);

				Assert.Equal("# needs a voice", harness.Rows[0].Comment);
				Assert.True(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void EditCommentIsRefusedForCommentLines()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _numbered);
				harness.Select(2);
				harness.Dialogs.PromptAnswer = "x";

				await harness.Editor.EditCommentCommand.ExecuteAsync(null);

				Assert.False(harness.Editor.IsModified);
				Assert.NotEqual(String.Empty, harness.Editor.NotificationMessage);
			});
		}

		[Fact]
		public void EditCommentKeepsTheCommentWhenThePromptIsCancelled()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{a} # keep");
				harness.Select(0);
				harness.Dialogs.PromptAnswer = null;

				await harness.Editor.EditCommentCommand.ExecuteAsync(null);

				Assert.Equal("# keep", harness.Rows[0].Comment);
			});
		}
	}
}
