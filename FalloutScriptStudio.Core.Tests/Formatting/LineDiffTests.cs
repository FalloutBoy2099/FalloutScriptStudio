namespace FalloutScriptStudio.Core.Tests.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public sealed class LineDiffTests
	{
		[Fact]
		public void EqualTexts_HaveNoEdit()
		{
			Assert.Empty(LineDiff.Compute("a\r\nb\r\n", "a\r\nb\r\n"));
		}

		[Fact]
		public void AChangedLine_IsOneEditOfThatLine()
		{
			IReadOnlyList<TextEdit> edits = LineDiff.Compute("a\r\nb\r\nc\r\n", "a\r\nB\r\nc\r\n");

			TextEdit edit = Assert.Single(edits);
			Assert.Equal(new TextEdit(3, 3, "B\r\n"), edit);
		}

		[Fact]
		public void InsertedAndDeletedLines_AreTheirOwnEdits_AndTheLinesBetweenAreKept()
		{
			String oldText = "a\nb\nc\nd\ne\nf\n";
			String newText = "a\nb\nX\nc\nd\n";

			IReadOnlyList<TextEdit> edits = LineDiff.Compute(oldText, newText);

			Assert.Equal(2, edits.Count);
			Assert.Equal(newText, TextEditApplier.Apply(oldText, edits));
		}

		[Fact]
		public void TheLastLineWithoutALineBreakIsHandled()
		{
			Assert.Equal("a\nb", TextEditApplier.Apply("a\nx", LineDiff.Compute("a\nx", "a\nb")));
			Assert.Equal("a\nb\n", TextEditApplier.Apply("a\nb", LineDiff.Compute("a\nb", "a\nb\n")));
			Assert.Equal("a", TextEditApplier.Apply("a\nb\n", LineDiff.Compute("a\nb\n", "a")));
		}

		[Fact]
		public void RandomChanges_AreAlwaysReproduced()
		{
			Random random = new Random(5);
			for (Int32 round = 0; round < 400; round++)
			{
				List<String> lines = Enumerable.Range(0, random.Next(0, 40)).Select(i => "line " + random.Next(12)).ToList();
				String oldText = String.Join(random.Next(2) == 0 ? "\r\n" : "\n", lines);
				List<String> changed = new(lines);
				for (Int32 k = random.Next(0, 8); k > 0; k--)
				{
					Int32 kind = random.Next(3);
					if (kind == 0 && changed.Count > 0)
					{
						changed.RemoveAt(random.Next(changed.Count));
					}
					else if (kind == 1)
					{
						changed.Insert(random.Next(changed.Count + 1), "new " + random.Next(5));
					}
					else if (changed.Count > 0)
					{
						changed[random.Next(changed.Count)] = "edited " + random.Next(5);
					}
				}

				String newText = String.Join(random.Next(2) == 0 ? "\r\n" : "\n", changed) + (random.Next(3) == 0 ? "\n" : String.Empty);
				IReadOnlyList<TextEdit> edits = LineDiff.Compute(oldText, newText);

				Assert.Equal(newText, TextEditApplier.Apply(oldText, edits));
			}
		}

		[Fact]
		public void ADocumentThatChangesEverywhere_StillGivesATextThatApplies()
		{
			String oldText = String.Concat(Enumerable.Range(0, 5000).Select(i => "line " + i + "\r\n"));
			String newText = String.Concat(Enumerable.Range(0, 5200).Select(i => "other " + (i * 7) + "\r\n"));

			Assert.Equal(newText, TextEditApplier.Apply(oldText, LineDiff.Compute(oldText, newText)));
		}

		[Fact]
		public void Restrict_KeepsOnlyTheEditsThatTouchTheSelectedLines()
		{
			String text = "a\r\nb\r\nc\r\nd\r\ne\r\n";
			IReadOnlyList<TextEdit> edits = LineDiff.Compute(text, "A\r\nb\r\nC\r\nd\r\nE\r\n");

			IReadOnlyList<TextEdit> second = LineDiff.Restrict(text, edits, new TextSpan(text.IndexOf('c', StringComparison.Ordinal), 0));
			IReadOnlyList<TextEdit> range = LineDiff.Restrict(text, edits, new TextSpan(0, text.IndexOf('d', StringComparison.Ordinal)));

			Assert.Equal("a\r\nb\r\nC\r\nd\r\ne\r\n", TextEditApplier.Apply(text, second));
			Assert.Equal("A\r\nb\r\nC\r\nd\r\ne\r\n", TextEditApplier.Apply(text, range));
		}

		[Fact]
		public void Restrict_AnInsertionBeforeTheFirstSelectedLineBelongsToIt()
		{
			String text = "a\nb\n";
			IReadOnlyList<TextEdit> edits = LineDiff.Compute(text, "a\n\nb\n");

			Assert.Single(LineDiff.Restrict(text, edits, new TextSpan(2, 1)));
			Assert.Empty(LineDiff.Restrict(text, edits, new TextSpan(0, 1)));
		}
	}
}