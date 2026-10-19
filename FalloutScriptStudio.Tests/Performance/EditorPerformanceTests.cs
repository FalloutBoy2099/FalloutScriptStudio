namespace FalloutScriptStudio.Tests.Performance
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Highlighting;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// Timings of the editor on the generated 5 000 and 20 000 line scripts: opening, keystrokes, the application of a parse result,
	/// completion, tips and occurrence marks. The budgets are several times the time measured on the development machine.
	/// </summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class EditorPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public EditorPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		/// <summary>Parses a generated script the way the application does without the native parser: procedures and macros of the script and its header.</summary>
		internal static ProgramInfo ParseLikeTheApplication(String text, String path)
		{
			MacroCollector collector = new MacroCollector(new IncludeResolver(null, null));
			return new InternalParser().Parse(text, path).WithScriptMacros(collector.CollectFromText(text, path, true)).WithFlags(isParsed: true);
		}

		private static String Format(Double value, String format = "F1")
		{
			return value.ToString(format, CultureInfo.InvariantCulture);
		}

		private static Window ShowInWindow(FrameworkElement view)
		{
			Window window = OffscreenWindow.Show(view, 1000, 700);
			UiThread.Flush();
			return window;
		}

		private static void CloseWindow(Window window)
		{
			window.Content = null;
			window.Close();
			UiThread.Flush();
		}

		[Fact]
		public void Lexical_map_of_a_20k_script_follows_the_edits()
		{
			String text = this._inputs.Script20kText;
			TextDocument document = new TextDocument(text);
			this._meter.Measure("TextDocument.Text of 20k lines (copy)", 30, () => Assert.True(document.Text.Length > 0));
			this._meter.Measure("LexicalMap.Build 20k lines", 40, () => Assert.NotEmpty(LexicalMap.Build(text).Spans));
			this._meter.Measure("new TextDocument(20k lines)", 150, () => _ = new TextDocument(text));

			using DocumentLexicalMap map = new DocumentLexicalMap(document);
			LexicalMap before = map.Current;
			Int32 offset = document.TextLength / 2;
			this._meter.Measure("DocumentLexicalMap after one edit in the middle (scans the text around the edit only)", 20, () =>
			{
				document.Insert(offset, "x");
				Assert.NotEmpty(map.Current.Spans);
			}, 20);

			// The incremental map is the map of a full scan, also after a typed comment opener that changes everything below it.
			document.Insert(100, "/*");
			Assert.True(LexicalMap.Build(document.Text).Spans.SequenceEqual(map.Current.Spans));
			document.Insert(document.TextLength - 10, "*/");
			Assert.True(LexicalMap.Build(document.Text).Spans.SequenceEqual(map.Current.Spans));
			Assert.NotSame(before, map.Current);
		}

		[Fact]
		public void Highlighting_definition_and_the_cost_per_line()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				HighlightingProvider provider = env.Services.Highlighting;
				HashSet<String> procedures = info.Procedures.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
				HashSet<String> macros = info.ScriptMacros.Values.Where(m => String.Equals(m.DeclaredFile, path, StringComparison.OrdinalIgnoreCase)).Select(m => m.Token).ToHashSet(StringComparer.Ordinal);
				this._meter.Measure("HighlightingProvider.Create (" + procedures.Count + " procedure names, " + macros.Count + " macros)", 100, () => provider.Create(HighlightingKind.Ssl, env.Theme.SyntaxTheme, macros, procedures), 5);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Ssl, env.Theme.SyntaxTheme, macros, procedures);
				TextDocument document = new TextDocument(text);
				Int32 lines = 5000;
				Double upTo = this._meter.MeasureOnce("DocumentHighlighter: state of the highlighting up to line " + lines + " (what a jump to a line costs)", 400, () => new DocumentHighlighter(document, definition).HighlightLine(lines));
				Double each = this._meter.MeasureOnce("DocumentHighlighter: highlight " + lines + " lines one by one (what drawing lines costs)", 3000, () =>
				{
					DocumentHighlighter highlighter = new DocumentHighlighter(document, definition);
					for (Int32 line = 1; line <= lines; line++)
					{
						highlighter.HighlightLine(line);
					}
				});
				this._meter.Note("highlighting: " + Format(upTo * 1000.0 / lines) + " us per line to reach a line, " + Format(each * 1000.0 / lines) + " us per line drawn");
			});
		}

		[Fact]
		public void Bracket_matching_20k()
		{
			String text = this._inputs.Script20kText;
			TextDocument document = new TextDocument(text);
			LexicalMap map = LexicalMap.Build(text);
			Int32 matched = text.IndexOf("if (", StringComparison.Ordinal) + 3;
			this._meter.Measure("BracketMatcher.FindPairOf: a bracket with a partner a few characters away", 5, () => Assert.NotNull(BracketMatcher.FindPairOf(document, map, matched)), 20);
			TextDocument broken = new TextDocument("procedure p begin\r\n   foo(\r\n" + text);
			LexicalMap brokenMap = LexicalMap.Build(broken.Text);
			Int32 unmatched = broken.Text.IndexOf("foo(", StringComparison.Ordinal) + 3;
			this._meter.Measure("BracketMatcher.FindPairOf: an opening bracket without a partner (scans to the end of 20k lines)", 200, () => Assert.Null(BracketMatcher.FindPairOf(broken, brokenMap, unmatched)), 5);
			TextDocument broken2 = new TextDocument(text + "\r\n   )");
			LexicalMap broken2Map = LexicalMap.Build(broken2.Text);
			Int32 closer = broken2.TextLength - 1;
			this._meter.Measure("BracketMatcher.FindPairOf: a closing bracket without a partner (scans back to the start)", 150, () => BracketMatcher.FindPairOf(broken2, broken2Map, closer), 5);
		}

		[Theory]
		[InlineData(5000, 75)]
		[InlineData(20000, 300)]
		public void Opening_a_script_to_the_first_paint(Int32 lines, Int32 procedures)
		{
			String path = this._inputs.GetScriptPath(lines, procedures);
			String text = File.ReadAllText(path);
			Byte[] bytes = File.ReadAllBytes(path);
			ProgramInfo info = ParseLikeTheApplication(text, path);
			this._meter.Note("peak working set of the test process before opening: " + Format(PerfMeter.PeakWorkingSetMegabytes, "F0") + " MB");
			this._meter.Measure("DocumentTextCodec.Decode + NormalizeNewLines " + lines + " lines", 80, () => FalloutScriptStudio.Workspace.DocumentTextCodec.NormalizeNewLines(FalloutScriptStudio.Workspace.DocumentTextCodec.Decode(bytes, System.Text.Encoding.Latin1).Text));

			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				List<Double> times = [];
				for (Int32 i = 0; i < 3; i++)
				{
					Stopwatch watch = Stopwatch.StartNew();
					DocumentViewModel document = env.CreateDocument(text, path, false);
					document.ProgramInfo = info;
					ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
					Window window = ShowInWindow(view);
					watch.Stop();
					if (i > 0)
					{
						times.Add(watch.Elapsed.TotalMilliseconds);
					}

					CloseWindow(window);
				}

				times.Sort();
				Double median = times[times.Count / 2];
				this._meter.Note("Open a script of " + lines + " lines in a ScriptEditorView (document, session, window layout): median " + Format(median) + " ms, budget 3000 ms");
				Assert.True(median < 3000, "opening the script took " + Format(median) + " ms");
			});
			this._meter.Note("peak working set of the test process after opening: " + Format(PerfMeter.PeakWorkingSetMegabytes, "F0") + " MB, managed " + Format(PerfMeter.ManagedMegabytes, "F0") + " MB");
		}

		[Fact]
		public void Memory_of_a_20k_script_and_of_a_typing_session()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = info;
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = ShowInWindow(view);
				EditorSession session = view.Session!;
				session.Context.Editor.ScrollToLine(10000);
				session.Context.Editor.UpdateLayout();
				UiThread.Flush();
				Double opened = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				this._meter.Note("managed memory: " + Format(before) + " MB before, " + Format(opened) + " MB with the 20k line script open in an editor (" + Format(opened - before) + " MB for " + Format(text.Length / (1024.0 * 1024.0), "F2") + " M characters)");
				Assert.True(opened - before < 40, "the editor of a script of 20000 lines needs " + Format(opened - before) + " MB");

				Int32 collections2 = GC.CollectionCount(2);
				Int64 allocated = GC.GetAllocatedBytesForCurrentThread();
				Int32 keystrokes = 60;
				for (Int32 i = 0; i < keystrokes; i++)
				{
					TypingSimulator.Type(session, "x");
					UiThread.Flush();
					if (i % 10 == 9)
					{
						// What the end of a parse does on the UI thread: the new result is applied (folding, highlighting, panels).
						document.ProgramInfo = info.WithFlags(isParsed: true);
						UiThread.Flush();
					}
				}

				Double perKeystroke = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (1024.0 * 1024.0) / keystrokes;
				Int32 fullCollections = GC.CollectionCount(2) - collections2;
				this._meter.Note("typing session of " + keystrokes + " keystrokes with 6 parse results: " + Format(perKeystroke, "F2") + " MB allocated per keystroke on the UI thread, " + fullCollections + " full collections");
				Assert.True(perKeystroke < 4.0, "a keystroke (with its share of the parse results) allocates " + Format(perKeystroke, "F2") + " MB");

				Int64 a0 = GC.GetAllocatedBytesForCurrentThread();
				TypingSimulator.Type(session, "x");
				UiThread.Flush();
				Int64 a1 = GC.GetAllocatedBytesForCurrentThread();
				document.ProgramInfo = info.WithFlags(isParsed: true);
				UiThread.Flush();
				Int64 a2 = GC.GetAllocatedBytesForCurrentThread();
				this._meter.Note("allocations: one keystroke " + Format((a1 - a0) / 1024.0, "F0") + " KB, applying a parse result " + Format((a2 - a1) / 1024.0, "F0") + " KB");
				Assert.True(a1 - a0 < 1024 * 1024, "a keystroke allocates " + Format((a1 - a0) / 1024.0, "F0") + " KB");
				CloseWindow(window);
			});
			this._meter.Note("peak working set of the test process: " + Format(PerfMeter.PeakWorkingSetMegabytes, "F0") + " MB");
		}

		[Theory]
		[InlineData(5000, 75)]
		[InlineData(20000, 300)]
		public void Keystrokes_and_parse_results(Int32 lines, Int32 procedures)
		{
			String path = this._inputs.GetScriptPath(lines, procedures);
			String text = File.ReadAllText(path);
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = info;
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = ShowInWindow(view);
				EditorSession session = view.Session!;
				ICSharpCode.AvalonEdit.Editing.TextArea area = session.Context.TextArea;
				Int32 middle = lines / 2;
				area.Caret.Offset = document.TextDocument.GetLineByNumber(middle).Offset;
				session.Context.Editor.ScrollToLine(middle);
				session.Context.Editor.UpdateLayout();
				UiThread.Flush();
				Assert.True(session.Context.TextView.VisualLines.First().FirstDocumentLine.LineNumber > middle - 60);
				String size = " (" + lines + " lines)";

				this._meter.Measure("Type one character in the middle" + size, 150, () =>
				{
					TypingSimulator.Type(session, "x");
					UiThread.Flush();
				}, 10);
				this._meter.Measure("  of which: the input handlers only (no layout)", 80, () => TypingSimulator.Type(session, "y"), 10);
				UiThread.Flush();
				this._meter.Measure("  of which: the layout after an edit", 120, () =>
				{
					document.TextDocument.Insert(area.Caret.Offset, "z");
					session.Context.Editor.UpdateLayout();
				}, 10);

				this._meter.Measure("Delete one character" + size, 150, () =>
				{
					document.TextDocument.Remove(area.Caret.Offset - 1, 1);
					UiThread.Flush();
				}, 10);

				this._meter.Measure("Apply an identical parse result" + size, 200, () =>
				{
					document.ProgramInfo = info.WithFlags(isParsed: true);
					UiThread.Flush();
				}, 5);

				Int32 counter = 0;
				this._meter.Measure("Apply a parse result with a new procedure name (the highlighting is created again)" + size, 800, () =>
				{
					counter++;
					List<ScriptProcedure> list = info.Procedures.ToList();
					list.Add(new ScriptProcedure("added_procedure_" + counter) { DeclaredFile = path, StartFile = path, StartLine = 5, EndLine = 6, DefinedLine = 5, DeclaredLine = 5 });
					document.ProgramInfo = info.WithProcedures(list);
					UiThread.Flush();
				}, 3);

				Int32 pages = 0;
				this._meter.MeasureOnce("Scroll 20 pages down (layout + highlighting of the visible lines)" + size, 3000, () =>
				{
					for (Int32 line = 100; line < lines && pages < 20; line += 400)
					{
						session.Context.Editor.ScrollToLine(line);
						session.Context.Editor.UpdateLayout();
						pages++;
					}
				});

				CloseWindow(window);
			});
		}

		[Fact]
		public void Folding_of_a_20k_script()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = info;
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = ShowInWindow(view);
				EditorSession session = view.Session!;
				Assert.True(session.Folding.Count >= 300);
				this._meter.Measure("FoldingController.Update 20k lines", 150, () => session.Folding.Update(), 5);
				this._meter.Measure("Collapse all procedures (300 foldings) and expand them again", 300, () =>
				{
					session.Folding.CollapseAll();
					UiThread.Flush();
					session.Folding.ExpandAll();
					UiThread.Flush();
				}, 3);
				this._meter.Measure("FoldingController.IsFoldStart x 200 lines", 100, () =>
				{
					for (Int32 line = 1; line < 20000; line += 100)
					{
						session.Folding.IsFoldStart(line);
					}
				}, 5);
				CloseWindow(window);
			});
		}

		[Fact]
		public void Completion_list_with_2000_macros()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = info;
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = ShowInWindow(view);
				EditorSession session = view.Session!;
				ICSharpCode.AvalonEdit.Editing.TextArea area = session.Context.TextArea;
				Int32 line = 5000;
				while (document.TextDocument.GetText(document.TextDocument.GetLineByNumber(line)).Trim().Length != 0)
				{
					line++;
				}

				area.Caret.Offset = document.TextDocument.GetLineByNumber(line).Offset;
				session.Context.Editor.ScrollToLine(line);
				UiThread.Flush();
				document.TextDocument.Insert(area.Caret.Offset, "PE");
				area.Caret.Offset = document.TextDocument.GetLineByNumber(line).EndOffset;
				UiThread.Flush();

				this._meter.Measure("CompletionController.Show('PE') ~2000 items (build the list and the window; the window is not shown)", 800, () =>
				{
					Assert.True(session.Completion.Show(true));
					UiThread.Flush();
					session.Completion.Close();
					UiThread.Flush();
				}, 4);

				IReadOnlyList<AutosuggestItem> suggestions = FalloutScriptStudio.Editor.Completion.CompletionSource.GetItems("PE", info, env.Services.GetOpcodeCatalog());
				FalloutScriptStudio.Editor.Completion.CompletionAppearance appearance = new FalloutScriptStudio.Editor.Completion.CompletionAppearance(session.Context.Palette, true, session.Context.FontFamily, session.Context.FontSize);
				this._meter.Measure("  of which: 2000 ScriptCompletionData objects", 30, () => _ = suggestions.Select(item => new FalloutScriptStudio.Editor.Completion.ScriptCompletionData(item, appearance)).ToList(), 5);
				document.TextDocument.Insert(area.Caret.Offset, "rf_func_0001");
				area.Caret.Offset = document.TextDocument.GetLineByNumber(line).EndOffset;
				UiThread.Flush();
				this._meter.Measure("CompletionController.Show with a handful of items (the cost of the window object itself; not shown)", 500, () =>
				{
					Assert.True(session.Completion.Show(true));
					UiThread.Flush();
					session.Completion.Close();
					UiThread.Flush();
				}, 4);
				this._meter.Measure("CompletionSource.GetItems('PE') only", 40, () => Assert.NotEmpty(FalloutScriptStudio.Editor.Completion.CompletionSource.GetItems("PE", info, env.Services.GetOpcodeCatalog())), 10);
				CloseWindow(window);
			});
		}

		[Fact]
		public void Tips_and_occurrences_in_a_20k_script()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo info = ParseLikeTheApplication(text, path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = info;
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = ShowInWindow(view);
				EditorSession session = view.Session!;
				Int32 hotOffset = text.IndexOf(PerformanceInputs.HotMacro + " * ", StringComparison.Ordinal);
				Int32 procOffset = text.IndexOf("perf_proc_150", StringComparison.Ordinal);

				this._meter.Measure("TipController.BuildTips (macro under the pointer)", 20, () => Assert.NotEmpty(session.Tips.BuildTips(hotOffset + 2)), 20);
				this._meter.Measure("TipController.BuildTips (procedure under the pointer)", 20, () => Assert.NotEmpty(session.Tips.BuildTips(procOffset + 2)), 20);
				Int32 shift = 0;
				this._meter.Measure("TipController.BuildTips right after an edit (the lexical map follows the edit)", 30, () =>
				{
					document.TextDocument.Insert(0, " ");
					shift++;
					Assert.NotEmpty(session.Tips.BuildTips(hotOffset + 3 + shift));
				}, 5);
				hotOffset += shift;

				session.Context.TextArea.Caret.Offset = hotOffset + 2;
				this._meter.Measure("OccurrenceController.Highlight a macro used 2000 times (and redraw)", 500, () =>
				{
					session.Occurrences.Highlight(PerformanceInputs.HotMacro, true);
					UiThread.Flush();
					session.Occurrences.Clear();
					UiThread.Flush();
				}, 4);

				TextSpan[] matches = new TextSearcher().FindAll(document.TextDocument.Text, new SearchQuery(PerformanceInputs.HotMacro) { WholeWord = true, MatchCase = true }).ToArray();
				this._meter.Measure("OccurrenceController.SetSearchMatches 2000 matches (and redraw)", 300, () =>
				{
					session.Occurrences.SetSearchMatches(matches);
					UiThread.Flush();
					session.Occurrences.ClearSearchMatches();
					UiThread.Flush();
				}, 4);

				CloseWindow(window);
			});
		}
	}
}