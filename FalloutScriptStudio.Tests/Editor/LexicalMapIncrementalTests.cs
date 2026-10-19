namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	/// <summary>The lexical map that follows the edits must always equal the map of a complete scan of the text.</summary>
	public sealed class LexicalMapIncrementalTests
	{
		private static readonly String[] _pieces =
		{
			"/*", "*/", "//", "\"", "\\\"", "{", "}", "(", ")", "\r\n", "\r\n", "\n", "abc", " ", "x := 1;", "procedure p begin", "end", "/", "*", "\\",
		};

		private static String RandomText(Random random, Int32 pieces)
		{
			StringBuilder builder = new StringBuilder();
			for (Int32 i = 0; i < pieces; i++)
			{
				builder.Append(_pieces[random.Next(_pieces.Length)]);
			}

			return builder.ToString();
		}

		private static String Describe(String text, Int32 offset, String inserted)
		{
			Int32 from = Math.Max(0, offset - 12);
			String around = text.Substring(from, Math.Min(30, text.Length - from));
			return "..." + around.Replace("\r", "R").Replace("\n", "N") + "... inserted [" + inserted.Replace("\r", "R").Replace("\n", "N") + "]";
		}

		private static void AssertSame(LexicalMap expected, LexicalMap actual, String context)
		{
			Assert.True(expected.Spans.SequenceEqual(actual.Spans), "the maps differ " + context + ": expected " + expected.Spans.Count + " spans, got " + actual.Spans.Count);
		}

		[Fact]
		public void TypingACommentOpener_ChangesEverythingThatFollows_AndClosingItRestoresTheMap()
		{
			String text = "a := 1;\r\n\"s\"; // c\r\nb := {x};\r\n/* d */\r\n";
			TextDocument document = new TextDocument(text);
			using DocumentLexicalMap map = new DocumentLexicalMap(document);
			Assert.NotEmpty(map.Current.Spans);

			document.Insert(0, "/*");
			AssertSame(LexicalMap.Build(document.Text), map.Current, "after opening a comment at the start");
			Assert.Equal(LexicalKind.BlockComment, map.Current.GetKindAt(20));

			document.Insert(10, "*/");
			AssertSame(LexicalMap.Build(document.Text), map.Current, "after closing it");

			document.Remove(0, 2);
			AssertSame(LexicalMap.Build(document.Text), map.Current, "after removing the opener");
		}

		[Fact]
		public void EveryEditOfARandomText_GivesTheMapOfAFullScan()
		{
			Random random = new Random(7);
			for (Int32 round = 0; round < 40; round++)
			{
				TextDocument document = new TextDocument(RandomText(random, 40 + random.Next(300)));
				using DocumentLexicalMap map = new DocumentLexicalMap(document);
				_ = map.Current;
				for (Int32 edit = 0; edit < 60; edit++)
				{
					Int32 offset = random.Next(document.TextLength + 1);
					Int32 removed = random.Next(3) == 0 ? random.Next(Math.Min(30, document.TextLength - offset + 1)) : 0;
					String inserted = random.Next(4) == 0 ? String.Empty : RandomText(random, 1 + random.Next(3));
					document.Replace(offset, removed, inserted);

					// Now and then two edits happen before the map is asked for.
					if (random.Next(5) == 0)
					{
						Int32 second = random.Next(document.TextLength + 1);
						document.Insert(second, RandomText(random, 1));
					}

					AssertSame(LexicalMap.Build(document.Text), map.Current, "in round " + round + " after edit " + edit);
				}
			}
		}

		[Fact]
		public void TheUpdateOfOneEditNeverGivesAWrongMap_AndMostOftenSucceeds()
		{
			Random random = new Random(11);
			Int32 updated = 0;
			Int32 total = 0;
			for (Int32 round = 0; round < 30; round++)
			{
				String text = RandomText(random, 100 + random.Next(2000));
				for (Int32 edit = 0; edit < 30; edit++)
				{
					LexicalMap before = LexicalMap.Build(text);
					Int32 offset = random.Next(text.Length + 1);
					Int32 removed = random.Next(Math.Min(10, text.Length - offset + 1));
					String inserted = RandomText(random, random.Next(3));
					String after = text.Remove(offset, removed).Insert(offset, inserted);

					LexicalMap? map = LexicalMap.TryUpdate(before, new StringTextSource(after), offset, removed, inserted.Length);

					total++;
					if (map is not null)
					{
						updated++;
						AssertSame(LexicalMap.Build(after), map, "with an edit of " + removed + " removed and " + inserted.Length + " inserted characters at " + offset + ": " + Describe(text, offset, inserted));
					}

					text = after;
				}
			}

			Assert.True(updated > total * 0.9, updated + " of " + total + " edits were followed without scanning the whole text");
		}

		[Fact]
		public void AnEditInABigDocumentOnlyReadsTheTextAroundIt()
		{
			StringBuilder builder = new StringBuilder();
			for (Int32 i = 0; i < 20000; i++)
			{
				builder.Append("   display_msg(\"line ").Append(i).Append("\"); // note\r\n");
			}

			TextDocument document = new TextDocument(builder.ToString());
			using DocumentLexicalMap map = new DocumentLexicalMap(document);
			LexicalMap first = map.Current;

			document.Insert(document.TextLength / 2, "x");
			LexicalMap second = map.Current;

			Assert.NotSame(first, second);
			Assert.Equal(first.Spans.Count, second.Spans.Count);
			AssertSame(LexicalMap.Build(document.Text), second, "after typing in the middle");
		}

		[Fact]
		public void TheEmptyTextAndTheDisposedMapAreHandled()
		{
			TextDocument document = new TextDocument("abc");
			DocumentLexicalMap map = new DocumentLexicalMap(document);
			Assert.Empty(map.Current.Spans);

			document.Remove(0, 3);
			Assert.Empty(map.Current.Spans);

			document.Insert(0, "// x");
			Assert.Single(map.Current.Spans);

			map.Dispose();
			document.Insert(0, "\"");
			Assert.Equal(LexicalKind.String, LexicalMap.Build(document.Text).GetKindAt(0));
		}
	}
}