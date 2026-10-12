# Performance

How Fallout Script Studio behaves on large inputs, how it is measured, and what is left to improve.

## Budgets

| Kind of operation | Budget |
|---|---|
| Interactive (a keystroke, a click, a filter, a tooltip, a cursor move) | 100 ms on the UI thread, 16 ms for what runs on every keystroke |
| Opening a document, a parse, a rename preview, a diagram build | 1 s (background work with cancellation and stale-result protection where possible) |
| Batch (search over a whole project, mass compile scheduling) | 1 s of overhead per 5 000 files, always cancellable within about 20 ms |
| Memory (the application is x86: 2 to 4 GB of address space) | no full copy of the text per document, no per-keystroke garbage that triggers full collections |

## The large inputs

`FalloutScriptStudio.Core.Tests\Support\PerformanceInputs.cs` generates deterministic inputs (fixed seed, built from the real procedures of
`_test_files\samples\FO2tweaksMk2\source`) under `_test_files\work` on first use:

* an SSL script of 5 000 and one of 20 000 lines (about 300 procedures, `#define` macros, an include of a generated header with 2 000 macros,
  many variables, nested `begin`/`end`, strings, comments; a macro that is used 2 000 times);
* a `.msg` file of 30 000 lines (comments, multi-line entries, player tabs);
* a dialogue script with 200 nodes and its `.msg` file;
* a project folder with 5 000 files (scripts, headers, messages) for the explorer and the advanced search;
* a folder of 300 header files with 30 000 macros, and a `.dat` archive of 30 000 entries.

## Running the tests

The timing tests carry the trait `Category=Performance`. They are part of a normal `dotnet test` run (each one is below a few seconds, the budgets
are 3 to 5 times the time measured on the development machine, so they fail only when an algorithm really became slower). To run only them and see the
timings (printed as `[perf] name: median ... ms`):

```
dotnet test FalloutScriptStudio.Core.Tests -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"
dotnet test FalloutScriptStudio.Tests      -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"
```

Use Release for numbers you want to compare; Debug is several times slower (the budgets hold in both). The tests run one after the other
(collection `Performance`), and the numbers are noisy when other builds run on the machine: compare medians of several runs.

* `FalloutScriptStudio.Core.Tests\Performance\CorePerformanceTests.cs`: parser, macros, search, refactoring, messages, dialogue model, archives, mass compile.
* `FalloutScriptStudio.Tests\Performance\EditorPerformanceTests.cs`: opening, typing, highlighting, folding, completion, tips, bracket matching, memory.
* `...\SearchPerformanceTests.cs`: folder search, project explorer, symbol trees and filters, global rename preview.
* `...\MessageEditorPerformanceTests.cs`: loading and editing a 30 000 line `.msg`.
* `...\DiagramPerformanceTests.cs`: the flowchart of 200 nodes.
* Equivalence tests for the optimised pieces (they compare with a full recomputation on random or large inputs):
  `LexicalMapIncrementalTests`, `BracketMatcherLargeTextTests`, `FoldingRedrawTests`, `DiagramCachedRenderingTests`, `MessageEditorLargeFileTests`,
  `FunctionsTreeFilterTests`, `ProceduresPanelSyncTests`, `MacroRegistryCacheTests`.

## Measurements

Machine: a laptop with an 8-core / 16-thread 2.0 GHz class mobile CPU, 15 GB of RAM, Windows 10, .NET 10, Release build. The machine was shared with
other builds, so every figure is a median of several runs and only the large differences are meaningful. "Before" is the last commit (`HEAD`) run with the
same tests; "after" is the current tree.

### Optimised

| Operation (20 000 line script unless stated) | Before | After |
|---|---|---|
| Type one character in the middle (layout included) | 28 to 31 ms | 11 to 13 ms |
| Of which the layout after the edit (no more redraw of the whole procedure) | 19 to 26 ms | 6 to 7 ms |
| Delete one character (5 000 lines) | 28 ms | 9 ms |
| Lexical map after one edit (tooltips, brackets, comments/strings state) | 3 to 4 ms | 0.05 ms |
| Tooltips right after an edit | 4 ms | 0.1 ms |
| Bracket matching for a bracket without partner | 13 to 20 ms | 3 to 6 ms |
| Allocation per keystroke (typing session with parse results) | 5.6 MB, 14 full collections | 1.6 MB, 2 full collections |
| Macro registry reload when no header changed (300 files, 30 000 macros) | 96 ms | 3 ms |
| Rename preview of a global macro in a 5 000 file project | 370 ms | 125 ms |
| `.msg` 30 000 lines: edit one row (undo snapshot, structure check, search update) | 25 to 28 ms | 5 ms |
| `.msg`: insert a line / undo / redo | 25 / 35 / 36 ms | 6 / 13 / 12 ms |
| `.msg`: memory of the undo history after 30 edits | 25 MB | 4 MB |
| Flowchart of 200 nodes: select all and draw | 540 ms | 26 to 78 ms |
| Flowchart: expand/collapse all and draw | 655 ms | 400 ms |
| Completion list of 2 000 items | 174 to 310 ms | 150 to 200 ms |
| Highlighting 5 000 lines one by one | 450 to 470 ms | 310 to 365 ms |
| Peak working set of the test process with the 20 000 line script open | about 233 MB | about 180 to 220 MB |

What was done: the lexical map follows a single edit by rescanning the text around it (`LexicalMap.TryUpdate`) instead of copying and lexing the whole text;
the folding manager no longer redraws a whole procedure for each character typed inside it (`FoldingRedrawGate`); the bracket matcher reads the document in chunks of 4 096 characters instead of one
character at a time; the tree panels (procedures, variables, functions) are updated in place and filtered with a single
pass; the message editor runs the structure check once after a pause (250 ms) in the edits for files of 2 000 rows and more, and its undo
snapshots share the state of the rows that did not change; the diagram caches text measurements and the content
drawing of the nodes, so a selection change draws only the frame; `MacroRegistry` skips headers whose timestamp did not change;
`TextSearcher` can stop after the first N matches (the search box highlights only the first 2 000).

### Already fast (no change needed)

| Operation | Time |
|---|---|
| `ScriptParser.ParseAsync` 20 000 lines, from scratch / merged into the previous result | 20 to 40 ms |
| `InternalParser.Parse` 20 000 lines | 6 to 11 ms |
| Script including 40 headers (4 000 macros) | 10 to 17 ms |
| Completion filter source (`GetItems('PE')`, 2 000 matches) / lookup of 1 000 tooltips | 1 ms / 25 to 30 ms |
| Folding regions 20 000 lines | 20 ms |
| Macro rename in the editor (2 000 uses): preview / apply | 2 to 6 ms / 5 to 8 ms |
| `TextSearcher` on 20 000 lines (plain, whole word, regex, replace all) | 0.3 to 6 ms |
| `.msg` 30 000 lines: parse / format / structure check | 14 to 44 ms / 2 to 16 ms / 10 to 17 ms |
| `DialogueNodeParser` + `FlowchartBuilder`, 200 nodes | 20 to 30 ms |
| Project explorer: listing / typing a filter (5 000 files) | 0.1 ms / 0.6 ms (first walk 40 ms) |
| Search over 5 000 files (plain, whole word, regex) | 350 to 430 ms, cancelled within 1 to 13 ms |
| `.dat` archive of 30 000 entries: open / 30 000 lookups | 30 ms / 6 ms |
| Mass compile scheduling of 5 000 scripts | 1 to 6 ms of overhead |
| Open a 5 000 / 20 000 line script in the editor (read, session, layout) | 150 to 180 ms / 230 to 310 ms |

### Memory

* 20 000 line script open in the editor: 6 MB of managed memory on top of the empty editor (the text is held once, in the AvalonEdit rope; the lexical map no
  longer keeps or recreates a full text copy). A typing session of 60 keystrokes allocates 1.6 MB per keystroke on the UI thread (including the share of the
  parse results) with 2 full collections.
* Search over 5 000 files with 4 000 hits: the files are read one at a time (streaming); managed memory grows by 3 to 4 MB, the peak working set of the test
  process stays at about 74 MB (72 MB before). Cancellation stops the search about 1 to 13 ms after the request.
* `.msg` of 30 000 lines: 17 MB managed after loading, 22 MB after 30 edits (undo history 4 MB).

## Known remaining hotspots

* Opening a flowchart with 200 nodes takes about 0.5 to 0.6 s: creating the `FormattedText` of every node (about 1.5 ms per node) and the first draw of the
  glyph runs dominate. Laying out only the visible nodes first would be the next step.
* Expanding or collapsing every node of the flowchart redraws about 400 ms (all layouts change).
* `CompletionController.Show` costs about 115 ms even for a handful of items: it is the creation of the AvalonEdit completion window itself, not the list;
  2 000 items add about 40 ms.
* Highlighting is about 60 to 75 us per drawn line (a full scroll of 20 pages costs 0.5 s in the test, 25 ms per page); the highlighting definition is
  recreated (80 to 90 ms on 20 000 lines) when a parse brings a new procedure name.
* Marking 2 000 occurrences of a macro (or 2 000 search matches) and redrawing takes 60 to 170 ms.
* The advanced search reads every file of the folder; a persistent index would make repeated searches over 5 000 files instant but was not needed to stay
  under the budget.
