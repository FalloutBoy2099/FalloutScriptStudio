namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;

	public class MessageStructureCheckerTests
	{
		private readonly MessageStructureChecker _checker = new MessageStructureChecker();

		[Fact]
		public void Check_WellFormedFileHasNoIssues()
		{
			String text = "# Look name\r\n{100}{}{Hello there.}\r\n{101}{snd01}{A long\r\n multi line text.} # comment\r\n\r\n";

			MessageStructureReport report = this._checker.Check(text);

			Assert.Empty(report.Issues);
			Assert.False(report.HasErrors);
		}

		[Fact]
		public void Check_ReportsAnInvalidNumber()
		{
			MessageStructureReport report = this._checker.Check("{abc}{}{x}");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal(BuildErrorType.Error, issue.Type);
			Assert.Equal("Invalid line number of the message.", issue.Message);
			Assert.Equal(1, issue.Offset);
			Assert.Equal(3, issue.Length);
			Assert.Equal(1, issue.Line);
			Assert.Equal(2, issue.Column);
			Assert.True(report.HasErrors);
		}

		[Fact]
		public void Check_ReportsAMissingNumberAndHighlightsTheBraces()
		{
			MessageStructureReport report = this._checker.Check("{}{}{x}");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal("Missing line number of the message.", issue.Message);
			Assert.Equal(0, issue.Offset);
			Assert.Equal(2, issue.Length);
		}

		[Fact]
		public void Check_ReportsADuplicateNumberAsWarning()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{A}\r\n{100}{}{B}");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal(BuildErrorType.Warning, issue.Type);
			Assert.Equal("Duplicate message line number: 100", issue.Message);
			Assert.Equal(2, issue.Line);
			Assert.False(report.HasErrors);
		}

		[Fact]
		public void Check_ReportsAnUnbalancedBracketOnTheLineOfThePreviousOpeningBrace()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{Hello\r\n{101}{}{World}");

			MessageStructureIssue issue = Assert.Single(report.Issues, item => item.Type == BuildErrorType.Error);
			Assert.Equal("Wrong structure of the paired curves brackets.", issue.Message);
			Assert.Equal(1, issue.Line);
			Assert.Equal(0, issue.Offset);
			Assert.Equal(13, issue.Length);
		}

		[Fact]
		public void Check_ReportsASurplusClosingBrace()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{A}}\r\n");

			Assert.Contains(report.Issues, item => item.Message.StartsWith("Wrong structure", StringComparison.Ordinal) && item.Line == 1);
		}

		[Fact]
		public void Check_ReportsATextAfterTheMessageThatIsNotAComment()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{Hello} some note\r\n{101}{}{Hi} # fine");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal(BuildErrorType.None, issue.Type);
			Assert.Equal("Missing '#' character of comment?", issue.Message);
			Assert.Equal(1, issue.Line);
			Assert.Equal(16, issue.Column);
			Assert.Equal(15, issue.Offset);
			Assert.Equal("some note".Length, issue.Length);
		}

		[Fact]
		public void Check_ReportsAMissingSpaceAfterPunctuation()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{Hello,world}");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal(BuildErrorType.Message, issue.Type);
			Assert.Equal("The white space character is missing.", issue.Message);
			Assert.Equal(1, issue.Line);
			Assert.Equal(14, issue.Column);
		}

		[Fact]
		public void Check_ReportsASentenceThatStartsWithASmallLetter()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{Hello. world}");

			MessageStructureIssue issue = Assert.Single(report.Issues);
			Assert.Equal("The sentence begins with a small letter instead of the capital letter.", issue.Message);
			Assert.Equal(BuildErrorType.Message, issue.Type);
		}

		[Fact]
		public void Check_AllowsAnEllipsisAndACommaBeforeASmallLetter()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{Well... maybe, perhaps}");

			Assert.Empty(report.Issues);
		}

		[Fact]
		public void Check_ReportsANewLineWithoutAWhiteSpace()
		{
			MessageStructureReport withoutSpace = this._checker.Check("{100}{}{Hello\r\nworld}");
			MessageStructureReport withSpace = this._checker.Check("{100}{}{Hello\r\n world}");

			Assert.Equal("A new line without a white space character.", Assert.Single(withoutSpace.Issues).Message);
			Assert.Empty(withSpace.Issues);
		}

		[Fact]
		public void ToBuildErrors_CarriesTheFileAndPositions()
		{
			MessageStructureReport report = this._checker.Check("{100}{}{A}\r\n{abc}{}{B}");

			IReadOnlyList<BuildError> errors = report.ToBuildErrors("dialog.msg");

			BuildError error = Assert.Single(errors);
			Assert.Equal("dialog.msg", error.FileName);
			Assert.Equal(BuildErrorType.Error, error.Type);
			Assert.Equal(2, error.Line);
			Assert.Equal(report.Issues.Single().Column, error.Column);
		}

		[Fact]
		public void Check_HandlesEmptyTextAndTrailingBreaks()
		{
			Assert.Empty(this._checker.Check(String.Empty).Issues);
			Assert.Empty(this._checker.Check("\r\n\r\n").Issues);
			Assert.Empty(this._checker.Check("{100}{}{Hello}").Issues);
		}
	}
}
