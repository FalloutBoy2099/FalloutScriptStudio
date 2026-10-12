namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	/// <summary>The behaviours of the Message Editor that exist to keep big files fast: shared undo states, deferred structure check, cheap search updates.</summary>
	public sealed class MessageEditorLargeFileTests
	{
		private static String[] CreateLines(Int32 count)
		{
			return Enumerable.Range(0, count).Select(i => "{" + (100 + i) + "}{}{Message number " + i + ".}").ToArray();
		}

		[Fact]
		public void UndoRestoresEveryEditOfABigFile_ThroughTheSharedStates()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String[] lines = CreateLines(2500);
				await harness.OpenAsync("big.msg", lines);
				String[] original = harness.Format();

				for (Int32 i = 0; i < 12; i++)
				{
					harness.Rows[100 + (i * 50)].Text = "Edited " + i;
				}

				Assert.Equal("Edited 11", harness.Rows[650].Text);
				for (Int32 i = 0; i < 12; i++)
				{
					harness.Editor.UndoCommand.Execute(null);
				}

				Assert.Equal(original, harness.Format());
				for (Int32 i = 0; i < 12; i++)
				{
					harness.Editor.RedoCommand.Execute(null);
				}

				Assert.Equal("Edited 11", harness.Rows[650].Text);
				Assert.Equal("Edited 0", harness.Rows[100].Text);
				Assert.Equal("Message number 201.", harness.Rows[201].Text);
			});
		}

		[Fact]
		public void TheStructureCheckOfABigFileRunsAfterAPauseInTheEdits()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("big.msg", CreateLines(2500));
				await Task.Delay(600);
				Assert.Equal(0, harness.Editor.WarningCount);

				harness.Rows[10].LineText = "105";

				Assert.Equal(0, harness.Editor.WarningCount);
				await Task.Delay(700);
				Assert.Equal(1, harness.Editor.WarningCount);
				Assert.Equal(MessageIssueSeverity.Warning, harness.Rows[10].Severity);
				Assert.Contains("Duplicate", harness.Rows[10].IssueText, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void ManyEditsOfABigFileRunTheStructureCheckOnce_AndNextIssueDoesNotWaitForIt()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("big.msg", CreateLines(2500));
				await Task.Delay(600);

				harness.Rows[10].LineText = "105";
				harness.Rows[20].Text = "Another text.";
				harness.Select(0);
				harness.Editor.GoToNextIssueCommand.Execute(null);

				Assert.Equal(1, harness.Editor.WarningCount);
				Assert.Same(harness.Rows[10], harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void ASmallFileIsCheckedAtOnce()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("small.msg", CreateLines(40));

				harness.Rows[10].LineText = "105";

				Assert.Equal(1, harness.Editor.WarningCount);
			});
		}

		[Fact]
		public void TheSearchMarksFollowTheSearchTextWhileEditing()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", CreateLines(30));

				harness.Editor.SearchText = "number 1";
				Assert.Equal(11, harness.Editor.MatchCount);
				Assert.True(harness.Rows[1].IsSearchMatch);
				Assert.False(harness.Rows[2].IsSearchMatch);

				harness.Rows[2].Text = "Message number 1 too.";
				Assert.Equal(12, harness.Editor.MatchCount);
				Assert.True(harness.Rows[2].IsSearchMatch);

				harness.Editor.SearchText = String.Empty;
				Assert.Equal(0, harness.Editor.MatchCount);
				Assert.All(harness.Rows, row => Assert.False(row.IsSearchMatch));
				harness.Rows[3].Text = "Edited while nothing is searched.";
				Assert.Equal(0, harness.Editor.MatchCount);
				Assert.All(harness.Rows, row => Assert.False(row.IsSearchMatch));
				Assert.Equal(String.Empty, harness.Editor.SearchStatus);
			});
		}
	}
}
