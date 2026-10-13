namespace FalloutScriptStudio.Core.Tests.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class ProjectSymbolIndexTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("symbol-index");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private static ProjectIndexRoots Roots(String folder, Boolean recursive = true)
		{
			return new ProjectIndexRoots { Folders = new[] { new ProjectIndexRoot(folder, recursive) } };
		}

		/// <summary>Writes a file and gives it an old time stamp, as a file that was saved some time ago (a file written less than 2 seconds before it is read is read again).</summary>
		private String Write(String relativePath, String content, Int32 hoursAgo = 3)
		{
			String path = Path.GetFullPath(this._folder.Write(relativePath, content));
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursAgo));
			return path;
		}

		private static ProjectSymbolIndex Create(Func<ProjectIndexOptions>? options = null)
		{
			return new ProjectSymbolIndex(options);
		}

		private static String[] Names(IEnumerable<IndexedSymbol> symbols)
		{
			return symbols.Select(s => s.Name).ToArray();
		}

		private const String Script = "#include \"define.h\"\r\n" +
			"#define LIMIT 5\r\n" +
			"#define MAX(a, b) (((a) > (b)) ? (a) : (b))\r\n" +
			"variable counter, total := 0;\r\n" +
			"import variable remote;\r\n" +
			"export variable shared;\r\n" +
			"procedure start;\r\n" +
			"import procedure api;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"\tvariable local1;\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure Helper(variable a) begin\r\n" +
			"end\r\n";

		[Fact]
		public async Task UpdateAsync_IndexesProceduresMacrosAndScriptVariables()
		{
			String path = this.Write("a.ssl", Script);
			using ProjectSymbolIndex index = Create();

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(1, index.FileCount);
			IReadOnlyList<IndexedSymbol> procedures = index.Search(null, 100, IndexedSymbolKind.Procedure);
			Assert.Equal(new[] { "api", "Helper", "start" }, Names(procedures).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray());
			IndexedSymbol start = index.Find("START", IndexedSymbolKind.Procedure).Single();
			Assert.Equal(10, start.Line);
			Assert.Equal(7, start.DeclarationLine);
			Assert.True(start.HasBody);
			Assert.Equal(path, start.File);
			IndexedSymbol api = index.Find("api").Single();
			Assert.False(api.HasBody);
			Assert.True((api.Flags & ProcedureFlags.Import) != 0);

			IndexedSymbol max = index.Find("MAX").Single();
			Assert.Equal(IndexedSymbolKind.Macro, max.Kind);
			Assert.Equal(3, max.Line);
			Assert.Equal("MAX(a, b)", max.Detail);
			Assert.Equal("MAX(a, b)", max.DisplayName);
			Assert.Null(index.Find("LIMIT").Single().Detail);

			Assert.Equal(new[] { "counter", "remote", "shared", "total" }, Names(index.Search(null, 100, IndexedSymbolKind.Variable)).ToArray());
			Assert.Equal(VariableScope.Import, index.Find("remote").Single().Scope);
			Assert.Equal(VariableScope.Export, index.Find("shared").Single().Scope);
			Assert.Equal(VariableScope.Global, index.Find("counter").Single().Scope);
			Assert.Empty(index.Find("local1"));
			Assert.Equal(index.SymbolCount, index.Search(null, 1000).Count);
		}

		[Fact]
		public async Task Find_MacrosAreCaseSensitive_OtherSymbolsAreNot()
		{
			this.Write("a.ssl", "#define Limit 1\r\nprocedure Foo begin\r\nend\r\nvariable Bar;\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Single(index.Find("Limit"));
			Assert.Empty(index.Find("LIMIT"));
			Assert.Single(index.Find("foo"));
			Assert.Single(index.Find("BAR"));
			Assert.Empty(index.Find("foo", IndexedSymbolKind.Macro | IndexedSymbolKind.Variable));
		}

		[Fact]
		public async Task Find_ReturnsEveryDefinitionOfAName()
		{
			this.Write("a/one.ssl", "procedure shared begin\r\nend\r\n#define SAME 1\r\n");
			this.Write("b/two.ssl", "procedure shared begin\r\nend\r\n");
			this.Write("c/three.h", "#define SAME 2\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(2, index.Find("shared").Count);
			Assert.Equal(2, index.Find("SAME").Count);
		}

		[Fact]
		public async Task UpdateAsync_OnlyScriptsAndHeadersAreIndexed_HiddenFoldersAreSkipped()
		{
			this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			this.Write("b.h", "#define TWO 2\r\n");
			this.Write("c.int", "procedure three begin\r\nend\r\n");
			this.Write("d.msg", "procedure four begin\r\nend\r\n");
			this.Write("e.txt", "procedure five begin\r\nend\r\n");
			this.Write(".git/f.ssl", "procedure six begin\r\nend\r\n");
			this.Write("sub/g.SSL", "procedure seven begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(new[] { "one", "seven", "TWO" }, Names(index.Search(null, 100)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray());
			Assert.Equal(3, index.FileCount);
		}

		[Fact]
		public async Task UpdateAsync_BinaryFilesAndHugeFilesAreSkipped()
		{
			String binary = this.Write("binary.ssl", "procedure hidden begin\r\nend\r\n");
			File.WriteAllBytes(binary, new Byte[] { 0x70, 0x00, 0x72, 0x00, 0x0A });
			this.Write("big.ssl", "procedure large begin\r\nend\r\n" + new String(' ', 5000));
			this.Write("small.ssl", "procedure fine begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create(() => new ProjectIndexOptions { MaxFileBytes = 4000 });

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(new[] { "fine" }, Names(index.Search(null, 100)).ToArray());
			Assert.Equal(3, index.FileCount);
		}

		[Fact]
		public async Task UpdateAsync_ReadsFilesWithTheConfiguredCodePage()
		{
			Encoding oem = TextEncodings.Oem866;
			String path = this._folder.Combine("cyrillic.ssl");
			File.WriteAllBytes(path, oem.GetBytes("#define привет 1\r\nprocedure start begin\r\nend\r\n"));
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
			using ProjectSymbolIndex index = Create(() => new ProjectIndexOptions { Encoding = oem });

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Single(index.Find("привет"));
		}

		[Fact]
		public async Task UpdateAsync_Utf8AndBomFilesAreDecodedWhateverTheCodePage()
		{
			String plain = this._folder.Combine("utf8.ssl");
			File.WriteAllBytes(plain, new UTF8Encoding(false).GetBytes("#define café 1\r\n"));
			File.SetLastWriteTimeUtc(plain, DateTime.UtcNow.AddHours(-2));
			String bom = this._folder.Combine("bom.ssl");
			File.WriteAllBytes(bom, new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes("#define naïve 1\r\n")).ToArray());
			File.SetLastWriteTimeUtc(bom, DateTime.UtcNow.AddHours(-2));
			using ProjectSymbolIndex index = Create(() => new ProjectIndexOptions { Encoding = TextEncodings.Oem866 });

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Single(index.Find("café"));
			Assert.Single(index.Find("naïve"));
		}

		[Fact]
		public async Task UpdateAsync_AFolderThatIsNotRecursiveDoesNotIncludeItsSubfolders()
		{
			this.Write("top.ssl", "procedure top begin\r\nend\r\n");
			this.Write("sub/deep.ssl", "procedure deep begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();

			await index.UpdateAsync(Roots(this._folder.Path, false));

			Assert.Equal(new[] { "top" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateAsync_ExtraFilesAreIndexed_AndAFileListedTwiceOnce()
		{
			String outside = this.Write("outside/a.ssl", "procedure outside_proc begin\r\nend\r\n");
			this.Write("inside/b.ssl", "procedure inside_proc begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			ProjectIndexRoots roots = new ProjectIndexRoots
			{
				Folders = new[] { new ProjectIndexRoot(Path.Combine(this._folder.Path, "inside"), true), new ProjectIndexRoot(Path.Combine(this._folder.Path, "missing"), true) },
				Files = new[] { outside, outside, this._folder.Combine("not-there.ssl"), this._folder.Combine("note.txt") },
			};

			await index.UpdateAsync(roots);

			Assert.Equal(2, index.FileCount);
			Assert.Equal(new[] { "inside_proc", "outside_proc" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateAsync_OnlyChangedFilesAreReadAgain()
		{
			this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			String b = this.Write("b.ssl", "procedure two begin\r\nend\r\n");
			this.Write("c.ssl", "procedure three begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			List<ProjectIndexProgress> progress = new List<ProjectIndexProgress>();
			index.ProgressChanged += (_, p) => progress.Add(p);
			await index.UpdateAsync(Roots(this._folder.Path));
			Assert.Equal(3, progress.Where(p => p.Phase == ProjectIndexPhase.Indexing).Max(p => p.Total));

			progress.Clear();
			Int32 updates = 0;
			index.Updated += (_, _) => updates++;
			await index.UpdateAsync(Roots(this._folder.Path));
			Assert.Equal(0, updates);
			Assert.DoesNotContain(progress, p => p.Phase == ProjectIndexPhase.Indexing);

			File.WriteAllText(b, "procedure two_renamed begin\r\nend\r\nprocedure extra begin\r\nend\r\n");
			File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddHours(-1));
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(1, updates);
			Assert.Equal(1, progress.Where(p => p.Phase == ProjectIndexPhase.Indexing).Max(p => p.Total));
			Assert.Equal(new[] { "extra", "one", "three", "two_renamed" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateAsync_AFileThatChangedLessThanTwoSecondsBeforeBeingReadIsReadAgain()
		{
			String path = this._folder.Write("a.ssl", "procedure one begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			Int32 updates = 0;
			index.Updated += (_, _) => updates++;

			File.WriteAllText(path, "procedure two begin\r\nend\r\n");
			File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path));
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(1, updates);
			Assert.Equal(new[] { "two" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateAsync_FilesThatDisappearAreDropped_AndNewFilesAreAdded()
		{
			String a = this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			this.Write("b.ssl", "procedure two begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			File.Delete(a);
			this.Write("c.ssl", "procedure three begin\r\nend\r\n");
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(new[] { "three", "two" }, Names(index.Search(null, 100)).ToArray());
			Assert.Equal(2, index.FileCount);
		}

		[Fact]
		public async Task UpdateAsync_ChangingTheRootsDropsTheFilesOutsideThem()
		{
			this.Write("a/one.ssl", "procedure one begin\r\nend\r\n");
			this.Write("b/two.ssl", "procedure two begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			Assert.Equal(2, index.FileCount);

			await index.UpdateAsync(Roots(Path.Combine(this._folder.Path, "b")));

			Assert.Equal(new[] { "two" }, Names(index.Search(null, 100)).ToArray());
			await index.UpdateAsync(ProjectIndexRoots.None);
			Assert.Equal(0, index.FileCount);
		}

		[Fact]
		public async Task UpdateAsync_AUnreadableFileDoesNotStopTheRun_AndIsReadAgainLater()
		{
			this.Write("good.ssl", "procedure good begin\r\nend\r\n");
			String locked = this.Write("locked.ssl", "procedure later begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				await index.UpdateAsync(Roots(this._folder.Path));
				Assert.Equal(new[] { "good" }, Names(index.Search(null, 100)).ToArray());
			}

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(new[] { "good", "later" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateAsync_ACancelledRunKeepsThePreviousSnapshot()
		{
			this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			this.Write("b.ssl", "procedure two begin\r\nend\r\n");
			using CancellationTokenSource cancel = new CancellationTokenSource();
			cancel.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.UpdateAsync(Roots(this._folder.Path), cancel.Token));

			Assert.Equal(new[] { "one" }, Names(index.Search(null, 100)).ToArray());
			Assert.Equal(ProjectIndexPhase.Idle, index.Progress.Phase);
		}

		[Fact]
		public async Task UpdateAsync_CancelledWhileRunningStopsAndKeepsTheOldSnapshot()
		{
			for (Int32 i = 0; i < 400; i++)
			{
				this.Write("many/file_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure p_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n");
			}

			using ProjectSymbolIndex index = Create();
			using CancellationTokenSource cancel = new CancellationTokenSource();
			index.ProgressChanged += (_, p) =>
			{
				if (p.Phase == ProjectIndexPhase.Indexing)
				{
					cancel.Cancel();
				}
			};

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.UpdateAsync(Roots(this._folder.Path), cancel.Token));

			Assert.Equal(0, index.FileCount);
			Assert.False(index.Progress.IsBusy);
		}

		[Fact]
		public async Task UpdateAsync_ReportsProgressFromScanningToTheEnd()
		{
			for (Int32 i = 0; i < 50; i++)
			{
				this.Write("f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n");
			}

			using ProjectSymbolIndex index = Create();
			List<ProjectIndexProgress> progress = new List<ProjectIndexProgress>();
			index.ProgressChanged += (_, p) =>
			{
				lock (progress)
				{
					progress.Add(p);
				}
			};

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(ProjectIndexPhase.Scanning, progress[0].Phase);
			Assert.Contains(progress, p => p.Phase == ProjectIndexPhase.Indexing && p.Done == 50 && p.Total == 50);
			Assert.Equal(ProjectIndexPhase.Idle, progress[^1].Phase);
			Assert.Equal(50, progress[^1].Symbols);
		}

		[Fact]
		public async Task UpdateAsync_RunsOneAfterTheOther()
		{
			for (Int32 i = 0; i < 30; i++)
			{
				this.Write("f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n");
			}

			using ProjectSymbolIndex index = Create();

			await Task.WhenAll(index.UpdateAsync(Roots(this._folder.Path)), index.UpdateAsync(Roots(this._folder.Path)), index.UpdateAsync(Roots(this._folder.Path)));

			Assert.Equal(30, index.SymbolCount);
			Assert.Equal(30, index.FileCount);
		}

		[Fact]
		public async Task UpdateAsync_StopsAddingSymbolsAtTheLimit()
		{
			for (Int32 i = 0; i < 20; i++)
			{
				this.Write("f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl", "procedure a" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\nprocedure b" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n");
			}

			using ProjectSymbolIndex index = Create(() => new ProjectIndexOptions { MaxSymbols = 15 });

			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.True(index.IsTruncated);
			Assert.InRange(index.SymbolCount, 1, 15);
		}

		[Fact]
		public async Task UpdateFileAsync_ReplacesTheSymbolsOfOneFile()
		{
			String path = this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			this.Write("b.ssl", "procedure two begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			Int32 updates = 0;
			index.Updated += (_, _) => updates++;

			File.WriteAllText(path, "procedure one_changed begin\r\nend\r\n");
			await index.UpdateFileAsync(path);

			Assert.Equal(1, updates);
			Assert.Equal(new[] { "one_changed", "two" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateFileAsync_UsesTheGivenTextWithoutReadingTheFile()
		{
			String path = this.Write("a.ssl", "procedure on_disk begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			await index.UpdateFileAsync(path, "procedure from_editor begin\r\nend\r\n");

			Assert.Equal(new[] { "from_editor" }, Names(index.Search(null, 100)).ToArray());
		}

		[Fact]
		public async Task UpdateFileAsync_AddsAnUnknownFile_AndRemovesADeletedOne()
		{
			using ProjectSymbolIndex index = Create();
			String added = this.Write("new.ssl", "procedure fresh begin\r\nend\r\n");

			await index.UpdateFileAsync(added);
			Assert.Equal(new[] { "fresh" }, Names(index.Search(null, 100)).ToArray());

			File.Delete(added);
			await index.UpdateFileAsync(added);
			Assert.Equal(0, index.SymbolCount);
			Assert.Equal(0, index.FileCount);
		}

		[Fact]
		public async Task UpdateFileAsync_IgnoresFilesThatAreNotScripts()
		{
			using ProjectSymbolIndex index = Create();
			String path = this.Write("note.txt", "procedure fresh begin\r\nend\r\n");

			await index.UpdateFileAsync(path);

			Assert.Equal(0, index.FileCount);
		}

		[Fact]
		public async Task Clear_ForgetsEverything()
		{
			this.Write("a.ssl", "procedure one begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			index.Clear();

			Assert.Equal(0, index.SymbolCount);
			Assert.Empty(index.Find("one"));
		}

		[Fact]
		public async Task Search_RanksPrefixBeforeWordStartBeforeSubstring()
		{
			this.Write("a.ssl", string.Join("\r\n", new[] { "procedure xlevelx begin end", "procedure get_level begin end", "procedure level_up begin end", "procedure level begin end", "procedure GetLevel begin end", "procedure unlevelled begin end" }) + "\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlyList<IndexedSymbol> result = index.Search("level", 10);

			Assert.Equal(new[] { "level", "level_up", "GetLevel", "get_level", "xlevelx", "unlevelled" }, Names(result).ToArray());
		}

		[Fact]
		public async Task Search_IsCaseInsensitive_AndTheMaximumLimitsTheResult()
		{
			this.Write("a.ssl", string.Join("\r\n", Enumerable.Range(0, 30).Select(i => "procedure Proc" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + " begin end")) + "\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlyList<IndexedSymbol> result = index.Search("PROC", 5);

			Assert.Equal(new[] { "Proc00", "Proc01", "Proc02", "Proc03", "Proc04" }, Names(result).ToArray());
			Assert.Empty(index.Search("proc", 0));
		}

		[Fact]
		public async Task Search_AFilterOfSeveralWordsAlsoMatchesTheFileName()
		{
			this.Write("alpha.ssl", "procedure start begin end\r\n");
			this.Write("beta.ssl", "procedure start begin end\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IndexedSymbol only = Assert.Single(index.Search("start beta", 10));

			Assert.Equal("beta.ssl", only.FileName);
			Assert.Empty(index.Search("start gamma", 10));
		}

		[Fact]
		public async Task Search_AKindFilterLimitsTheResult()
		{
			this.Write("a.ssl", "#define ITEM 1\r\nvariable item_count;\r\nprocedure item_use begin end\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			Assert.Equal(3, index.Search("item", 10).Count);
			Assert.Equal(new[] { "ITEM" }, Names(index.Search("item", 10, IndexedSymbolKind.Macro)).ToArray());
			Assert.Equal(2, index.Search("item", 10, IndexedSymbolKind.Macro | IndexedSymbolKind.Variable).Count);
		}

		[Fact]
		public async Task FindOccurrencesAsync_SearchesTheIndexedFiles_AndOpenTextsWinOverTheDisk()
		{
			String a = this.Write("a.ssl", "procedure helper begin\r\nend\r\n");
			String b = this.Write("b.ssl", "procedure start begin\r\n\thelper();\r\nend\r\n");
			this.Write("c.ssl", "procedure other begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlyList<SymbolOccurrence> disk = await index.FindOccurrencesAsync(new SymbolOccurrenceRequest("HELPER"));
			Assert.Equal(new[] { a, b }, disk.Select(o => o.File).ToArray());
			Assert.Equal(new[] { SymbolOccurrenceKind.Declaration, SymbolOccurrenceKind.Reference }, disk.Select(o => o.Kind).ToArray());

			Dictionary<String, String> open = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase) { [b] = "procedure start begin\r\n\tother();\r\nend\r\n" };
			IReadOnlyList<SymbolOccurrence> edited = await index.FindOccurrencesAsync(new SymbolOccurrenceRequest("helper") { OpenTexts = open });
			Assert.Equal(new[] { a }, edited.Select(o => o.File).ToArray());
		}

		[Fact]
		public async Task FindOccurrencesAsync_ASetOfFilesLimitsTheSearch_AndMacrosAreCaseSensitive()
		{
			String a = this.Write("a.ssl", "#define LIM 1\r\nx = LIM + lim;\r\n");
			this.Write("b.ssl", "x = LIM;\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlyList<SymbolOccurrence> result = await index.FindOccurrencesAsync(new SymbolOccurrenceRequest("LIM") { CaseSensitive = true, Files = new[] { a } });

			Assert.Equal(2, result.Count);
			Assert.All(result, o => Assert.Equal(a, o.File));
		}

		[Fact]
		public async Task FindOccurrencesAsync_CanBeCancelled()
		{
			this.Write("a.ssl", "procedure helper begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			using CancellationTokenSource cancel = new CancellationTokenSource();
			cancel.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.FindOccurrencesAsync(new SymbolOccurrenceRequest("helper"), cancel.Token));
		}

		[Fact]
		public async Task GetFilesSeeing_FindsTheFilesThatIncludeAHeaderDirectlyOrNot()
		{
			String common = this.Write("headers/common.h", "#define COMMON 1\r\n");
			String middle = this.Write("headers/middle.h", "#include \"common.h\"\r\n#define MIDDLE 1\r\n");
			String script = this.Write("scripts/a.ssl", "#include \"..\\headers\\middle.h\"\r\nprocedure start begin\r\nend\r\n");
			this.Write("scripts/b.ssl", "procedure start begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));
			IncludeResolver resolver = new IncludeResolver(null, null);

			IReadOnlySet<String> seeing = index.GetFilesSeeing(common, resolver);

			Assert.True(seeing.SetEquals(new[] { common, middle, script }));
		}

		[Fact]
		public async Task GetFilesSeeing_UsesTheHeadersFolderOfTheResolver()
		{
			String header = this.Write("headers/common.h", "#define COMMON 1\r\n");
			String script = this.Write("scripts/a.ssl", "#include \"common.h\"\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlySet<String> seeing = index.GetFilesSeeing(header, new IncludeResolver(Path.Combine(this._folder.Path, "headers"), null));

			Assert.True(seeing.SetEquals(new[] { header, script }));
		}

		[Fact]
		public async Task GetIncludeClosure_FollowsTheIncludesOfTheIndexedFiles_AndSurvivesCycles()
		{
			String a = this.Write("a.h", "#include \"b.h\"\r\n");
			String b = this.Write("b.h", "#include \"c.h\"\r\n#include \"a.h\"\r\n");
			String c = this.Write("c.h", "#define C 1\r\n");
			this.Write("d.h", "#define D 1\r\n");
			using ProjectSymbolIndex index = Create();
			await index.UpdateAsync(Roots(this._folder.Path));

			IReadOnlySet<String> closure = index.GetIncludeClosure(new[] { a }, new IncludeResolver(null, null));

			Assert.True(closure.SetEquals(new[] { a, b, c }));
		}

		[Fact]
		public async Task Search_FindsSymbolsOfTheRealSampleScripts()
		{
			String source = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			if (!Directory.Exists(source))
			{
				return;
			}

			using ProjectSymbolIndex index = Create();

			await index.UpdateAsync(Roots(source));

			Assert.True(index.FileCount >= 27);
			IndexedSymbol procedure = index.Find("is_in_array", IndexedSymbolKind.Procedure).First(s => s.FileName == "gl_g_ammobox.ssl");
			Assert.True(procedure.HasBody);
			Assert.True(procedure.DeclarationLine < procedure.Line);
			IndexedSymbol macro = index.Find("ndebug").First(s => s.FileName == "gl_g_ammobox.ssl");
			Assert.Equal("ndebug(msg)", macro.Detail);
			Assert.Contains(index.Find("box_num", IndexedSymbolKind.Variable), s => s.FileName == "gl_g_ammobox.ssl");
			Assert.NotEmpty(index.Search("hp", 10));
			Assert.True(index.Find("start", IndexedSymbolKind.Procedure).Count >= 20);
		}

		[Fact]
		public async Task UpdateAsync_TheGeneratedProjectIsIndexedWithTheSameSymbolsTheParsersFind()
		{
			String script = this.Write(
				"a.ssl",
				"#define M1 1\r\n#define M2(x) (x)\r\nvariable g1, g2;\r\nprocedure p1;\r\nprocedure p1 begin\r\nend\r\nprocedure p2(variable a) begin\r\nend\r\n");
			using ProjectSymbolIndex index = Create();

			await index.UpdateAsync(Roots(this._folder.Path));

			String text = File.ReadAllText(script);
			ProgramInfo info = new InternalParser().Parse(text, script);
			SslScriptModel model = SslScriptModel.Analyze(text);
			Assert.Equal(info.Procedures.Count, index.Search(null, 100, IndexedSymbolKind.Procedure).Count);
			Assert.Equal(model.Declarations.Count(d => d.Kind == VariableDeclarationKind.Global), index.Search(null, 100, IndexedSymbolKind.Variable).Count);
			Assert.Equal(new MacroCollector(new IncludeResolver(null, null)).CollectFromText(text, script, false).Count, index.Search(null, 100, IndexedSymbolKind.Macro).Count);
		}
	}
}
