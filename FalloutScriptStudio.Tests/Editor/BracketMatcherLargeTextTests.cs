namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Text;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	/// <summary>The bracket search reads the text in chunks; its result must not depend on where the chunks end.</summary>
	public sealed class BracketMatcherLargeTextTests
	{
		private const String Openers = "([{";
		private const String Closers = ")]}";

		/// <summary>The character by character search the matcher replaced.</summary>
		private static BracketPair? Reference(String text, LexicalMap map, Int32 offset)
		{
			Char c = text[offset];
			Int32 opener = Openers.IndexOf(c);
			Int32 closer = Closers.IndexOf(c);
			if ((opener < 0 && closer < 0) || !IsBracket(map, offset))
			{
				return null;
			}

			if (opener >= 0)
			{
				Int32 depth = 0;
				for (Int32 i = offset; i < text.Length; i++)
				{
					if (text[i] == c && IsBracket(map, i))
					{
						depth++;
					}
					else if (text[i] == Closers[opener] && IsBracket(map, i) && --depth == 0)
					{
						return new BracketPair(offset, i);
					}
				}

				return null;
			}

			Int32 level = 0;
			for (Int32 i = offset; i >= 0; i--)
			{
				if (text[i] == c && IsBracket(map, i))
				{
					level++;
				}
				else if (text[i] == Openers[closer] && IsBracket(map, i) && --level == 0)
				{
					return new BracketPair(i, offset);
				}
			}

			return null;
		}

		private static Boolean IsBracket(LexicalMap map, Int32 offset)
		{
			LexicalKind kind = map.GetKindAt(offset);
			return kind == LexicalKind.Code || kind == LexicalKind.Braces;
		}

		[Theory]
		[InlineData(10)]
		[InlineData(4095)]
		[InlineData(4096)]
		[InlineData(4097)]
		[InlineData(8191)]
		[InlineData(8192)]
		[InlineData(30000)]
		public void TheOpeningBracketFindsItsPartnerAtAnyDistance(Int32 distance)
		{
			String text = "x(" + new String('a', distance) + ")y";
			TextDocument document = new TextDocument(text);

			BracketPair? pair = BracketMatcher.FindPairOf(document, LexicalMap.Build(text), 1);

			Assert.Equal(new BracketPair(1, distance + 2), pair);
		}

		[Theory]
		[InlineData(10)]
		[InlineData(4095)]
		[InlineData(4096)]
		[InlineData(4097)]
		[InlineData(8192)]
		[InlineData(30000)]
		public void TheClosingBracketFindsItsPartnerAtAnyDistance(Int32 distance)
		{
			String text = "x(" + new String('a', distance) + ")y";
			TextDocument document = new TextDocument(text);

			BracketPair? pair = BracketMatcher.FindPairOf(document, LexicalMap.Build(text), distance + 2);

			Assert.Equal(new BracketPair(1, distance + 2), pair);
		}

		[Fact]
		public void BracketsWithoutAPartnerAreNotFoundInABigText()
		{
			String text = "(" + new String('a', 20000) + "\r\n// ) in a comment\r\n\"string )\"" + new String('b', 5000);
			TextDocument document = new TextDocument(text);
			LexicalMap map = LexicalMap.Build(text);

			Assert.Null(BracketMatcher.FindPairOf(document, map, 0));
			String closing = new String('a', 9000) + "\r\n/* ( */ " + new String('b', 9000) + ")";
			Assert.Null(BracketMatcher.FindPairOf(new TextDocument(closing), LexicalMap.Build(closing), closing.Length - 1));
		}

		[Fact]
		public void TheChunkedSearchGivesTheSameAnswersAsTheCharacterBySearchOnRandomTexts()
		{
			Random random = new Random(20260101);
			String[] pieces = { "(", ")", "[", "]", "{", "}", "\"", "//", "/*", "*/", "\r\n", "abc", " ", "x" };
			for (Int32 round = 0; round < 12; round++)
			{
				StringBuilder builder = new StringBuilder();
				Int32 target = 3000 + random.Next(20000);
				while (builder.Length < target)
				{
					builder.Append(pieces[random.Next(pieces.Length)]);
					if (random.Next(40) == 0)
					{
						builder.Append(new String('q', random.Next(5000)));
					}
				}

				String text = builder.ToString();
				TextDocument document = new TextDocument(text);
				LexicalMap map = LexicalMap.Build(text);
				for (Int32 sample = 0; sample < 150; sample++)
				{
					Int32 offset = random.Next(text.Length);
					Assert.Equal(Reference(text, map, offset), BracketMatcher.FindPairOf(document, map, offset));
				}
			}
		}
	}
}