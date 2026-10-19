namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Core.Tests.Archives;
	using FalloutScriptStudio.Core.Tests.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// Timings of the Core algorithms on large generated inputs (see <see cref="PerformanceInputs"/>). The budgets are several times the
	/// time measured on the development machine, so a test fails only when an algorithm became really slower (for example quadratic).
	/// Run with <c>dotnet test --filter Category=Performance -c Release</c>; the timings are printed with the detailed logger verbosity.
	/// </summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class CorePerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public CorePerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		private ScriptParser CreateParser(Boolean native)
		{
			ParserOptions options = new ParserOptions
			{
				EnableNativeParser = native,
				HeadersDirectory = Path.GetDirectoryName(this._inputs.HeaderPath),
			};
			return new ScriptParser(new FakeNativeParser(), () => options);
		}

		[Fact]
		public void Generated_inputs_have_the_documented_shape()
		{
			String script = this._inputs.Script20kText;
			Int32 lines = script.Count(c => c == '\n');
			this._meter.Note("script20k: " + lines + " lines, " + script.Length + " chars");
			Assert.InRange(lines, 19000, 22000);
			Assert.Equal(PerformanceInputs.HotMacroUses + 1, CountOccurrences(script, PerformanceInputs.HotMacro + " "));
			Assert.Equal(PerformanceInputs.HeaderMacroCount, this._inputs.HeaderText.Split("#define ").Length - 1);
			Assert.InRange(File.ReadAllLines(this._inputs.MessagePath).Length, 29990, 30100);
		}

		[Fact]
		public async Task Parse_20k_script_from_scratch()
		{
			ScriptParser parser = this.CreateParser(false);
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			await this._meter.MeasureAsync("ScriptParser.ParseAsync 20k lines (internal, from scratch, header of 2000 macros)", 300, async () =>
			{
				ParseResult result = await parser.ParseAsync(new ParseRequest(text) { FilePath = path, IsFirstPass = false }, CancellationToken.None);
				Assert.True(result.ProgramInfo.Procedures.Count >= 300);
				Assert.True(result.ProgramInfo.ScriptMacros.Count >= PerformanceInputs.HeaderMacroCount);
			}, 3);
		}

		[Fact]
		public async Task Reparse_20k_script_after_a_keystroke()
		{
			ScriptParser parser = this.CreateParser(true);
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ParseResult first = await parser.ParseAsync(new ParseRequest(text) { FilePath = path, IsFirstPass = true }, CancellationToken.None);
			ProgramInfo info = first.ProgramInfo;
			Int32 half = text.Length / 2;
			await this._meter.MeasureAsync("ScriptParser.ParseAsync 20k lines (merge into previous result, as after a keystroke)", 300, async () =>
			{
				String edited = text.Insert(half, "x");
				ParseResult result = await parser.ParseAsync(new ParseRequest(edited) { FilePath = path, PreviousInfo = info }, CancellationToken.None);
				Assert.Equal(info.Procedures.Count, result.ProgramInfo.Procedures.Count);
			}, 3);
		}

		[Fact]
		public void Internal_parser_and_macro_collection_20k()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			InternalParser parser = new InternalParser();
			this._meter.Measure("InternalParser.Parse 20k lines", 100, () => parser.Parse(text, path));

			MacroCollector collector = new MacroCollector(new IncludeResolver(null, null));
			this._meter.Measure("MacroCollector.CollectFromText 20k lines + include of 2000 macros", 200, () => collector.CollectFromText(text, path, true));
			this._meter.Measure("MacroCollector.CollectFromFile header of 2000 macros", 60, () => collector.CollectFromFile(this._inputs.HeaderPath, new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal), false));
		}

		[Fact]
		public async Task Global_macros_and_includes_of_a_big_headers_folder()
		{
			String headers = this._inputs.HeaderFolder;
			MacroRegistry registry = new MacroRegistry();
			this._meter.Measure("MacroRegistry.LoadFromDirectory 300 header files (30000 macros)", 2500, () => Assert.Equal(30000, registry.LoadFromDirectory(headers)), 3);
			foreach (String header in Directory.GetFiles(headers, "*.h", SearchOption.AllDirectories))
			{
				File.SetLastWriteTimeUtc(header, DateTime.UtcNow.AddDays(-1));
			}

			registry.LoadFromDirectory(headers, null, true);
			this._meter.Measure("MacroRegistry.LoadFromDirectory 300 header files, unchanged since the last load", 100, () => Assert.Equal(30000, registry.LoadFromDirectory(headers, null, true)), 10);

			StringBuilder script = new StringBuilder();
			for (Int32 file = 0; file < 40; file++)
			{
				script.Append("#include \"group").Append(file % 10).Append("\\hdr_").Append(file.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)).Append(".h\"\r\n");
			}

			script.Append("procedure start begin\r\nend\r\n");
			String path = Path.Combine(headers, "group0", "script_with_includes.ssl");
			ScriptParser parser = new ScriptParser(new FakeNativeParser(), () => new ParserOptions { EnableNativeParser = false, HeadersDirectory = headers });
			await this._meter.MeasureAsync("ScriptParser.ParseAsync of a script that includes 40 headers (4000+ macros)", 300, async () =>
			{
				ParseResult result = await parser.ParseAsync(new ParseRequest(script.ToString()) { FilePath = path }, CancellationToken.None);
				Assert.True(result.ProgramInfo.ScriptMacros.Count >= 4000);
			}, 5);
		}

		[Fact]
		public void The_cached_load_of_the_headers_gives_the_same_macros_as_a_full_load()
		{
			String headers = this._inputs.HeaderFolder;
			foreach (String header in Directory.GetFiles(headers, "*.h", SearchOption.AllDirectories))
			{
				File.SetLastWriteTimeUtc(header, DateTime.UtcNow.AddDays(-2));
			}

			MacroRegistry full = new MacroRegistry();
			full.LoadFromDirectory(headers);
			MacroRegistry cached = new MacroRegistry();
			cached.LoadFromDirectory(headers, null, true);
			Int32 count = cached.LoadFromDirectory(headers, null, true);

			Assert.Equal(30000, count);
			Assert.Equal(full.Macros.Keys, cached.Macros.Keys);
			foreach (String token in new[] { "HDR0_M0", "HDR150_M42", "HDR299_M99" })
			{
				ScriptMacro expected = full.Macros[token];
				ScriptMacro actual = cached.Macros[token];
				Assert.Equal(expected.DefinitionName, actual.DefinitionName);
				Assert.Equal(expected.Code, actual.Code);
				Assert.Equal(expected.DeclaredFile, actual.DeclaredFile);
				Assert.Equal(expected.DeclaredLine, actual.DeclaredLine);
				Assert.Equal(expected.Description, actual.Description);
			}
		}

		[Fact]
		public void The_first_matches_of_a_text_are_the_first_of_all_the_matches_and_are_found_quickly()
		{
			String text = this._inputs.Script20kText;
			TextSearcher searcher = new TextSearcher();
			SearchQuery everywhere = new SearchQuery("e");
			SearchQuery whole = new SearchQuery(PerformanceInputs.HotMacro) { WholeWord = true, MatchCase = true };
			SearchQuery regex = new SearchQuery(@"PERF_(CONST|DOC)_\d+") { UseRegex = true };

			foreach (SearchQuery query in new[] { everywhere, whole, regex })
			{
				IReadOnlyList<TextSpan> all = searcher.FindAll(text, query);
				Assert.Equal(all.Take(2000), searcher.FindAll(text, query, 2000));
				Assert.Equal(all.Take(1), searcher.FindAll(text, query, 1));
				Assert.Empty(searcher.FindAll(text, query, 0));
				Assert.Equal(all, searcher.FindAll(text, query, Int32.MaxValue));
			}

			this._meter.Measure("TextSearcher.FindAll first 2000 of 100000 matches ('e', what the search box highlights)", 30, () => Assert.Equal(2000, searcher.FindAll(text, everywhere, 2000).Count));
		}

		[Fact]
		public void Folding_regions_20k()
		{
			String text = this._inputs.Script20kText;
			ProgramInfo info = new InternalParser().Parse(text, this._inputs.Script20kPath);
			FoldingGenerator generator = new FoldingGenerator();
			this._meter.Measure("FoldingGenerator.Generate 20k lines", 100, () => generator.Generate(text, this._inputs.Script20kPath, info.Procedures));
		}

		[Fact]
		public void Autosuggest_and_lookup_with_2000_macros()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			MacroCollector collector = new MacroCollector(new IncludeResolver(null, null));
			SortedDictionary<String, ScriptMacro> macros = collector.CollectFromText(text, path, true);
			ProgramInfo info = new InternalParser().Parse(text, path).WithScriptMacros(macros);
			OpcodeCatalog opcodes = new OpcodeCatalog(File.ReadAllLines(Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions", "opcodes.txt")));

			this._meter.Measure("ProgramInfo.GetAutosuggest('PE') ~2000 matches", 40, () => Assert.True(info.GetAutosuggest("PE", opcodes).Count >= PerformanceInputs.HeaderMacroCount / 2));
			this._meter.Measure("ProgramInfo.GetAutosuggest('perf_func_1') ", 20, () => info.GetAutosuggest("perf_func_1", opcodes));
			this._meter.Measure("ProgramInfo.GetAutosuggest('de') opcodes only", 20, () => info.GetAutosuggest("de", opcodes));
			this._meter.Measure("ProgramInfo.Lookup 1000 words (tooltips)", 150, () =>
			{
				for (Int32 i = 0; i < 1000; i++)
				{
					info.Lookup("PERF_CONST_" + ((i * 4) % PerformanceInputs.HeaderMacroCount).ToString("D4", System.Globalization.CultureInfo.InvariantCulture), path, 5000);
					info.Lookup("perf_proc_" + (i % 300).ToString("D3", System.Globalization.CultureInfo.InvariantCulture), path, 5000);
				}
			});
			this._meter.Measure("ProgramInfo.GetProcedureFromPosition 20000 lines", 50, () =>
			{
				for (Int32 i = 1; i <= 20000; i += 10)
				{
					info.GetProcedureFromPosition(i);
				}
			});
		}

		[Fact]
		public void Text_search_20k()
		{
			String text = this._inputs.Script20kText;
			TextSearcher searcher = new TextSearcher();
			SearchQuery plain = new SearchQuery(PerformanceInputs.HotMacro);
			SearchQuery whole = new SearchQuery(PerformanceInputs.HotMacro) { WholeWord = true, MatchCase = true };
			SearchQuery regex = new SearchQuery(@"PERF_(CONST|DOC)_\d+") { UseRegex = true };

			this._meter.Measure("TextSearcher.FindAll plain (2000 matches)", 30, () => Assert.Equal(PerformanceInputs.HotMacroUses + 1, searcher.FindAll(text, plain).Count));
			this._meter.Measure("TextSearcher.FindAll whole word (2000 matches)", 40, () => Assert.Equal(PerformanceInputs.HotMacroUses + 1, searcher.FindAll(text, whole).Count));
			this._meter.Measure("TextSearcher.FindAll regex", 60, () => Assert.NotEmpty(searcher.FindAll(text, regex)));
			this._meter.Measure("TextSearcher.FindAllLines whole word", 100, () => Assert.Equal(PerformanceInputs.HotMacroUses + 1, searcher.FindAllLines(text, "x.ssl", whole).Count));
			this._meter.Measure("TextSearcher.FindNext 200 steps (whole word)", 60, () =>
			{
				Int32 position = 0;
				for (Int32 i = 0; i < 200; i++)
				{
					TextSpan? found = searcher.FindNext(text, whole, position, true);
					position = found!.Value.End;
				}
			});
			this._meter.Measure("TextSearcher.FindPrevious regex from the end", 60, () => Assert.NotNull(searcher.FindPrevious(text, regex, text.Length, true)));
			this._meter.Measure("TextSearcher.ReplaceAll whole word (2000 matches)", 200, () => Assert.Equal(PerformanceInputs.HotMacroUses + 1, searcher.ReplaceAll(text, whole, "PERF_RENAMED").Edits.Count));
		}

		[Fact]
		public void Rename_a_macro_used_2000_times()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			MacroCollector collector = new MacroCollector(new IncludeResolver(null, null));
			SortedDictionary<String, ScriptMacro> macros = collector.CollectFromText(text, path, true);
			ProgramInfo info = new InternalParser().Parse(text, path).WithScriptMacros(macros);
			ScriptMacro macro = macros[PerformanceInputs.HotMacro];
			RefactoringService service = new RefactoringService();
			RefactorDocument[] documents = { new RefactorDocument(path, text, true) };

			this._meter.Measure("RefactoringService.PreviewMacroRename (2000 uses)", 40, () => Assert.Equal(PerformanceInputs.HotMacroUses + 1, service.PreviewMacroRename(macro, "PERF_COLD", documents).Matches.Count));
			this._meter.Measure("RefactoringService.Rename macro (2000 uses)", 60, () =>
			{
				RenameResult result = service.Rename(new RenameRequest(info, macro, "PERF_COLD", path, documents));
				Assert.True(result.Succeeded);
			});
		}

		[Fact]
		public void Messages_parse_check_format_30k()
		{
			String[] lines = File.ReadAllLines(this._inputs.MessagePath);
			MessageStructureChecker checker = new MessageStructureChecker();
			this._meter.Measure("MessageDocument.Parse 30k lines", 200, () => MessageDocument.Parse(lines));
			MessageDocument document = MessageDocument.Parse(lines);
			String text = String.Join("\r\n", document.Format());
			this._meter.Measure("MessageDocument.Format", 80, () => document.Format());
			this._meter.Measure("MessageStructureChecker.Check 30k lines", 100, () => checker.Check(text));
			this._meter.Measure("MessageDocument.BuildTextMap", 20, () => document.BuildTextMap());
			this._meter.Measure("MessageDocument.TryGetText 1000 lookups", 250, () =>
			{
				for (Int32 i = 0; i < 1000; i++)
				{
					document.TryGetText(100 + (i * 7), out _);
				}
			});
		}

		[Fact]
		public async Task Messages_file_service_read_30k()
		{
			MessageFileService service = new MessageFileService(NullLogger<MessageFileService>.Instance);
			await this._meter.MeasureAsync("MessageFileService.ReadAsync 30k lines", 300, async () => Assert.NotEmpty((await service.ReadAsync(this._inputs.MessagePath, Encoding.UTF8)).Entries), 3);
		}

		[Fact]
		public async Task Dialogue_200_nodes_parse_and_flowchart_model()
		{
			String text = File.ReadAllText(this._inputs.DialogueScriptPath);
			ProgramInfo info = new InternalParser().Parse(text, this._inputs.DialogueScriptPath)
				.WithScriptMacros(new MacroCollector(new IncludeResolver(null, null)).CollectFromText(text, this._inputs.DialogueScriptPath, false));
			MessageFileService files = new MessageFileService(NullLogger<MessageFileService>.Instance);
			MessageDocument messages = await files.ReadAsync(this._inputs.DialogueMessagePath, Encoding.UTF8);
			DialogueMessageSource source = new DialogueMessageSource(messages, this._inputs.DialogueMessagePath);
			DialogueNodeParser parser = new DialogueNodeParser(new DialogueFunctionRules());
			ProcedureTextService procedures = new ProcedureTextService();
			FlowchartBuilder builder = new FlowchartBuilder(parser, procedures);
			FlowchartBuildOptions options = new FlowchartBuildOptions();

			IReadOnlyList<String> names = parser.GetNodeProcedureNames(info.Procedures);
			Assert.True(names.Count >= PerformanceInputs.DialogueNodeCount);
			IReadOnlyDictionary<String, String> bodies = procedures.GetProcedureCodes(text, names);
			this._meter.Measure("ProcedureTextService.GetProcedureCodes 200 nodes", 20, () => procedures.GetProcedureCodes(text, names));
			this._meter.Measure("DialogueNodeParser.ParseNodeForDiagram 200 nodes", 60, () =>
			{
				foreach (String name in names)
				{
					parser.ParseNodeForDiagram(bodies[name], info, true);
				}
			});
			await this._meter.MeasureAsync("FlowchartBuilder.BuildAsync 200 nodes", 200, async () =>
			{
				FlowchartModel model = await builder.BuildAsync(text, info, source, options);
				Assert.True(model.Nodes.Count >= PerformanceInputs.DialogueNodeCount);
			}, 3);
		}

		[Fact]
		public void Project_file_listing_5000_files()
		{
			String root = this._inputs.ProjectFolder;
			this._meter.Measure("ProjectFileFinder.EnumerateScriptFiles 5000 files", 300, () => Assert.True(ProjectFileFinder.EnumerateScriptFiles(root).Count >= 3000), 3);
		}

		[Fact]
		public async Task File_search_5000_files()
		{
			String root = this._inputs.ProjectFolder;
			IReadOnlyList<String> files = ProjectFileFinder.EnumerateScriptFiles(root);
			FileSearcher searcher = new FileSearcher(new TextSearcher());
			SearchQuery query = new SearchQuery("NeedleWord_3") { MatchCase = true };
			await this._meter.MeasureAsync("FileSearcher.FindFilesAsync " + files.Count + " files", 10000, async () => Assert.NotEmpty(await searcher.FindFilesAsync(files, query, Encoding.UTF8, null, CancellationToken.None)), 3);
			await this._meter.MeasureAsync("FileSearcher.FindRowsAsync " + files.Count + " files", 10000, async () => Assert.NotEmpty(await searcher.FindRowsAsync(files, query, Encoding.UTF8, null, CancellationToken.None)), 3);
		}

		[Fact]
		public void Dat_archive_of_30000_files()
		{
			DatTestArchiveBuilder builder = new DatTestArchiveBuilder();
			Byte[] content = Encoding.ASCII.GetBytes("procedure start begin\r\nend\r\n");
			for (Int32 i = 0; i < 30000; i++)
			{
				String path = "scripts\\folder" + (i % 300).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\\script" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".int";
				if (i % 5 == 0)
				{
					builder.AddCompressed(path, content);
				}
				else
				{
					builder.AddStored(path, content);
				}
			}

			Byte[] bytes = builder.BuildFallout2();
			String file = Path.Combine(this._inputs.Folder, "big.dat");
			File.WriteAllBytes(file, bytes);
			this._meter.Note("archive: " + bytes.Length / 1024 + " KB, 30000 entries");

			this._meter.Measure("DatArchiveReader.Open 30000 entries", 300, () => Assert.Equal(30000, DatArchiveReader.Open(file).Entries.Count), 5);
			DatArchiveReader reader = DatArchiveReader.Open(file);
			this._meter.Measure("DatArchiveReader.Find x30000", 120, () =>
			{
				for (Int32 i = 0; i < 30000; i++)
				{
					Assert.NotNull(reader.Find("scripts/folder" + (i % 300).ToString(System.Globalization.CultureInfo.InvariantCulture) + "/script" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".int"));
				}
			}, 3);
			this._meter.Measure("DatArchiveReader.GetEntries of 300 folders", 20, () =>
			{
				for (Int32 i = 0; i < 300; i++)
				{
					Assert.Equal(100, reader.GetEntries("scripts\\folder" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Count);
				}
			}, 5);
			this._meter.Measure("DatArchiveReader.OpenEntry + read x500 (stored and compressed)", 300, () =>
			{
				for (Int32 i = 0; i < 500; i++)
				{
					DatEntry entry = reader.Entries[i * 50];
					using Stream stream = reader.OpenEntry(entry);
					using MemoryStream copy = new MemoryStream();
					stream.CopyTo(copy);
					Assert.Equal(content.Length, copy.Length);
				}
			}, 3);
		}

		[Fact]
		public async Task Mass_compile_scheduling_of_5000_scripts()
		{
			String[] scripts = Enumerable.Range(0, 5000).Select(i => "C:\\mod\\scripts\\script" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ssl").ToArray();
			BatchCompileService service = new BatchCompileService(new InstantCompiler());
			CompilerOptions options = new CompilerOptions { ProgramDirectory = "C:\\app", ResourcesDirectory = "C:\\app\\resources", ScratchDirectory = "C:\\app\\scrTemp" };
			CountingProgress progress = new CountingProgress();
			await this._meter.MeasureAsync("BatchCompileService.CompileAsync 5000 scripts (instant compiler, progress reports)", 1500, async () =>
			{
				BatchCompileResult result = await service.CompileAsync(scripts, options, new BatchCompileOptions(), progress);
				Assert.Equal(5000, result.Compiled);
			}, 3);
			await this._meter.MeasureAsync("BatchCompileService.CompileAsync 5000 scripts on one thread", 100, async () =>
			{
				BatchCompileResult result = await service.CompileAsync(scripts, options, new BatchCompileOptions { MultiThreaded = false }, null);
				Assert.Equal(5000, result.Compiled);
			}, 3);
		}

		/// <summary>Counts the reports without posting them anywhere (a Progress on a thread with a synchronization context posts every report).</summary>
		private sealed class CountingProgress : IProgress<BatchCompileProgress>
		{
			private Int32 _reports;

			public void Report(BatchCompileProgress value)
			{
				Interlocked.Increment(ref this._reports);
			}
		}

		private sealed class InstantCompiler : IScriptCompiler
		{
			public Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
			{
				return Task.FromResult(new CompilationResult { Success = true, Output = String.Empty });
			}

			public Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
			{
				throw new NotSupportedException();
			}

			public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
			{
				throw new NotSupportedException();
			}

			public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
			{
				throw new NotSupportedException();
			}
		}

		private static Int32 CountOccurrences(String text, String value)
		{
			Int32 count = 0;
			Int32 index = 0;
			while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}
	}
}