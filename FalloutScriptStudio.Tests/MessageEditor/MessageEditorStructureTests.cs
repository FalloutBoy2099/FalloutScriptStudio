namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorStructureTests
	{
		[Fact]
		public void ACleanFileHasNoProblems()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.OpenAsync("a.msg", "# Intro", "{100}{}{Hello there.}", "{101}{}{Bye.}");

				Assert.All(harness.Rows, row => Assert.Equal(MessageIssueSeverity.None, row.Severity));
				Assert.Equal("No problems", harness.Editor.IssueSummary);
			});
		}

		[Fact]
		public void DuplicateNumbersAreFlaggedOnTheSecondMessage()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.OpenAsync("a.msg", "{100}{}{one}", "{101}{}{two}", "{100}{}{again}");

				Assert.Equal(new[] { MessageIssueSeverity.None, MessageIssueSeverity.None, MessageIssueSeverity.Warning }, harness.Rows.Select(row => row.Severity).ToArray());
				Assert.Contains("Duplicate", harness.Rows[2].IssueText, StringComparison.Ordinal);
				Assert.Equal(1, harness.Editor.WarningCount);
				Assert.True(harness.Rows[2].HasIssue);
			});
		}

		[Fact]
		public void EditingTheNumberClearsTheProblem()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{one}", "{100}{}{again}");
				Assert.Equal(MessageIssueSeverity.Warning, harness.Rows[1].Severity);

				harness.Rows[1].LineText = "101";

				Assert.Equal(MessageIssueSeverity.None, harness.Rows[1].Severity);
				Assert.Equal(0, harness.Editor.WarningCount);
			});
		}

		[Fact]
		public void UnrecognizedLinesAreWarnings()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.OpenAsync("a.msg", "{100}{}{one}", "this is not a message");

				Assert.Equal(MessageIssueSeverity.Warning, harness.Rows[1].Severity);
				Assert.Equal("?", harness.Rows[1].LineText);
			});
		}

		[Fact]
		public void BracketMistakesAreErrorsOnTheirLine()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{one}", String.Empty);

				harness.Rows[1].Text = "}}";

				Assert.Equal(MessageIssueSeverity.Error, harness.Rows[1].Severity);
				Assert.Equal(MessageIssueSeverity.None, harness.Rows[0].Severity);
				Assert.True(harness.Editor.ErrorCount >= 1);
				Assert.Contains("error", harness.Editor.IssueSummary, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void StyleHintsAreMappedToTheirRows()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.OpenAsync("a.msg", "{100}{}{one}", "{101}{}{Hello,world}");

				Assert.Equal(MessageIssueSeverity.Hint, harness.Rows[1].Severity);
				Assert.Equal(1, harness.Editor.HintCount);
			});
		}

		[Fact]
		public void MultiLineMessagesKeepTheLineMappingCorrect()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{one}", "{101}{}{first", " second}", "{100}{}{dup}");

				Assert.Equal(3, harness.Rows.Count);
				Assert.Equal("first\r\n second", harness.Rows[1].Text);
				Assert.Equal(MessageIssueSeverity.Warning, harness.Rows[2].Severity);
				Assert.Equal(MessageIssueSeverity.None, harness.Rows[1].Severity);
			});
		}

		[Fact]
		public void GoToNextIssueWrapsAround()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{one}", "{100}{}{dup}", "{101}{}{three}");
				harness.Select(1);

				harness.Editor.GoToNextIssueCommand.Execute(null);

				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);

				harness.Select(2);
				harness.Editor.GoToNextIssueCommand.Execute(null);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);

				harness.Editor.GoToPreviousIssueCommand.Execute(null);
				Assert.Same(harness.Rows[1], harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void GoToNextIssueSaysWhenThereIsNone()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", "{100}{}{one.}");

				harness.Editor.GoToNextIssueCommand.Execute(null);

				Assert.Equal("No problems found.", harness.Editor.StatusMessage);
			});
		}
	}
}
