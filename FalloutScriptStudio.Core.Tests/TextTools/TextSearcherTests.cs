namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class TextSearcherTests
	{
		private readonly TextSearcher _searcher = new TextSearcher();

		private const String Text = "Node001 node002 mynode001 NODE001;\r\nnode001_x";

		[Fact]
		public void FindNext_IsCaseInsensitiveByDefault()
		{
			TextSpan? match = this._searcher.FindNext(Text, new SearchQuery("node001"), 1, false);

			Assert.Equal(new TextSpan(18, 7), match);
		}

		[Fact]
		public void FindNext_MatchCase_SkipsOtherCases()
		{
			TextSpan? match = this._searcher.FindNext(Text, new SearchQuery("NODE001") { MatchCase = true }, 1, false);

			Assert.Equal(new TextSpan(26, 7), match);
		}

		[Fact]
		public void FindNext_WholeWord_SkipsPartsOfOtherWords()
		{
			var query = new SearchQuery("node001") { WholeWord = true };

			Assert.Equal(new TextSpan(0, 7), this._searcher.FindNext(Text, query, 0, false));
			Assert.Equal(new TextSpan(26, 7), this._searcher.FindNext(Text, query, 1, false));
			Assert.Null(this._searcher.FindNext(Text, query, 27, false));
		}

		[Fact]
		public void FindNext_Wrap_ContinuesFromStart()
		{
			var query = new SearchQuery("node002");

			Assert.Null(this._searcher.FindNext(Text, query, 20, false));
			Assert.Equal(new TextSpan(8, 7), this._searcher.FindNext(Text, query, 20, true));
		}

		[Fact]
		public void FindNext_StartPastEnd_IsClamped()
		{
			Assert.Equal(new TextSpan(0, 7), this._searcher.FindNext(Text, new SearchQuery("node001"), 1000, true));
			Assert.Null(this._searcher.FindNext(Text, new SearchQuery("node001"), 1000, false));
		}

		[Fact]
		public void FindNext_Regex()
		{
			var query = new SearchQuery(@"node00\d") { UseRegex = true, MatchCase = true };

			Assert.Equal(new TextSpan(8, 7), this._searcher.FindNext(Text, query, 0, false));
		}

		[Fact]
		public void FindNext_WholeWordWithSymbolEdges_Works()
		{
			var query = new SearchQuery("a+b") { WholeWord = true };

			Assert.Equal(new TextSpan(2, 3), this._searcher.FindNext("x a+b y", query, 0, false));
			Assert.Null(this._searcher.FindNext("xa+by", query, 0, false));
		}

		[Fact]
		public void FindPrevious_FindsLastMatchEndingBeforeOffset()
		{
			var query = new SearchQuery("node001") { WholeWord = true };

			Assert.Equal(new TextSpan(26, 7), this._searcher.FindPrevious(Text, query, 40, false));
			Assert.Equal(new TextSpan(0, 7), this._searcher.FindPrevious(Text, query, 26, false));
			Assert.Null(this._searcher.FindPrevious(Text, query, 5, false));
		}

		[Fact]
		public void FindPrevious_Wrap_ContinuesFromEnd()
		{
			var query = new SearchQuery("node001");

			Assert.Equal(new TextSpan(36, 7), this._searcher.FindPrevious(Text, query, 5, true));
		}

		[Fact]
		public void FindPrevious_PlainSearchAllowsOverlappingMatches()
		{
			Assert.Equal(new TextSpan(1, 2), this._searcher.FindPrevious("aaa", new SearchQuery("aa"), 3, false));
		}

		[Fact]
		public void FindPrevious_Regex()
		{
			var query = new SearchQuery(@"\d+") { UseRegex = true };

			Assert.Equal(new TextSpan(10, 3), this._searcher.FindPrevious("ab 12 345 678 x", query, 15, false));
			Assert.Equal(new TextSpan(6, 3), this._searcher.FindPrevious("ab 12 345 678 x", query, 9, false));
		}

		[Fact]
		public void FindAll_ReturnsNonOverlappingMatches()
		{
			IReadOnlyList<TextSpan> matches = this._searcher.FindAll("aaaa", new SearchQuery("aa"));

			Assert.Equal(new[] { new TextSpan(0, 2), new TextSpan(2, 2) }, matches);
		}

		[Fact]
		public void FindAll_IgnoresEmptyRegexMatches()
		{
			IReadOnlyList<TextSpan> matches = this._searcher.FindAll("a b", new SearchQuery("x*") { UseRegex = true });

			Assert.Empty(matches);
		}

		[Fact]
		public void FindAllLines_ReturnsOneRowPerMatchingLine()
		{
			IReadOnlyList<SearchResultRow> rows = this._searcher.FindAllLines("  call a; call a;\r\nb;\r\n\tcall a;", "x.ssl", new SearchQuery("call a"));

			Assert.Equal(2, rows.Count);
			Assert.Equal(new SearchResultRow("x.ssl", 1, 3, 6, "call a; call a;"), rows[0]);
			Assert.Equal(new SearchResultRow("x.ssl", 3, 2, 6, "call a;"), rows[1]);
		}

		[Fact]
		public void IsMatch_And_Validate()
		{
			Assert.True(this._searcher.IsMatch("hello", new SearchQuery("ELL")));
			Assert.False(this._searcher.IsMatch("hello", new SearchQuery("ELL") { MatchCase = true }));
			Assert.True(new SearchQuery("(").TryValidate(out _));
			Assert.False(new SearchQuery("(") { UseRegex = true }.TryValidate(out String? error));
			Assert.NotNull(error);
			Assert.False(new SearchQuery(String.Empty).TryValidate(out _));
		}

		[Fact]
		public void ReplaceAll_ReplacesEveryMatch_EvenWhenTheReplacementContainsTheSearchText()
		{
			ReplaceResult result = this._searcher.ReplaceAll("a a a", new SearchQuery("a"), "aa");

			Assert.Equal("aa aa aa", result.NewText);
			Assert.Equal(3, result.Count);
		}

		[Fact]
		public void ReplaceAll_WholeWordAndRegexGroups()
		{
			Assert.Equal("Node002 Node002 mynode001 Node002;", this._searcher.ReplaceAll("Node001 node001 mynode001 NODE001;", new SearchQuery("node001") { WholeWord = true }, "Node002").NewText);
			ReplaceResult groups = this._searcher.ReplaceAll("call Foo; call Bar;", new SearchQuery(@"call (\w+);") { UseRegex = true }, "run($1);");
			Assert.Equal("run(Foo); run(Bar);", groups.NewText);
		}

		[Fact]
		public void ReplaceNext_ReplacesTheFirstMatchAfterTheOffset()
		{
			TextEdit? edit = this._searcher.ReplaceNext("a b a b", new SearchQuery("b"), "X", 3, false);

			Assert.Equal("a b a X", TextEditApplier.Apply("a b a b", new[] { edit! }));
			Assert.Null(this._searcher.ReplaceNext("a b", new SearchQuery("z"), "X", 0, true));
		}

		[Fact]
		public void TextReplacer_KeepsTheFirstGroupWhenAsked()
		{
			var regex = new System.Text.RegularExpressions.Regex(@"(\bprocedure\s|\bcall\s)\s*Foo\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			String text = "procedure foo; call  Foo; x := Foo;";

			ReplaceResult result = TextReplacer.Replace(regex, text, "Bar", true);

			Assert.Equal("procedure Bar; call Bar; x := Foo;", result.NewText);
			Assert.Equal("Bar; Bar; x := Foo;", TextReplacer.Replace(regex, text, "Bar", false).NewText);
		}

		[Fact]
		public void FindPrevious_WholeWord_CaretInsideAWord_DoesNotFindThePrefixOfThatWord()
		{
			var query = new SearchQuery("foo") { WholeWord = true };

			Assert.Equal(new TextSpan(0, 3), this._searcher.FindPrevious("foo foobar", query, 7, false));
			Assert.Null(this._searcher.FindPrevious("foobar", query, 3, false));
			Assert.Null(this._searcher.FindPrevious("foobar", query, 3, true));
		}

		[Fact]
		public void FindPrevious_Regex_LookaheadSeesTheTextAfterTheOffset()
		{
			var query = new SearchQuery("foo(?!bar)") { UseRegex = true };

			Assert.Null(this._searcher.FindPrevious("foobar", query, 3, false));
			Assert.Equal(new TextSpan(7, 3), this._searcher.FindPrevious("foobar foo", query, 10, false));
		}

		[Fact]
		public void FindPrevious_WholeWord_MatchEndingExactlyAtTheOffsetIsFound()
		{
			var query = new SearchQuery("foo") { WholeWord = true };

			Assert.Equal(new TextSpan(0, 3), this._searcher.FindPrevious("foo bar", query, 3, false));
		}

		[Fact]
		public void MatchTimeout_HasASafeDefault()
		{
			TimeSpan timeout = new SearchQuery("x") { UseRegex = true }.MatchTimeout;

			Assert.True(timeout > TimeSpan.Zero && timeout <= TimeSpan.FromSeconds(5));
			Assert.Equal(timeout, new SearchQuery("x") { UseRegex = true }.CreateRegex()!.MatchTimeout);
			Assert.Equal(timeout, new SearchQuery("x") { WholeWord = true }.CreateRegex()!.MatchTimeout);
		}

		[Fact]
		public void CatastrophicRegex_ThrowsATimeoutExceptionInsteadOfHanging()
		{
			String text = new String('a', 45) + "c";
			var query = new SearchQuery("(a+)+b") { UseRegex = true, MatchTimeout = TimeSpan.FromMilliseconds(50) };

			Assert.True(query.TryValidate(out String? _));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.FindNext(text, query, 0, true));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.FindPrevious(text, query, text.Length, true));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.FindAll(text, query));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.IsMatch(text, query));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.FindAllLines(text, "f.ssl", query));
			Assert.Throws<RegexMatchTimeoutException>(() => this._searcher.ReplaceAll(text, query, "x"));
		}
	}
}