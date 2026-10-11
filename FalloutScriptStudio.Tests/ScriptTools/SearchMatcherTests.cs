namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using Xunit;

	public sealed class SearchMatcherTests
	{
		private static SearchMatcher Create(String text, Boolean matchCase = false, Boolean wholeWord = false, Boolean regex = false)
		{
			Boolean ok = SearchMatcher.TryCreate(new SearchOptions(text, matchCase, wholeWord, regex), out SearchMatcher? matcher, out String? error);
			Assert.True(ok, error);
			return matcher!;
		}

		[Fact]
		public void TryCreate_EmptyText_Fails()
		{
			Boolean ok = SearchMatcher.TryCreate(new SearchOptions(String.Empty, false, false, false), out SearchMatcher? matcher, out String? error);

			Assert.False(ok);
			Assert.Null(matcher);
			Assert.False(String.IsNullOrEmpty(error));
		}

		[Fact]
		public void TryCreate_InvalidRegex_ReportsTheErrorInsteadOfThrowing()
		{
			Boolean ok = SearchMatcher.TryCreate(new SearchOptions("(unclosed", false, false, true), out SearchMatcher? matcher, out String? error);

			Assert.False(ok);
			Assert.Null(matcher);
			Assert.Contains("regular expression", error, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void FindAll_IgnoresCaseUnlessMatchCaseIsSet()
		{
			Assert.Equal(2, Create("foo").FindAll("Foo foo").Count);
			Assert.Single(Create("foo", matchCase: true).FindAll("Foo foo"));
		}

		[Fact]
		public void FindAll_PlainTextIsNotInterpretedAsRegex()
		{
			SearchMatcher matcher = Create("a.b(");

			IReadOnlyList<LineMatch> matches = matcher.FindAll("xa.b( aab(");

			Assert.Single(matches);
			Assert.Equal(1, matches[0].Index);
			Assert.Equal(4, matches[0].Length);
		}

		[Fact]
		public void FindAll_WholeWord_SkipsPartsOfLongerWords()
		{
			SearchMatcher matcher = Create("end", wholeWord: true);

			IReadOnlyList<LineMatch> matches = matcher.FindAll("end endif send end_x (end) end");

			Assert.Equal(new[] { 0, 22, 27 }, matches.Select(match => match.Index).ToArray());
		}

		[Fact]
		public void FindAll_WholeWordWithSymbols_OnlyChecksWordEdges()
		{
			SearchMatcher matcher = Create("#define", wholeWord: true);

			Assert.Single(matcher.FindAll("  #define X"));
			Assert.Single(matcher.FindAll("x#define"));
			Assert.Empty(Create("define", wholeWord: true).FindAll("#defined"));
		}

		[Fact]
		public void FindAll_Regex_UsesTheLineAsContext()
		{
			SearchMatcher matcher = Create("^\\s*procedure\\s+(\\w+)", regex: true);

			Assert.Single(matcher.FindAll("  procedure Foo begin"));
			Assert.Empty(matcher.FindAll("x procedure Foo"));
		}

		[Fact]
		public void FindAll_Regex_SkipsEmptyMatches()
		{
			SearchMatcher matcher = Create("x*", regex: true);

			IReadOnlyList<LineMatch> matches = matcher.FindAll("abxxc");

			LineMatch single = Assert.Single(matches);
			Assert.Equal(2, single.Index);
			Assert.Equal(2, single.Length);
		}

		[Fact]
		public void FindAll_Regex_RespectsMatchCase()
		{
			Assert.Single(Create("foo", regex: true).FindAll("FOO"));
			Assert.Empty(Create("foo", matchCase: true, regex: true).FindAll("FOO"));
		}

		[Fact]
		public void WholeWord_IsIgnoredForRegularExpressions()
		{
			SearchMatcher matcher = Create("end", wholeWord: true, regex: true);

			Assert.Single(matcher.FindAll("endif"));
		}

		[Fact]
		public void FindNext_ReturnsLineAndColumnAndContinuesAfterTheStart()
		{
			SearchMatcher matcher = Create("ab");
			String text = "xab\r\nab ab\nzzz";

			TextMatch? first = matcher.FindNext(text, 0, false);
			TextMatch? second = matcher.FindNext(text, first!.Value.Offset + first.Value.Length, false);
			TextMatch? third = matcher.FindNext(text, second!.Value.Offset + second.Value.Length, false);
			TextMatch? none = matcher.FindNext(text, third!.Value.Offset + third.Value.Length, false);

			Assert.Equal(new TextMatch(1, 2, 1, 2), first);
			Assert.Equal(new TextMatch(5, 2, 2, 1), second);
			Assert.Equal(new TextMatch(8, 2, 2, 4), third);
			Assert.Null(none);
		}

		[Fact]
		public void FindNext_WrapsAroundWhenAsked()
		{
			SearchMatcher matcher = Create("ab");

			TextMatch? wrapped = matcher.FindNext("ab\r\ncd", 3, true);
			TextMatch? notWrapped = matcher.FindNext("ab\r\ncd", 3, false);

			Assert.Equal(new TextMatch(0, 2, 1, 1), wrapped);
			Assert.Null(notWrapped);
		}

		[Fact]
		public void FindNext_StartInTheMiddleOfAMatchDoesNotReturnIt()
		{
			SearchMatcher matcher = Create("abc");

			Assert.Null(matcher.FindNext("abc", 1, false));
		}

		[Fact]
		public void ComputeReplacements_ReplacesEveryMatchWithAscendingEdits()
		{
			SearchMatcher matcher = Create("foo");
			String text = "foo bar\r\nfoo foo\r\n";

			IReadOnlyList<TextEdit> edits = matcher.ComputeReplacements(text, "x");

			Assert.Equal(3, edits.Count);
			Assert.Equal(new[] { 0, 9, 13 }, edits.Select(edit => edit.Offset).ToArray());
			Assert.Equal("x bar\r\nx x\r\n", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ComputeReplacements_RegexExpandsGroups()
		{
			SearchMatcher matcher = Create("(\\w+)=(\\d+)", regex: true);

			IReadOnlyList<TextEdit> edits = matcher.ComputeReplacements("a=1 b=22", "$2:$1");

			Assert.Equal("1:a 22:b", TextEditApplier.Apply("a=1 b=22", edits));
		}

		[Fact]
		public void ComputeReplacements_PlainTextKeepsDollarSignsLiteral()
		{
			SearchMatcher matcher = Create("a");

			IReadOnlyList<TextEdit> edits = matcher.ComputeReplacements("a", "$1");

			Assert.Equal("$1", TextEditApplier.Apply("a", edits));
		}

		[Fact]
		public void CatastrophicRegex_ThrowsATimeoutExceptionInsteadOfHanging()
		{
			Boolean ok = SearchMatcher.TryCreate(new SearchOptions("(a+)+b", false, false, true), out SearchMatcher? matcher, out String? error, TimeSpan.FromMilliseconds(50));
			Assert.True(ok, error);
			String line = new String('a', 45) + "c";

			Assert.Throws<RegexMatchTimeoutException>(() => matcher!.FindFirst(line, 0));
			Assert.Throws<RegexMatchTimeoutException>(() => matcher!.FindAll(line));
			Assert.Throws<RegexMatchTimeoutException>(() => matcher!.FindNext(line, 0, true));
			Assert.Throws<RegexMatchTimeoutException>(() => matcher!.ComputeReplacements(line, "x"));
		}

		[Fact]
		public void ComputeReplacements_DoesNotMatchAcrossLines()
		{
			SearchMatcher matcher = Create("a\\r?\\nb", regex: true);

			Assert.Empty(matcher.ComputeReplacements("a\r\nb", "x"));
		}
	}
}
