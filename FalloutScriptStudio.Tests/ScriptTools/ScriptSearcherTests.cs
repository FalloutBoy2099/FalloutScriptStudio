namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using Xunit;

	public sealed class ScriptSearcherTests
	{
		private readonly ScriptSearcher _searcher = new ScriptSearcher();

		private static SearchMatcher Matcher(String text, Boolean matchCase = false, Boolean wholeWord = false, Boolean regex = false)
		{
			Assert.True(SearchMatcher.TryCreate(new SearchOptions(text, matchCase, wholeWord, regex), out SearchMatcher? matcher, out String? error), error);
			return matcher!;
		}

		private static SearchFolder Folder(String directory, String masks = SearchFileMasks.AllScriptFiles, Boolean subfolders = true, IReadOnlyDictionary<String, String>? openTexts = null)
		{
			return new SearchFolder(directory, SearchFileMasks.Parse(masks), subfolders, openTexts ?? new Dictionary<String, String>());
		}

		[Fact]
		public void Search_Sources_ReportsOneHitPerLineWithTheFirstMatch()
		{
			SearchRequest request = new SearchRequest
			{
				Matcher = Matcher("foo"),
				Sources = new[] { new SearchTextSource("a.ssl", "  foo foo\r\nbar\r\nxFOO\r\n") },
			};

			SearchResult result = this._searcher.Search(request, null, CancellationToken.None);

			Assert.Equal(2, result.Hits.Count);
			Assert.Equal(new SearchHit("a.ssl", 1, 3, 3, "  foo foo"), result.Hits[0]);
			Assert.Equal(3, result.Hits[1].Line);
			Assert.Equal(2, result.Hits[1].Column);
			Assert.Equal(1, result.FilesSearched);
			Assert.Equal(1, result.FilesWithMatches);
		}

		[Fact]
		public void Search_Hits_ConvertToSearchBuildErrors()
		{
			SearchHit hit = new SearchHit("C:\\a.ssl", 7, 5, 3, "\t  call Foo;  ");

			BuildError error = hit.ToBuildError();

			Assert.Equal(BuildErrorType.Search, error.Type);
			Assert.Equal("call Foo;", error.Message);
			Assert.Equal("C:\\a.ssl", error.FileName);
			Assert.Equal(7, error.Line);
			Assert.Equal(5, error.Column);
			Assert.Equal(3, error.Length);
		}

		[Fact]
		public void Search_VeryLongLines_AreShortenedInTheMessage()
		{
			SearchHit hit = new SearchHit("a", 1, 1, 1, new String('x', 2000));

			Assert.Equal(SearchHit.MaxMessageLength + 3, hit.Message.Length);
		}

		[Fact]
		public void Search_Folder_FindsMatchesInMaskedFilesAndSubfolders()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String a = temp.Write("a.ssl", "procedure Foo begin\r\nend\r\n");
			String b = temp.Write("sub/b.h", "#define FOO 1\r\n");
			temp.Write("c.txt", "foo in a text file\r\n");
			temp.Write("sub/d.msg", "{100}{}{no match}\r\n");
			SearchRequest request = new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path) };

			SearchResult result = this._searcher.Search(request, null, CancellationToken.None);

			Assert.Equal(new[] { a, b }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase), result.Hits.Select(hit => hit.FilePath));
			Assert.Equal(3, result.FilesSearched);
			Assert.Equal(2, result.FilesWithMatches);
		}

		[Fact]
		public void Search_Folder_CanSkipSubfoldersAndUseCustomMasks()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String top = temp.Write("a.ssl", "foo\r\n");
			temp.Write("sub/b.ssl", "foo\r\n");
			String text = temp.Write("c.txt", "foo\r\n");

			SearchResult flat = this._searcher.Search(new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path, "*.ssl", false) }, null, CancellationToken.None);
			SearchResult texts = this._searcher.Search(new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path, "*.txt;*.ssl", false) }, null, CancellationToken.None);

			Assert.Equal(top, Assert.Single(flat.Hits).FilePath);
			Assert.Equal(new[] { top, text }, texts.Hits.Select(hit => hit.FilePath).ToArray());
		}

		[Fact]
		public void Search_Folder_UsesTheOpenTextInsteadOfTheFileOnDisk()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String saved = temp.Write("a.ssl", "old text\r\n");
			Dictionary<String, String> open = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase) { [saved.ToUpperInvariant()] = "edited needle\r\n" };

			SearchResult edited = this._searcher.Search(new SearchRequest { Matcher = Matcher("needle"), Folder = Folder(temp.Path, openTexts: open) }, null, CancellationToken.None);
			SearchResult diskOnly = this._searcher.Search(new SearchRequest { Matcher = Matcher("needle"), Folder = Folder(temp.Path) }, null, CancellationToken.None);
			SearchResult oldText = this._searcher.Search(new SearchRequest { Matcher = Matcher("old"), Folder = Folder(temp.Path, openTexts: open) }, null, CancellationToken.None);

			Assert.Equal("edited needle", Assert.Single(edited.Hits).LineText);
			Assert.Empty(diskOnly.Hits);
			Assert.Empty(oldText.Hits);
		}

		[Fact]
		public void Search_Folder_SkipsBinaryFilesAndDecodesAnsiFiles()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String accent = TextEncodings.Ansi.GetString(new Byte[] { 0xE9 });
			temp.Write("ansi.ssl", "caf" + accent + " needle\r\n", TextEncodings.Ansi);
			File.WriteAllBytes(temp.Combine("binary.ssl"), new Byte[] { 0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0x00, 0x01 });
			SearchRequest request = new SearchRequest { Matcher = Matcher("needle"), Folder = Folder(temp.Path) };

			SearchResult result = this._searcher.Search(request, null, CancellationToken.None);

			SearchHit hit = Assert.Single(result.Hits);
			Assert.Equal("caf" + accent + " needle", hit.LineText);
			Assert.Equal(1, result.FilesSkipped);
		}

		[Fact]
		public void Search_Folder_UnicodeFilesWithAByteOrderMarkAreDecoded()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			Byte[] body = Encoding.Unicode.GetBytes("wide needle\r\n");
			File.WriteAllBytes(temp.Combine("wide.ssl"), Encoding.Unicode.GetPreamble().Concat(body).ToArray());

			SearchResult result = this._searcher.Search(new SearchRequest { Matcher = Matcher("needle"), Folder = Folder(temp.Path) }, null, CancellationToken.None);

			Assert.Equal("wide needle", Assert.Single(result.Hits).LineText);
		}

		[Fact]
		public void Search_MissingFolder_FindsNothing()
		{
			SearchResult result = this._searcher.Search(new SearchRequest { Matcher = Matcher("x"), Folder = Folder("Z:\\definitely\\not\\here") }, null, CancellationToken.None);

			Assert.Empty(result.Hits);
			Assert.Equal(0, result.FilesSearched);
		}

		[Fact]
		public void Search_StopsAtTheHitLimitAndSaysSo()
		{
			String text = String.Join("\r\n", Enumerable.Repeat("foo", 50));
			SearchRequest request = new SearchRequest
			{
				Matcher = Matcher("foo"),
				Sources = new[] { new SearchTextSource("a", text), new SearchTextSource("b", text) },
				MaxHits = 60,
			};

			SearchResult result = this._searcher.Search(request, null, CancellationToken.None);

			Assert.Equal(60, result.Hits.Count);
			Assert.True(result.Truncated);
		}

		[Fact]
		public void Search_ExactlyAtTheLimit_IsNotTruncated()
		{
			SearchRequest request = new SearchRequest
			{
				Matcher = Matcher("foo"),
				Sources = new[] { new SearchTextSource("a", "foo\r\nfoo\r\n") },
				MaxHits = 2,
			};

			Assert.False(this._searcher.Search(request, null, CancellationToken.None).Truncated);
		}

		[Fact]
		public void Search_ReportsProgressFromZeroToTheTotal()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			for (Int32 i = 0; i < 5; i++)
			{
				temp.Write("f" + i + ".ssl", "foo\r\n");
			}

			List<SearchProgress> reports = new List<SearchProgress>();
			ImmediateProgress progress = new ImmediateProgress(reports);

			this._searcher.Search(new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path) }, progress, CancellationToken.None);

			Assert.Equal(new SearchProgress(0, 5, 0), reports[0]);
			Assert.Equal(new SearchProgress(5, 5, 5), reports[^1]);
			Assert.True(reports.Zip(reports.Skip(1)).All(pair => pair.Second.FilesProcessed >= pair.First.FilesProcessed));
		}

		[Fact]
		public async Task SearchAsync_CancelledToken_Throws()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			temp.Write("a.ssl", "foo\r\n");
			using CancellationTokenSource source = new CancellationTokenSource();
			await source.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this._searcher.SearchAsync(new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path) }, null, source.Token));
		}

		[Fact]
		public async Task SearchAsync_CancelledWhileRunning_Throws()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			for (Int32 i = 0; i < 200; i++)
			{
				temp.Write("f" + i + ".ssl", "foo\r\n");
			}

			using CancellationTokenSource source = new CancellationTokenSource();
			CancelingProgress progress = new CancelingProgress(source);

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this._searcher.SearchAsync(new SearchRequest { Matcher = Matcher("foo"), Folder = Folder(temp.Path) }, progress, source.Token));
		}

		[Fact]
		public async Task SearchAsync_FindsTheSameAsSearch()
		{
			SearchRequest request = new SearchRequest { Matcher = Matcher("foo"), Sources = new[] { new SearchTextSource("a", "foo\r\nbar\r\nfoo") } };

			SearchResult result = await this._searcher.SearchAsync(request, null, CancellationToken.None);

			Assert.Equal(new[] { 1, 3 }, result.Hits.Select(hit => hit.Line).ToArray());
		}

		[Theory]
		[InlineData(null, "*.ssl;*.h;*.msg")]
		[InlineData("", "*.ssl;*.h;*.msg")]
		[InlineData("All", "*.ssl;*.h;*.msg")]
		[InlineData("*.ssl", "*.ssl")]
		[InlineData("*.ssl, *.SSL ;*.txt", "*.ssl;*.txt")]
		[InlineData(" ; ", "*.ssl;*.h;*.msg")]
		[InlineData("all;*.ssl", "*.ssl;*.h;*.msg")]
		public void FileMasks_Parse_Normalizes(String? text, String expected)
		{
			Assert.Equal(expected, String.Join(";", SearchFileMasks.Parse(text)));
		}

		private sealed class ImmediateProgress : IProgress<SearchProgress>
		{
			private readonly List<SearchProgress> _reports;

			public ImmediateProgress(List<SearchProgress> reports)
			{
				this._reports = reports;
			}

			public void Report(SearchProgress value)
			{
				this._reports.Add(value);
			}
		}

		private sealed class CancelingProgress : IProgress<SearchProgress>
		{
			private readonly CancellationTokenSource _source;

			public CancelingProgress(CancellationTokenSource source)
			{
				this._source = source;
			}

			public void Report(SearchProgress value)
			{
				if (value.FilesProcessed >= 0)
				{
					this._source.Cancel();
				}
			}
		}
	}
}
