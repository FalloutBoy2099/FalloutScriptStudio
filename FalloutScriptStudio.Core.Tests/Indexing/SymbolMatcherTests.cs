namespace FalloutScriptStudio.Core.Tests.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Indexing;
	using Xunit;

	public class SymbolMatcherTests
	{
		[Theory]
		[InlineData("level", "level", SymbolMatcher.Exact)]
		[InlineData("LEVEL", "level", SymbolMatcher.Exact)]
		[InlineData("level_up", "level", SymbolMatcher.Prefix)]
		[InlineData("LevelUp", "lev", SymbolMatcher.Prefix)]
		[InlineData("get_level", "level", SymbolMatcher.WordStart)]
		[InlineData("GetLevel", "level", SymbolMatcher.WordStart)]
		[InlineData("get_dude_level", "dude", SymbolMatcher.WordStart)]
		[InlineData("Node001", "001", SymbolMatcher.WordStart)]
		[InlineData("unlevelled", "level", SymbolMatcher.Substring)]
		[InlineData("get_dude_level", "gdl", SymbolMatcher.Subsequence)]
		[InlineData("get_dude_level", "xyz", SymbolMatcher.NoMatch)]
		[InlineData("abc", "abcd", SymbolMatcher.NoMatch)]
		[InlineData("abc", "b", SymbolMatcher.Substring)]
		[InlineData("abc", "", SymbolMatcher.Prefix)]
		public void GetTier_RanksTheMatch(String name, String term, Int32 expected)
		{
			Assert.Equal(expected, SymbolMatcher.GetTier(name, term));
		}

		[Fact]
		public void GetTier_AOneLetterTermDoesNotMatchBySubsequence()
		{
			Assert.Equal(SymbolMatcher.NoMatch, SymbolMatcher.GetTier("alpha", "z"));
			Assert.Equal(SymbolMatcher.Subsequence, SymbolMatcher.GetTier("alpha", "ah"));
		}

		[Fact]
		public void SplitTerms_SplitsOnWhiteSpace()
		{
			Assert.Equal(new[] { "a", "b", "c" }, SymbolMatcher.SplitTerms("  a  b\tc ").ToArray());
			Assert.Empty(SymbolMatcher.SplitTerms("   "));
			Assert.Empty(SymbolMatcher.SplitTerms(null));
		}

		[Fact]
		public void GetTier_ForASymbol_UsesTheFirstTermForTheTierAndTheOthersForTheFileToo()
		{
			IndexedSymbol symbol = new IndexedSymbol("start", IndexedSymbolKind.Procedure, @"C:\mod\scripts\gl_g_ammobox.ssl", 3);

			Assert.Equal(SymbolMatcher.Prefix, SymbolMatcher.GetTier(symbol, new[] { "sta" }));
			Assert.Equal(SymbolMatcher.Prefix, SymbolMatcher.GetTier(symbol, new[] { "sta", "ammobox" }));
			Assert.Equal(SymbolMatcher.Exact, SymbolMatcher.GetTier(symbol, new[] { "start", "art" }));
			Assert.Equal(SymbolMatcher.NoMatch, SymbolMatcher.GetTier(symbol, new[] { "sta", "nothere" }));
			Assert.Equal(SymbolMatcher.NoMatch, SymbolMatcher.GetTier(symbol, new[] { "ammobox", "start" }));
		}

		[Fact]
		public void Compare_OrdersByTierThenLengthThenNameThenFileThenLine()
		{
			IndexedSymbol shortName = new IndexedSymbol("ab", IndexedSymbolKind.Procedure, "a.ssl", 1);
			IndexedSymbol longName = new IndexedSymbol("abc", IndexedSymbolKind.Procedure, "a.ssl", 1);
			IndexedSymbol otherFile = new IndexedSymbol("ab", IndexedSymbolKind.Procedure, "b.ssl", 1);
			IndexedSymbol otherLine = new IndexedSymbol("ab", IndexedSymbolKind.Procedure, "a.ssl", 9);

			List<IndexedSymbol> sorted = new[] { otherLine, longName, otherFile, shortName }
				.OrderBy(s => s, Comparer<IndexedSymbol>.Create((x, y) => SymbolMatcher.Compare(1, x, 1, y)))
				.ToList();

			Assert.Equal(new[] { shortName, otherLine, otherFile, longName }, sorted);
			Assert.True(SymbolMatcher.Compare(0, longName, 1, shortName) < 0);
		}
	}
}
