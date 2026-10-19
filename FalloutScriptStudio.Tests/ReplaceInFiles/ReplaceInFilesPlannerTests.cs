namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;

	/// <summary>The first phase of Replace in Files: what would be replaced, without touching anything. Files live under _test_files.</summary>
	public sealed class ReplaceInFilesPlannerTests
	{
		private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

		internal static SearchMatcher Matcher(String text, Boolean matchCase = true, Boolean wholeWord = false, Boolean regex = false, TimeSpan? timeout = null)
		{
			Assert.True(SearchMatcher.TryCreate(new SearchOptions(text, matchCase, wholeWord, regex), out SearchMatcher? matcher, out String? error, timeout), error);
			return matcher!;
		}

		internal static ReplaceInFilesRequest FolderRequest(String folder, String find, String replacement, Boolean matchCase = true, Boolean wholeWord = false, Boolean regex = false, String masks = "*.ssl;*.h;*.msg", TimeSpan? timeout = null)
		{
			return new ReplaceInFilesRequest
			{
				Matcher = Matcher(find, matchCase, wholeWord, regex, timeout),
				Replacement = replacement,
				Folder = new SearchFolder(folder, SearchFileMasks.Parse(masks), true, new Dictionary<String, String>()),
				FallbackEncoding = TextEncodings.Ansi,
			};
		}

		private static ReplacePlan Plan(ReplaceInFilesRequest request)
		{
			return new ReplaceInFilesPlanner().Plan(request, null, CancellationToken.None);
		}

		[Fact]
		public void PlainText_FindsEveryOccurrence_WithLineColumnAndTheTextAroundIt()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "variable x = old;\r\n  call old + old;\r\nnothing\r\n");

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "new"));

			ReplaceFilePlan file = Assert.Single(plan.Files);
			Assert.Equal(path, file.Path);
			Assert.Equal(3, file.Matches.Count);
			ReplaceMatch first = file.Matches[0];
			Assert.Equal((1, 14), (first.Line, first.Column));
			Assert.Equal("variable x = ", first.Prefix);
			Assert.Equal(";", first.Suffix);
			Assert.Equal("variable x = old;", first.Before);
			Assert.Equal("variable x = new;", first.After);
			Assert.Equal((2, 8, 1), (file.Matches[1].Line, file.Matches[1].Column, file.Matches[1].Id));
			Assert.Equal((2, 14, 2), (file.Matches[2].Line, file.Matches[2].Column, file.Matches[2].Id));
			Assert.False(plan.Truncated);
			Assert.Empty(plan.Skipped);
		}

		[Fact]
		public void TheOffsetsAreOffsetsOfTheRawText_WhateverTheLineBreaks()
		{
			String text = "a old\r\nb old\nc old\rd old";
			IReadOnlyList<ReplaceMatch> matches = ReplaceInFilesPlanner.FindMatches(text, Matcher("old"), "NEW", 100, CancellationToken.None, out Boolean truncated);

			Assert.False(truncated);
			Assert.Equal(4, matches.Count);
			Assert.All(matches, match => Assert.Equal("old", text.Substring(match.Offset, match.Length)));
			Assert.Equal(new[] { 1, 2, 3, 4 }, matches.Select(match => match.Line).ToArray());
		}

		[Fact]
		public void WholeWord_DoesNotMatchInsideALongerWord()
		{
			IReadOnlyList<ReplaceMatch> matches = ReplaceInFilesPlanner.FindMatches("old older bold old_x old", Matcher("old", true, true), "n", 100, CancellationToken.None, out _);

			Assert.Equal(new[] { 0, 21 }, matches.Select(match => match.Offset).ToArray());
		}

		[Fact]
		public void CaseInsensitive_MatchesAnyCase_AndKeepsTheOriginalText()
		{
			IReadOnlyList<ReplaceMatch> matches = ReplaceInFilesPlanner.FindMatches("Old OLD old", Matcher("old", false), "n", 100, CancellationToken.None, out _);

			Assert.Equal(new[] { "Old", "OLD", "old" }, matches.Select(match => match.Matched).ToArray());
		}

		[Fact]
		public void Regex_ExpandsTheCaptureGroupsOfTheReplacement()
		{
			IReadOnlyList<ReplaceMatch> matches = ReplaceInFilesPlanner.FindMatches("set(speed, 10); set(power, 20);", Matcher(@"set\((\w+), (\d+)\)", true, false, true), "put($2 into $1)", 100, CancellationToken.None, out _);

			Assert.Equal(new[] { "put(10 into speed)", "put(20 into power)" }, matches.Select(match => match.Replacement).ToArray());
			Assert.Equal("set(speed, 10)", matches[0].Matched);
		}

		[Fact]
		public void Regex_NamedGroupsAndAnEmptyReplacementWork_ZeroLengthMatchesAreIgnored()
		{
			IReadOnlyList<ReplaceMatch> named = ReplaceInFilesPlanner.FindMatches("id=12", Matcher(@"id=(?<n>\d+)", true, false, true), "${n}", 10, CancellationToken.None, out _);
			IReadOnlyList<ReplaceMatch> removed = ReplaceInFilesPlanner.FindMatches("a1b2", Matcher(@"\d", true, false, true), String.Empty, 10, CancellationToken.None, out _);
			IReadOnlyList<ReplaceMatch> none = ReplaceInFilesPlanner.FindMatches("abc", Matcher(@"x*", true, false, true), "-", 10, CancellationToken.None, out _);

			Assert.Equal("12", named[0].Replacement);
			Assert.Equal(new[] { String.Empty, String.Empty }, removed.Select(match => match.Replacement).ToArray());
			Assert.Empty(none);
		}

		[Fact]
		public void ALongLineIsCutAroundTheMatch()
		{
			String line = new String('a', 300) + "old" + new String('b', 300);

			ReplaceMatch match = ReplaceInFilesPlanner.FindMatches(line, Matcher("old"), "n", 10, CancellationToken.None, out _).Single();

			Assert.True(match.Prefix.Length <= ReplaceInFilesPlanner.ContextLength + 1);
			Assert.StartsWith("\u2026", match.Prefix, StringComparison.Ordinal);
			Assert.EndsWith("\u2026", match.Suffix, StringComparison.Ordinal);
			Assert.Equal(301, match.Column);
		}

		[Theory]
		[InlineData("ansi")]
		[InlineData("utf8")]
		[InlineData("utf8bom")]
		[InlineData("utf16")]
		public void TheFilesAreReadInTheirOwnEncoding(String kind)
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String text = "// caf\u00e9\r\nold caf\u00e9\r\n";
			Encoding encoding = kind switch
			{
				"ansi" => TextEncodings.Ansi,
				"utf8" => new UTF8Encoding(false),
				"utf8bom" => Utf8WithBom,
				_ => Encoding.Unicode,
			};
			Byte[] preamble = encoding.GetPreamble();
			Byte[] body = encoding.GetBytes(text);
			File.WriteAllBytes(folder.Combine("enc.ssl"), preamble.Concat(body).ToArray());

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "new"));

			ReplaceMatch match = Assert.Single(Assert.Single(plan.Files).Matches);
			Assert.Equal("old caf\u00e9", match.Before);
			Assert.Equal(2, match.Line);
		}

		[Fact]
		public void TheHashIsTheSha256OfTheBytesThatWereRead()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old\r\n");

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "new"));

			Assert.Equal(SHA256.HashData(File.ReadAllBytes(path)), Assert.Single(plan.Files).Hash);
		}

		[Fact]
		public void AFileWithoutMatchesIsNotInThePlan_AndTheFilesAreInPathOrder()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			folder.Write("b.ssl", "old");
			folder.Write("a.ssl", "old");
			folder.Write("c.ssl", "nothing here");
			folder.Write("sub\\d.h", "old");
			folder.Write("e.txt", "old");

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "n"));

			Assert.Equal(new[] { "a.ssl", "b.ssl", "d.h" }, plan.Files.Select(file => Path.GetFileName(file.Path)).ToArray());
			Assert.Equal(4, plan.FilesSearched);
		}

		[Fact]
		public void TheMasksAndTheSubfoldersOptionAreHonoured()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			folder.Write("a.ssl", "old");
			folder.Write("sub\\b.ssl", "old");
			folder.Write("sub\\c.msg", "old");

			ReplaceInFilesRequest request = FolderRequest(folder.Path, "old", "n", masks: "*.ssl");
			ReplaceInFilesRequest flat = new ReplaceInFilesRequest
			{
				Matcher = request.Matcher,
				Replacement = "n",
				Folder = new SearchFolder(folder.Path, new[] { "*.ssl" }, false, new Dictionary<String, String>()),
			};

			Assert.Equal(2, Plan(request).Files.Count);
			Assert.Single(Plan(flat).Files);
		}

		[Fact]
		public void ABinaryFileIsIgnored()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			File.WriteAllBytes(folder.Combine("bin.ssl"), new Byte[] { 0x6F, 0x6C, 0x64, 0x00, 0x01, 0x6F, 0x6C, 0x64 });

			Assert.Empty(Plan(FolderRequest(folder.Path, "old", "n")).Files);
		}

		[Fact]
		public void AReadOnlyFileIsLeftOutAndReported()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("ro.ssl", "old old");
			folder.Write("rw.ssl", "old");
			File.SetAttributes(path, FileAttributes.ReadOnly);
			try
			{
				ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "n"));

				Assert.Equal("rw.ssl", Path.GetFileName(Assert.Single(plan.Files).Path));
				ReplaceSkip skip = Assert.Single(plan.Skipped);
				Assert.Equal((path, ReplaceSkipReason.ReadOnly, 2), (skip.Path, skip.Reason, skip.Matches));
			}
			finally
			{
				File.SetAttributes(path, FileAttributes.Normal);
			}
		}

		[Fact]
		public void ALockedFileIsLeftOutAndReported_TheOthersAreStillPlanned()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String locked = folder.Write("locked.ssl", "old");
			folder.Write("free.ssl", "old");
			using FileStream hold = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "n"));

			Assert.Equal("free.ssl", Path.GetFileName(Assert.Single(plan.Files).Path));
			ReplaceSkip skip = Assert.Single(plan.Skipped);
			Assert.Equal((locked, ReplaceSkipReason.Unreadable), (skip.Path, skip.Reason));
		}

		[Fact]
		public void AnOpenDocumentIsPlannedFromItsTextNotFromTheDisk()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old on disk");
			Object owner = new Object();
			ReplaceInFilesRequest request = new ReplaceInFilesRequest
			{
				Matcher = Matcher("old"),
				Replacement = "n",
				Folder = new SearchFolder(folder.Path, new[] { "*.ssl" }, true, new Dictionary<String, String>()),
				OpenDocuments = new Dictionary<String, ReplaceTextSource>(StringComparer.OrdinalIgnoreCase) { [path] = new ReplaceTextSource(path, "unsaved: old old", owner) },
			};

			ReplacePlan plan = Plan(request);

			ReplaceFilePlan file = Assert.Single(plan.Files);
			Assert.Same(owner, file.Owner);
			Assert.True(file.IsOpenDocument);
			Assert.Equal(2, file.Matches.Count);
			Assert.Equal(ReplaceInFilesPlanner.Hash("unsaved: old old"), file.Hash);
		}

		[Fact]
		public void SourcesInMemoryAreSearchedFirst_AMissingFileNeedsNoDisk()
		{
			Object owner = new Object();
			ReplaceInFilesRequest request = new ReplaceInFilesRequest
			{
				Matcher = Matcher("old"),
				Replacement = "n",
				Sources = new[] { new ReplaceTextSource("<never saved>", "old", owner), new ReplaceTextSource(@"C:\nowhere\x.ssl", "nothing", new Object()) },
			};

			ReplacePlan plan = Plan(request);

			Assert.Equal("<never saved>", Assert.Single(plan.Files).Path);
			Assert.Equal(2, plan.FilesSearched);
		}

		[Fact]
		public void TooManyMatchesStopTheListing_AndAreReported()
		{
			StringBuilder text = new StringBuilder();
			for (Int32 i = 0; i < 100000; i++)
			{
				text.Append("old\r\n");
			}

			ReplaceInFilesRequest request = new ReplaceInFilesRequest
			{
				Matcher = Matcher("old"),
				Replacement = "n",
				Sources = new[] { new ReplaceTextSource("a", text.ToString(), new Object()), new ReplaceTextSource("b", "old", new Object()) },
				MaxMatches = 50000,
			};
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

			ReplacePlan plan = Plan(request);

			Assert.True(plan.Truncated);
			Assert.Equal(50000, plan.MatchCount);
			Assert.Single(plan.Files);
			Assert.True(clock.ElapsedMilliseconds < 5000, "Planning took " + clock.ElapsedMilliseconds + " ms.");
		}

		[Fact]
		public void ExactlyTheLimitIsNotTruncated()
		{
			ReplaceInFilesRequest request = new ReplaceInFilesRequest
			{
				Matcher = Matcher("old"),
				Replacement = "n",
				Sources = new[] { new ReplaceTextSource("a", "old\r\nold\r\nold", new Object()) },
				MaxMatches = 3,
			};

			ReplacePlan plan = Plan(request);

			Assert.False(plan.Truncated);
			Assert.Equal(3, plan.MatchCount);
		}

		[Fact]
		public void ARunThatIsCancelledThrows_AndReportsProgress()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			for (Int32 i = 0; i < 20; i++)
			{
				folder.Write("f" + i + ".ssl", "old");
			}

			using CancellationTokenSource cancellation = new CancellationTokenSource();
			List<SearchProgress> reports = [];
			Int32 seen = 0;
			SyncProgress progress = new SyncProgress(report =>
			{
				reports.Add(report);
				if (++seen == 1)
				{
					cancellation.Cancel();
				}
			});

			Assert.Throws<OperationCanceledException>(() => new ReplaceInFilesPlanner().Plan(FolderRequest(folder.Path, "old", "n"), progress, cancellation.Token));
			Assert.NotEmpty(reports);
			Assert.Equal(20, reports[0].FilesTotal);
		}

		[Fact]
		public void ARegexThatBacktracksCatastrophically_TimesOutInsteadOfHanging()
		{
			String text = new String('a', 40) + "!";
			SearchMatcher matcher = Matcher(@"^(a+)+$", true, false, true, TimeSpan.FromMilliseconds(100));

			Assert.Throws<RegexMatchTimeoutException>(() => ReplaceInFilesPlanner.FindMatches(text, matcher, "x", 10, CancellationToken.None, out _));
		}

		[Fact]
		public void AHugeFileIsStreamedWithoutKeepingItsText()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String line = "some code old here and there\r\n";
			StringBuilder text = new StringBuilder();
			for (Int32 i = 0; i < 40000; i++)
			{
				text.Append(line);
			}

			folder.Write("big.ssl", text.ToString());
			GC.Collect();
			Int64 before = GC.GetTotalMemory(true);

			ReplacePlan plan = Plan(FolderRequest(folder.Path, "old", "new"));

			GC.Collect();
			Int64 after = GC.GetTotalMemory(true);
			Assert.Equal(40000, plan.MatchCount);
			Assert.True(after - before < 60L * 1024 * 1024, "The plan keeps " + ((after - before) / 1024 / 1024) + " MB.");
		}

		private sealed class SyncProgress : IProgress<SearchProgress>
		{
			private readonly Action<SearchProgress> _handler;

			public SyncProgress(Action<SearchProgress> handler)
			{
				this._handler = handler;
			}

			public void Report(SearchProgress value)
			{
				this._handler(value);
			}
		}
	}
}