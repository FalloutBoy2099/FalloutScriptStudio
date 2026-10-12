namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Tests.Functions;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class EditingHelpersTests : IDisposable
	{
		private readonly AutoPairHandler _pairs = new AutoPairHandler();
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		[Theory]
		[InlineData("display_msg(", "\"", true)]
		[InlineData("x := ", "\"", true)]
		[InlineData("x := foo", "\"", false)]
		[InlineData("x := (", "\"", true)]
		public void Quote_AtTheEndOfTheLine_IsClosedWhenItStartsAString(String before, String typed, Boolean closed)
		{
			AutoPairAction action = this._pairs.Handle(before, before.Length, typed[0], false);

			Assert.Equal(closed ? AutoPairActionKind.InsertClosing : AutoPairActionKind.None, action.Kind);
			Assert.Equal(closed ? "\"" : null, action.Text);
		}

		[Fact]
		public void Quote_BetweenParentheses_IsClosed()
		{
			AutoPairAction action = this._pairs.Handle("foo()", 4, '"', false);

			Assert.Equal(AutoPairActionKind.InsertClosing, action.Kind);
		}

		[Fact]
		public void Quote_TypedBeforeTheAutomaticClosingQuote_Overtypes()
		{
			AutoPairAction action = this._pairs.Handle("foo(\"\")", 5, '"', false);

			Assert.Equal(AutoPairActionKind.RemoveNext, action.Kind);
		}

		[Fact]
		public void Quote_InsideAString_IsLeftAlone()
		{
			Assert.Equal(AutoPairActionKind.None, this._pairs.Handle("x := \"ab cd\"", 8, '"', false).Kind);
		}

		[Theory]
		[InlineData("foo", '(', ")")]
		[InlineData("foo ", '(', ")")]
		[InlineData("foo", '[', "]")]
		public void OpeningBracket_BeforeWhiteSpaceOrTheEnd_GetsAClosingBracket(String before, Char typed, String closing)
		{
			AutoPairAction action = this._pairs.Handle(before + " bar", before.Length, typed, false);

			Assert.Equal((AutoPairActionKind.InsertClosing, closing, true), (action.Kind, action.Text, action.ArmOvertype));
		}

		[Fact]
		public void OpeningBracket_BeforeAnIdentifier_IsLeftAlone()
		{
			Assert.Equal(AutoPairActionKind.None, this._pairs.Handle("(abc", 0, '(', false).Kind);
		}

		[Fact]
		public void ClosingBracket_RightAfterAnAutomaticOne_Overtypes()
		{
			Assert.Equal(AutoPairActionKind.RemoveNext, this._pairs.Handle("foo()", 4, ')', true).Kind);
			Assert.Equal(AutoPairActionKind.RemoveNext, this._pairs.Handle("a[]", 2, ']', true).Kind);
			Assert.Equal(AutoPairActionKind.None, this._pairs.Handle("foo()", 4, ')', false).Kind);
			Assert.Equal(AutoPairActionKind.None, this._pairs.Handle("foo(x)", 5, ')', true).Kind);
		}

		[Theory]
		[InlineData('a')]
		[InlineData('{')]
		[InlineData('}')]
		[InlineData(';')]
		public void OtherCharacters_DoNothing(Char typed)
		{
			Assert.Same(AutoPairAction.None, this._pairs.Handle("x ", 2, typed, true));
		}

		[Fact]
		public void FindOccurrences_OfAWord_ReturnsWholeWordsOnly()
		{
			String text = "counter := counter + my_counter; counters := 1; Counter := 2;";

			var spans = SelectionHelper.FindOccurrences(text, new TextSpan(0, 7));

			Assert.Equal(new[] { new TextSpan(0, 7), new TextSpan(11, 7) }, spans.ToArray());
		}

		[Fact]
		public void FindOccurrences_OfAFragment_ReturnsEveryOccurrence()
		{
			String text = "counter my_counter";

			var spans = SelectionHelper.FindOccurrences(text, new TextSpan(0, 5));

			Assert.Equal(new[] { new TextSpan(0, 5), new TextSpan(11, 5) }, spans.ToArray());
		}

		[Fact]
		public void FindOccurrences_IgnoresEmptyWhiteSpaceAndSymbolPairs()
		{
			Assert.Empty(SelectionHelper.FindOccurrences("ab ab", new TextSpan(2, 0)));
			Assert.Empty(SelectionHelper.FindOccurrences("   ", new TextSpan(0, 3)));
			Assert.Empty(SelectionHelper.FindOccurrences("a ;; b ;;", new TextSpan(2, 2)));
			Assert.Equal(2, SelectionHelper.FindOccurrences("a ( b (", new TextSpan(2, 1)).Count);
		}

		[Theory]
		[InlineData("foo({condition}) then", 5, 4, 11)]
		[InlineData("foo({condition}) then", 4, 4, 11)]
		[InlineData("foo({condition}) then", 14, 4, 11)]
		public void FindPlaceholder_AroundTheCaret(String text, Int32 caret, Int32 start, Int32 length)
		{
			TextSpan? span = SelectionHelper.FindPlaceholder(text, caret);

			Assert.Equal(new TextSpan(start, length), span);
		}

		[Theory]
		[InlineData("foo({condition}) then", 15)]
		[InlineData("foo({condition}) then", 16)]
		[InlineData("foo({condition}) then", 1)]
		[InlineData("x := {1, 2, 3};", 8)]
		[InlineData("x := {a: 1};", 8)]
		[InlineData("x := {};", 6)]
		[InlineData("x := {abc", 7)]
		public void FindPlaceholder_Rejects(String text, Int32 caret)
		{
			Assert.Null(SelectionHelper.FindPlaceholder(text, caret));
		}

		[Fact]
		public void FindPlaceholder_DoesNotCrossLines()
		{
			Assert.Null(SelectionHelper.FindPlaceholder("x := {abc\r\ndef}", 8));
		}

		[Fact]
		public void ProjectFileFinder_ListsMatchingFilesRecursively()
		{
			this._folder.Write("a.ssl", "x");
			this._folder.Write("sub\\b.SSL", "x");
			this._folder.Write("sub\\deep\\c.h", "x");
			this._folder.Write("sub\\d.txt", "x");

			var all = ProjectFileFinder.EnumerateScriptFiles(this._folder.Path).Select(f => Path.GetFileName(f).ToLowerInvariant()).OrderBy(n => n).ToArray();
			var top = ProjectFileFinder.EnumerateFiles(this._folder.Path, new[] { "*.ssl;*.txt", "*.ssl" }, false).Select(Path.GetFileName).ToArray();

			Assert.Equal(new[] { "a.ssl", "b.ssl", "c.h" }, all);
			Assert.Equal(new[] { "a.ssl" }, top);
			Assert.Empty(ProjectFileFinder.EnumerateFiles(this._folder.Combine("missing"), new[] { "*.*" }, true));
		}

		[Fact]
		public async Task FileSearcher_FindsFilesAndRows()
		{
			String a = this._folder.Write("a.ssl", "procedure start begin\r\n   call Node001;\r\nend");
			String b = this._folder.Write("b.ssl", "// nothing\r\ncall node001; call NODE001;\r\n");
			String missing = this._folder.Combine("missing.ssl");
			var searcher = new FileSearcher(new TextSearcher());
			var progress = new CountingProgress();

			var files = await searcher.FindFilesAsync(new[] { a, b, missing }, new SearchQuery("node001"), null, progress, CancellationToken.None);
			var rows = await searcher.FindRowsAsync(new[] { a, b }, new SearchQuery("node001") { WholeWord = true }, null, null, CancellationToken.None);

			Assert.Equal(new[] { a, b }, files.ToArray());
			Assert.Equal(3, progress.Last);
			Assert.Equal(new[] { (a, 2), (b, 2) }, rows.Select(r => (r.File, r.Line)).ToArray());
			Assert.Equal(9, rows[0].Column);
			Assert.Equal("call Node001;", rows[0].LineText);
		}

		[Fact]
		public async Task FileSearcher_Cancellation_Throws()
		{
			String a = this._folder.Write("a.ssl", "x");
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileSearcher(new TextSearcher()).FindFilesAsync(new[] { a }, new SearchQuery("x"), Encoding.UTF8, null, cts.Token));
		}

		private sealed class CountingProgress : IProgress<Int32>
		{
			public Int32 Last { get; private set; }

			public void Report(Int32 value)
			{
				this.Last = value;
			}
		}
	}
}
