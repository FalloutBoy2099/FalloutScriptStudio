namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;

	public class MessageDocumentTests
	{
		private static readonly String[] _sample =
		{
			"# Look Name",
			"{100}{}{Hello there}",
			"{101}{lip01}{Multi",
			"line text}",
			"\t{102}{}{PC line} # note",
			"{103}{}{}",
			String.Empty,
			"garbage line",
		};

		[Fact]
		public void Parse_RecognizesEveryKindOfLine()
		{
			MessageDocument document = MessageDocument.Parse(_sample);

			Assert.Equal(7, document.Entries.Count);
			Assert.Equal(MessageLineKind.Comment, document.Entries[0].Kind);
			Assert.Equal("# Look Name", document.Entries[0].Text);

			MessageEntry hello = document.Entries[1];
			Assert.Equal(MessageLineKind.Message, hello.Kind);
			Assert.Equal(100, hello.Id);
			Assert.Equal("Hello there", hello.Text);
			Assert.False(hello.IsPlayerLine);

			MessageEntry multi = document.Entries[2];
			Assert.Equal(101, multi.Id);
			Assert.Equal("lip01", multi.SoundName);
			Assert.Equal("Multi\r\nline text", multi.Text);

			MessageEntry player = document.Entries[3];
			Assert.Equal(102, player.Id);
			Assert.True(player.IsPlayerLine);
			Assert.Equal(1, player.IndentTabs);
			Assert.Equal("PC line", player.Text);
			Assert.Equal(" # note", player.Description);

			Assert.Equal(MessageLineKind.Message, document.Entries[4].Kind);
			Assert.Equal(MessageLineKind.Blank, document.Entries[5].Kind);
			Assert.Equal(MessageLineKind.Raw, document.Entries[6].Kind);
		}

		[Fact]
		public void Format_RoundTripsTheSample()
		{
			MessageDocument document = MessageDocument.Parse(_sample);

			IReadOnlyList<String> formatted = document.Format();

			Assert.Equal(
				new[]
				{
					"# Look Name",
					"{100}{}{Hello there}",
					"{101}{lip01}{Multi\r\nline text}",
					"\t{102}{}{PC line} # note",
					"{103}{}{}",
					String.Empty,
					"garbage line",
				},
				formatted);
		}

		[Fact]
		public void Parse_MessageWithoutClosingBraceRunsToTheEndOfTheFile()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "{100}{}{open", "still open" });

			MessageEntry entry = Assert.Single(document.Entries);
			Assert.Equal("open\r\nstill open", entry.Text);
			Assert.Equal("{100}{}{open\r\nstill open}", entry.Format());
		}

		[Fact]
		public void Parse_PreservesTheNumberOfLeadingTabsOfPlayerLines()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "\t\t{5}{}{x}" });

			MessageEntry entry = Assert.Single(document.Entries);
			Assert.Equal(2, entry.IndentTabs);
			Assert.Equal("\t\t{5}{}{x}", entry.Format());
		}

		[Fact]
		public void TextMap_TrimsTheFirstLineAndKeepsTheFirstOfDuplicates()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "{100}{}{  padded  }", "{100}{}{second}", "{101}{}{a ", "b}" });

			Dictionary<Int32, String> map = document.BuildTextMap();

			Assert.Equal("padded", map[100]);
			Assert.Equal("a\r\nb", map[101]);
			Assert.True(document.TryGetText(101, out String text));
			Assert.Equal("a\r\nb", text);
			Assert.False(document.TryGetText(999, out _));
		}

		[Fact]
		public void TryGetNextId_IncrementsTheNearestPrecedingNumber()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "# Look", "{100}{}{a}", "# Desc", "{101}{}{b}" });

			Assert.True(document.TryGetNextId(3, 10, out Int32 id));
			Assert.Equal(102, id);
		}

		[Fact]
		public void TryGetNextId_AfterACommentJumpsToTheNextRoundNumber()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "# Look", "{100}{}{a}", "# Desc", "{101}{}{b}" });

			Assert.True(document.TryGetNextId(2, 10, out Int32 id));
			Assert.Equal(110, id);
			Assert.True(document.TryGetNextId(2, 5, out id));
			Assert.Equal(105, id);
		}

		[Fact]
		public void TryGetNextId_ReportsAnAlreadyUsedNumber()
		{
			MessageDocument document = MessageDocument.Parse(new[] { "{100}{}{a}", "{101}{}{b}", "{102}{}{c}" });

			Assert.False(document.TryGetNextId(0, 10, out Int32 id));
			Assert.Equal(101, id);
		}

		[Fact]
		public void TryGetNextId_InAnEmptyDocumentStartsAtOne()
		{
			Assert.True(new MessageDocument().TryGetNextId(0, 10, out Int32 id));
			Assert.Equal(1, id);
		}

		[Fact]
		public void MessageTextEditor_ReplacesOnlyTheTextOfOneMessage()
		{
			List<String> lines = new List<String> { "# c", "{100}{snd}{old text} # keep", "{101}{}{other}" };

			Boolean replaced = MessageTextEditor.TryReplaceText(lines, 100, "new text");

			Assert.True(replaced);
			Assert.Equal(new[] { "# c", "{100}{snd}{new text} # keep", "{101}{}{other}" }, lines);
		}

		[Fact]
		public void MessageTextEditor_CollapsesAMultiLineMessageWithoutDamagingTheNextLine()
		{
			List<String> lines = new List<String> { "{100}{}{first", "second", "third} # d", "{101}{}{next}" };

			Boolean replaced = MessageTextEditor.TryReplaceText(lines, 100, "single");

			Assert.True(replaced);
			Assert.Equal(new[] { "{100}{}{single} # d", "{101}{}{next}" }, lines);
		}

		[Fact]
		public void MessageTextEditor_ReturnsFalseForAnUnknownMessage()
		{
			List<String> lines = new List<String> { "{100}{}{a}" };

			Assert.False(MessageTextEditor.TryReplaceText(lines, 7, "x"));
			Assert.Equal(new[] { "{100}{}{a}" }, lines);
		}
	}
}
