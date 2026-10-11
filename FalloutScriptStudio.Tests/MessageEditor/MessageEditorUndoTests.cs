namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorUndoTests
	{
		private static readonly String[] _sample =
		{
			"{100}{}{one}",
			"{101}{}{two}",
			"{102}{}{three}",
		};

		[Fact]
		public void UndoRevertsACellEditAndRedoRepeatsIt()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "TWO";

				harness.Editor.UndoCommand.Execute(null);
				Assert.Equal("two", harness.Rows[1].Text);
				Assert.False(harness.Editor.IsModified);

				harness.Editor.RedoCommand.Execute(null);
				Assert.Equal("TWO", harness.Rows[1].Text);
				Assert.True(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void UndoRevertsStructuralChanges()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(0, MessageGridColumn.Text, 1);
				harness.Editor.DeleteRowsCommand.Execute(null);
				Assert.Single(harness.Rows);

				harness.Editor.UndoCommand.Execute(null);

				Assert.Equal(new[] { "100", "101", "102" }, harness.Rows.Select(row => row.LineText).ToArray());
				Assert.Equal("one", harness.Rows[0].Text);
			});
		}

		[Fact]
		public void UndoSelectsTheRowThatWasCurrentBeforeTheChange()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(2);
				harness.Editor.DeleteRowsCommand.Execute(null);

				harness.Editor.UndoCommand.Execute(null);

				Assert.Equal("102", harness.FocusedRows[0].LineText);
			});
		}

		[Fact]
		public void AnEditAfterUndoDropsTheRedoHistory()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[0].Text = "a";
				harness.Editor.UndoCommand.Execute(null);
				Assert.True(harness.Editor.RedoCommand.CanExecute(null));

				harness.Rows[1].Text = "b";

				Assert.False(harness.Editor.RedoCommand.CanExecute(null));
			});
		}

		[Fact]
		public void TheModifiedFlagFollowsTheSavedState()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[0].Text = "first";
				await harness.Editor.SaveDocumentAsync();
				Assert.False(harness.Editor.IsModified);

				harness.Rows[1].Text = "second";
				Assert.True(harness.Editor.IsModified);

				harness.Editor.UndoCommand.Execute(null);
				Assert.False(harness.Editor.IsModified);

				harness.Editor.UndoCommand.Execute(null);
				Assert.True(harness.Editor.IsModified);
				Assert.Equal("one", harness.Rows[0].Text);
			});
		}

		[Fact]
		public void ChangedMarksComeBackWithUndo()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[0].Text = "first";
				harness.Rows[1].Text = "second";

				harness.Editor.UndoCommand.Execute(null);

				Assert.True(harness.Rows[0].IsTextChanged);
				Assert.False(harness.Rows[1].IsTextChanged);
			});
		}

		[Fact]
		public void LoadingAFileForgetsTheHistory()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[0].Text = "first";
				harness.Dialogs.SaveAnswer = FalloutScriptStudio.Services.YesNoCancel.No;

				await harness.OpenAsync("b.msg", "{1}{}{x}");

				Assert.False(harness.Editor.UndoCommand.CanExecute(null));
				Assert.False(harness.Editor.RedoCommand.CanExecute(null));
			});
		}

		[Fact]
		public void OneCommandIsOneUndoStep()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(0, MessageGridColumn.Text, 1, 2);

				harness.Editor.TogglePlayerLineCommand.Execute(null);
				Assert.All(harness.Rows, row => Assert.True(row.IsPlayerLine));

				harness.Editor.UndoCommand.Execute(null);

				Assert.All(harness.Rows, row => Assert.False(row.IsPlayerLine));
				Assert.False(harness.Editor.UndoCommand.CanExecute(null));
			});
		}

		[Fact]
		public void TheHistoryIsBounded()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				for (Int32 i = 0; i < 300; i++)
				{
					harness.Rows[0].Text = "v" + i;
				}

				Int32 steps = 0;
				while (harness.Editor.UndoCommand.CanExecute(null))
				{
					harness.Editor.UndoCommand.Execute(null);
					steps++;
				}

				Assert.Equal(200, steps);
			});
		}
	}
}
