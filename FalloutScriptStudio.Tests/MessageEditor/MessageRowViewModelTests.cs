namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageRowViewModelTests
	{
		private static readonly String[] _sample =
		{
			"# Intro",
			"{100}{}{Hello there}",
			"\t{101}{snd}{I answer} # reply",
			String.Empty,
		};

		[Fact]
		public void ParsedRowsExposeTheirCells()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				Assert.Equal(4, harness.Rows.Count);
				Assert.True(harness.Rows[0].IsComment);
				Assert.Equal("-", harness.Rows[0].LineText);
				Assert.Equal("# Intro", harness.Rows[0].Text);

				MessageRowViewModel player = harness.Rows[2];
				Assert.True(player.IsMessage);
				Assert.True(player.IsPlayerLine);
				Assert.Equal("101", player.LineText);
				Assert.Equal("snd", player.SoundName);
				Assert.Equal("I answer", player.Text);
				Assert.Equal("# reply", player.Comment);

				Assert.True(harness.Rows[3].IsBlank);
				Assert.Equal(String.Empty, harness.Rows[3].LineText);
			});
		}

		[Fact]
		public void LineNumberMustBeDigits()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].LineText = "12x";

				Assert.Equal("100", harness.Rows[1].LineText);
				Assert.Equal("The line number must contain digits only.", harness.Editor.NotificationMessage);
				Assert.False(harness.Editor.IsModified);
				Assert.False(harness.Rows[1].IsLineChanged);
			});
		}

		[Fact]
		public void ChangingTheNumberMarksTheCellAndTheDocument()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].LineText = "150";

				Assert.Equal(150, harness.Rows[1].Entry.Id);
				Assert.True(harness.Rows[1].IsLineChanged);
				Assert.True(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void TypingANumberIntoAnEmptyLineMakesItAMessage()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[3].LineText = "200";
				harness.Rows[3].Text = "New text";

				Assert.True(harness.Rows[3].IsMessage);
				Assert.Equal("{200}{}{New text}", harness.Rows[3].Entry.Format());
			});
		}

		[Fact]
		public void CommentLinesCannotBeNumbered()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[0].LineText = "5";

				Assert.True(harness.Rows[0].IsComment);
				Assert.False(harness.Rows[0].CanEditLine);
			});
		}

		[Fact]
		public void CurlyBracesAreRefusedInMessageText()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].Text = "bad {text}";

				Assert.Equal("Hello there", harness.Rows[1].Text);
				Assert.Contains("braces", harness.Editor.NotificationMessage, StringComparison.OrdinalIgnoreCase);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void CurlyBracesAreRefusedInTheSoundName()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[2].SoundName = "a}b";

				Assert.Equal("snd", harness.Rows[2].SoundName);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void TheLegacyPlayerMarkerIsRemovedFromTypedText()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].Text = "● Typed with marker";

				Assert.Equal("Typed with marker", harness.Rows[1].Text);
			});
		}

		[Fact]
		public void TextTypedIntoAnEmptyLineBecomesACommentWhenItStartsWithHash()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[3].Text = "# section";

				Assert.True(harness.Rows[3].IsComment);
				Assert.Equal("-", harness.Rows[3].LineText);
			});
		}

		[Fact]
		public void AWholeMessageLineTypedIntoAnEmptyLineIsParsed()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[3].Text = "\t{300}{lip}{Answer} # note";

				MessageRowViewModel row = harness.Rows[3];
				Assert.True(row.IsMessage);
				Assert.True(row.IsPlayerLine);
				Assert.Equal(300, row.Entry.Id);
				Assert.Equal("lip", row.SoundName);
				Assert.Equal("Answer", row.Text);
				Assert.Equal("# note", row.Comment);
			});
		}

		[Fact]
		public void CommentsAreStoredWithAHashAndALeadingSpace()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].Comment = "remember this";
				Assert.Equal(" # remember this", harness.Rows[1].Entry.Description);
				Assert.Equal("{100}{}{Hello there} # remember this", harness.Rows[1].Entry.Format());

				harness.Rows[1].Comment = "# already marked";
				Assert.Equal(" # already marked", harness.Rows[1].Entry.Description);

				harness.Rows[1].Comment = "   ";
				Assert.Equal(String.Empty, harness.Rows[1].Entry.Description);
			});
		}

		[Fact]
		public void UnchangedValuesDoNotMarkTheDocumentAsModified()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].Text = "Hello there";
				harness.Rows[1].LineText = "100";
				harness.Rows[2].Comment = "# reply";
				harness.Rows[2].SoundName = "snd";

				Assert.False(harness.Editor.IsModified);
				Assert.False(harness.Editor.UndoCommand.CanExecute(null));
			});
		}

		[Fact]
		public void MultiLineTextKeepsItsLineBreaks()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[1].Text = "first\nsecond\rthird";

				Assert.Equal("first\r\nsecond\r\nthird", harness.Rows[1].Text);
			});
		}

		[Fact]
		public void OnlyMessagesCanBePlayerLines()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				harness.Rows[0].IsPlayerLine = true;
				harness.Rows[1].IsPlayerLine = true;

				Assert.False(harness.Rows[0].IsPlayerLine);
				Assert.True(harness.Rows[1].IsPlayerLine);
				Assert.Equal("\t{100}{}{Hello there}", harness.Rows[1].Entry.Format());
				Assert.Equal(MessageLineKind.Comment, harness.Rows[0].Kind);
			});
		}
	}
}