namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class TextEditTests
	{
		[Fact]
		public void Apply_AppliesEditsGivenInAnyOrder()
		{
			var edits = new[]
			{
				new TextEdit(6, 5, "there"),
				TextEdit.Insert(0, ">> "),
				TextEdit.Delete(5, 1),
			};

			Assert.Equal(">> hellothere", TextEditApplier.Apply("hello world", edits));
		}

		[Fact]
		public void Apply_OverlappingEdits_Throw()
		{
			var edits = new[] { new TextEdit(0, 5, "a"), new TextEdit(3, 2, "b") };

			Assert.Throws<ArgumentException>(() => TextEditApplier.Apply("hello world", edits));
		}

		[Fact]
		public void Apply_EditOutsideText_Throws()
		{
			Assert.Throws<ArgumentException>(() => TextEditApplier.Apply("abc", new[] { new TextEdit(2, 5, "x") }));
		}

		[Fact]
		public void Apply_InsertionsAtSameOffset_KeepGivenOrder()
		{
			String result = TextEditApplier.Apply("ab", new[] { TextEdit.Insert(1, "X"), TextEdit.Insert(1, "Y") });

			Assert.Equal("aXYb", result);
		}

		[Fact]
		public void CreateDiff_KeepsCommonPrefixAndSuffix()
		{
			TextEdit? diff = TextEditApplier.CreateDiff("procedure Foo begin", "procedure Barbaz begin");

			Assert.NotNull(diff);
			Assert.Equal(10, diff!.Offset);
			Assert.Equal(3, diff.Length);
			Assert.Equal("Barbaz", diff.NewText);
		}

		[Fact]
		public void CreateDiff_EqualTexts_ReturnsNull()
		{
			Assert.Null(TextEditApplier.CreateDiff("same", "same"));
		}

		[Theory]
		[InlineData(0, 0)]
		[InlineData(3, 5)]
		[InlineData(4, 6)]
		[InlineData(6, 7)]
		[InlineData(8, 8)]
		public void MapOffset_FollowsInsertionsAndDeletions(Int32 offset, Int32 expected)
		{
			var edits = new[] { TextEdit.Insert(2, "++"), TextEdit.Delete(5, 2) };

			// "abcdefghij" -> "ab++cdeghij": offsets inside the deleted range collapse to its start.
			Int32 mapped = TextEditApplier.MapOffset(offset, edits);

			Assert.Equal(expected, mapped);
		}

		[Fact]
		public void Builder_ReportsSequentialEditsInOriginalCoordinates()
		{
			var builder = new TextEditBuilder("0123456789");

			builder.Remove(2, 3);          // 0156789
			builder.Insert(0, "AB");       // AB0156789
			builder.Replace(7, 1, "xyz");  // AB01567xyz9

			Assert.Equal("AB01567xyz9", builder.CurrentText);
			IReadOnlyList<TextEdit> edits = builder.ToEdits();
			Assert.Equal("AB01567xyz9", TextEditApplier.Apply("0123456789", edits));
			Assert.Equal(3, edits.Count);
		}

		[Fact]
		public void Builder_RemovingInsertedText_LeavesNoEdit()
		{
			var builder = new TextEditBuilder("hello");

			builder.Insert(2, "XYZ");
			builder.Remove(2, 3);

			Assert.Empty(builder.ToEdits());
			Assert.Equal("hello", builder.CurrentText);
		}

		[Fact]
		public void Builder_MovingText_ProducesDeleteAndInsert()
		{
			var builder = new TextEditBuilder("A-B-C");

			builder.Remove(0, 2);
			builder.Insert(builder.Length, "-A");

			Assert.Equal("B-C-A", builder.CurrentText);
			Assert.Equal("B-C-A", TextEditApplier.Apply("A-B-C", builder.ToEdits()));
		}

		[Fact]
		public void LineMap_HandlesAllTerminators()
		{
			var map = new TextLineMap("one\r\ntwo\nthree\rfour");

			Assert.Equal(4, map.LineCount);
			Assert.Equal("two", map.GetLineText(2));
			Assert.Equal(5, map.GetLineStart(2));
			Assert.Equal(8, map.GetLineEnd(2));
			Assert.Equal(1, map.GetTerminatorLength(2));
			Assert.Equal(2, map.GetTerminatorLength(1));
			Assert.Equal("four", map.GetLineText(4));
		}

		[Fact]
		public void LineMap_TextEndingWithTerminator_HasEmptyLastLine()
		{
			var map = new TextLineMap("a\r\n");

			Assert.Equal(2, map.LineCount);
			Assert.Equal(0, map.GetLineLength(2));
			Assert.True(map.IsBlank(2));
		}

		[Theory]
		[InlineData(0, 1, 0)]
		[InlineData(3, 1, 3)]
		[InlineData(5, 2, 0)]
		[InlineData(7, 2, 2)]
		[InlineData(100, 2, 3)]
		public void LineMap_GetLineAndColumnFromOffset(Int32 offset, Int32 line, Int32 column)
		{
			var map = new TextLineMap("abc\r\ndef");

			Assert.Equal(line, map.GetLineFromOffset(offset));
			Assert.Equal(column, map.GetColumnFromOffset(offset));
		}

		[Fact]
		public void LineMap_SelectedLines_ExcludeLastLineWhenSelectionEndsAtItsStart()
		{
			var map = new TextLineMap("a\r\nb\r\nc\r\n");

			Assert.Equal(new FalloutScriptStudio.Core.Parsing.LineRange(1, 2), map.GetSelectedLines(new TextSpan(0, 6)));
			Assert.Equal(new FalloutScriptStudio.Core.Parsing.LineRange(2, 2), map.GetSelectedLines(new TextSpan(4, 0)));
		}

		[Theory]
		[InlineData("a\r\nb", "\r\n")]
		[InlineData("a\nb", "\n")]
		[InlineData("single", "\r\n")]
		public void DetectNewLine_UsesFirstTerminator(String text, String expected)
		{
			Assert.Equal(expected, TextLineMap.DetectNewLine(text));
		}
	}
}
