namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class FuzzyMatcherTests
	{
		private static readonly String[] Names =
		{
			"File > Save",
			"File > Save All",
			"File > Save As...",
			"Code > Format Document",
			"Code > Format Selection",
			"Edit > Find in Files...",
			"Edit > Go to Line...",
			"View > Zoom In",
		};

		[Fact]
		public void AnEmptyPatternMatchesEverythingWithoutScore()
		{
			Assert.Equal(0, FuzzyMatcher.Score(String.Empty, "anything"));
			Assert.Equal(0, FuzzyMatcher.Score("   ", "anything"));
		}

		[Fact]
		public void TheTiersAreRankedExactPrefixWordStartSubstringSubsequence()
		{
			Int32 exact = FuzzyMatcher.Score("save", "Save")!.Value;
			Int32 prefix = FuzzyMatcher.Score("save", "Save All")!.Value;
			Int32 wordStart = FuzzyMatcher.Score("save", "File Save")!.Value;
			Int32 substring = FuzzyMatcher.Score("ave", "File Save")!.Value;
			Int32 subsequence = FuzzyMatcher.Score("sve", "File Save")!.Value;

			Assert.True(exact > prefix, "exact > prefix");
			Assert.True(prefix > wordStart, "prefix > word start");
			Assert.True(wordStart > substring, "word start > substring");
			Assert.True(substring > subsequence, "substring > subsequence");
		}

		[Fact]
		public void ANameThatDoesNotContainTheLettersInOrderDoesNotMatch()
		{
			Assert.Null(FuzzyMatcher.Score("xyz", "Save"));
			Assert.Null(FuzzyMatcher.Score("evas", "Save"));
			Assert.Null(FuzzyMatcher.Score("save", String.Empty));
		}

		[Theory]
		[InlineData("FORMAT", "format document")]
		[InlineData("format", "FORMAT DOCUMENT")]
		[InlineData("cafe", "Café")]
		[InlineData("CAFÉ", "cafe")]
		[InlineData("uber", "Über")]
		[InlineData("lodz", "Łódź")]
		[InlineData("fmtdoc", "Format Document")]
		public void TheLetterCaseAndTheAccentsAreIgnored(String pattern, String name)
		{
			Assert.NotNull(FuzzyMatcher.Score(pattern, name));
		}

		[Fact]
		public void SeveralWordsMustAllMatch_InAnyPlace()
		{
			Assert.NotNull(FuzzyMatcher.Score("code format", "Code > Format Document"));
			Assert.NotNull(FuzzyMatcher.Score("doc format", "Code > Format Document"));
			Assert.Null(FuzzyMatcher.Score("code zoom", "Code > Format Document"));
		}

		[Fact]
		public void WordsInTheOrderOfTheNameScoreSlightlyHigher()
		{
			Int32 inOrder = FuzzyMatcher.Score("format doc", "Format Document")!.Value;
			Int32 reversed = FuzzyMatcher.Score("doc format", "Format Document")!.Value;

			Assert.True(inOrder > reversed);
		}

		[Fact]
		public void AShorterNameWinsAmongNamesThatStartWithTheText()
		{
			Int32 save = FuzzyMatcher.Score("sav", "Save")!.Value;
			Int32 saveAll = FuzzyMatcher.Score("sav", "Save All")!.Value;

			Assert.True(save > saveAll);
		}

		[Fact]
		public void AnEarlierWordWinsOverALaterOne()
		{
			Int32 early = FuzzyMatcher.Score("doc", "Document Format")!.Value;
			Int32 late = FuzzyMatcher.Score("doc", "Format Document")!.Value;

			Assert.True(early > late);
		}

		[Fact]
		public void ChineseNamesMatchBySubstringAndByLetters()
		{
			Assert.NotNull(FuzzyMatcher.Score("保存", "文件 > 保存全部"));
			Assert.NotNull(FuzzyMatcher.Score("保部", "文件 > 保存全部"));
			Assert.Null(FuzzyMatcher.Score("关闭", "文件 > 保存全部"));
		}

		[Fact]
		public void RankOrdersByScoreAndKeepsTheOrderOfEqualScores()
		{
			String[] ranked = FuzzyMatcher.Rank(Names, "save", name => name).ToArray();

			Assert.Equal(3, ranked.Length);
			Assert.Equal("File > Save", ranked[0]);
			Assert.Equal("File > Save All", ranked[1]);
			Assert.Equal("File > Save As...", ranked[2]);
		}

		[Fact]
		public void RankWithAnEmptyPatternReturnsEveryItemInItsOrder()
		{
			Assert.Equal(Names, FuzzyMatcher.Rank(Names, String.Empty, name => name));
		}

		[Fact]
		public void RankPutsAPrefixOfTheNameBeforeAWordStart()
		{
			String[] items = { "Edit > Find in Files...", "Find Next" };

			Assert.Equal(new[] { "Find Next", "Edit > Find in Files..." }, FuzzyMatcher.Rank(items, "find", name => name).ToArray());
		}

		[Fact]
		public void TheTierOfAScoreNamesTheKindOfMatch()
		{
			Assert.Equal(4, FuzzyMatcher.GetTier(FuzzyMatcher.Score("save", "Save")!.Value));
			Assert.Equal(3, FuzzyMatcher.GetTier(FuzzyMatcher.Score("save", "Save everything in the project now and then")!.Value));
			Assert.Equal(3, FuzzyMatcher.GetTier(FuzzyMatcher.Score("sav", "Save All")!.Value));
			Assert.Equal(2, FuzzyMatcher.GetTier(FuzzyMatcher.Score("all", "Save All")!.Value));
			Assert.Equal(1, FuzzyMatcher.GetTier(FuzzyMatcher.Score("ll", "Save All")!.Value));
			Assert.Equal(0, FuzzyMatcher.GetTier(FuzzyMatcher.Score("sal", "Save All")!.Value));
		}

		[Fact]
		public void FoldKeepsTheLength()
		{
			String text = "Łódź ÀÉÎÕÜ ß 保存";

			Assert.Equal(text.Length, FuzzyMatcher.Fold(text).Length);
		}

		[Theory]
		[InlineData("sav", "Save All", "0:3")]
		[InlineData("all", "Save All", "5:3")]
		[InlineData("ll", "Save All", "6:2")]
		[InlineData("sal", "Save All", "0:2;6:1")]
		[InlineData("EDITER", "Éditer", "0:6")]
		[InlineData("sa al", "Save All", "0:2;5:2")]
		[InlineData("code format", "Format Document", "0:6")]
		[InlineData("zzz", "Save All", "")]
		[InlineData("", "Save All", "")]
		public void TheMatchedCharactersAreFoundTheWayTheScoreLooksThemUp(String pattern, String name, String expected)
		{
			String actual = String.Join(";", FuzzyMatcher.FindMatchRanges(pattern, name).Select(range => range.Start + ":" + range.Length));

			Assert.Equal(expected, actual);
		}

		[Fact]
		public void AdjacentMatchesOfSeveralWordsAreMergedIntoOneRun()
		{
			IReadOnlyList<MatchRange> ranges = FuzzyMatcher.FindMatchRanges("sa ve", "Save");

			Assert.Equal(new[] { new MatchRange(0, 4) }, ranges);
		}

		[Fact]
		public void ThousandsOfNamesAreRankedQuickly()
		{
			String[] many = Enumerable.Range(0, 5000).Select(i => "Menu " + i + " > Command number " + i).ToArray();
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

			FuzzyMatcher.Rank(many, "cmd nbr 77", name => name);

			Assert.True(clock.ElapsedMilliseconds < 1000, "Ranking took " + clock.ElapsedMilliseconds + " ms.");
		}
	}
}