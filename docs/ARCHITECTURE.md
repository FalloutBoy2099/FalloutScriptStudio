# Fallout Script Studio — architecture

Rewrite of the legacy *Sfall Script Editor* (WinForms / .NET Framework 4.8 / ICSharpCode.TextEditor) as a
.NET 10 WPF application with the Fluent look of **WPF-UI** and the **AvalonEdit** text editor.

Step-by-step guides to extend it (menu commands, settings, languages, problems, editor commands, test files, options, publishing): [HOW_TO.md](HOW_TO.md).

## Projects

| Project | Target | Role |
|---|---|---|
| `FalloutScriptStudio.Core` | `net10.0` | Pure logic, no UI: parsing, compilation, message files, dialogue parsing, settings, refactoring, archives |
| `FalloutScriptStudio` | `net10.0-windows`, x86 | WPF application |
| `FalloutScriptStudio.ParserHost` | `net10.0`, x86, console | Hosts the optional 32-bit native `parser.dll` in its own process (see below); no UI, references Core |
| `FalloutScriptStudio.Core.Tests` | `net10.0` | Unit tests of Core, plus integration tests against the real sfall tools (skipped when absent) |
| `FalloutScriptStudio.Tests` | `net10.0-windows`, x86 | Unit tests of UI-side logic (router, editor helpers…) |

Dependencies: App → Core. Core never references WPF.

## Runtime layout (next to the executable, same as the legacy editor)

```
FalloutScriptStudio.exe
FalloutScriptStudio.ParserHost.exe                                                 (shipped: 32-bit process that runs parser.dll; built by Publish.ps1, copied by the build)
descriptions\   functions.ini, opcodes.txt, prochandlers.ini, userfunctions.ini   (content, copied from FalloutScriptStudio/Content)
resources\      mcpp.bat, wcc.bat, usercomp.bat, icon_*.ico, templates\*.ssl       (content)
                parser.dll                                                         (shipped: the sfall native parser, 32-bit; Content/resources)
                compile.exe (sslc), mcpp.exe, wcc386.exe                           (NOT shipped: user supplies them, all optional)
                int2ssl.exe, int2ssl_v35.exe                                       (optional: only for the "External int2ssl.exe" decompiler)
                syntax\Ssl.xshd, syntax\Msg.xshd                                   (optional: replace the built-in syntax highlighting definitions, same format as Assets/Syntax)
settings\       settings.json, recent.json, ... (created at runtime)
scrTemp\        scratch folder, wiped at start-up
```

No external tool is needed to **read `.dat` archives** (`FalloutScriptStudio.Core.Archives`: Fallout 1 and Fallout 2 formats, or a folder
standing in for an archive) or to **decompile `.int` scripts** (`FalloutScriptStudio.Core.Decompilation.BuiltInScriptDecompiler`, pure C#).
`undat`/`dat2` are gone. `int2ssl.exe` can still be plugged in: Settings > Compiler > Decompiler > "External int2ssl.exe" with an optional
path (`CompilerSettings.Decompiler` / `ExternalDecompilerPath`; `int2ssl_v35.exe` is the fallback, or the only tool with "Use the old decompiler").
Compilation still needs the user-supplied `compile.exe` (sslc); the preprocessors `mcpp`/`wcc` stay optional. `parser.dll` is shipped in `resources\` (a copy of the sfall one, identical to `_test_files\tools\parser.dll`); a user can still replace it by a newer sfall build, and the editor falls back to the built-in parser when it is missing or broken.

## Settings, scratch folder and instances

* `SettingsService` never writes blank defaults over real data: a file is saved only when this process changed it, and a file that
  changed on disk meanwhile (a second process, such as the stand-alone message editor) is merged property by property (three-way JSON
  merge against the content loaded). A file that exists but cannot be read (after a few retries) is neither taken for absent nor
  overwritten. `App.OnExit` saves only when `Load` completed.
* The scratch folder is prepared and deleted by the main editor only; the stand-alone message editor (`.msg` argument) shares it and never
  purges it. Leftovers that cannot be deleted are logged, never fatal (read-only attributes are cleared first).
* External tools get the scratch folder in the `FSS_SCRATCH` environment variable (`usercomp.bat` uses it); paths on their command lines
  are absolute, quoted and without a trailing backslash (except for a drive root, which is doubled).
* Intermediate preprocessed scripts are named `name_[pre]~<hash of the full path>.ssl`, so scripts of the same name in different folders can
  be compiled in parallel.

## Native parser (`parser.dll`) and the parser host

`parser.dll` is the 32-bit front end of the sfall compiler. It gives exact procedures, variables, references and includes, but it has
bugs that crash the process on ordinary half-typed scripts (a procedure declared twice, a procedure header with a missing comma), and an
access violation cannot be caught on .NET. **The application therefore never loads it.** `OutOfProcessNativeParser` (Core) talks to
`FalloutScriptStudio.ParserHost.exe`, which runs the in-process `NativeParser` engine:

* **Protocol**: JSON lines over stdin/stdout (`Core/Parsing/Interop/Host`: `HostProtocol`, `HostRequest`, `HostResponse`, source-generated
  JSON). Every response line carries the marker `@@FSS-PARSER@@`, so anything the native library prints to stdout is skipped. A new host
  first answers a `ping` (separates "cannot start" from "crashes while parsing").
* **One long-lived host** (started on the first parse, replaced after 500 requests) instead of one process per parse: starting a .NET
  process and loading the library costs hundreds of milliseconds per keystroke pause. A crash, hang, timeout, cancellation or garbage
  answer kills the process tree and loses only that request; the next request starts a new host. `parse_main` status 0 = valid, -1 =
  semantic errors (data still available), 1 = syntax error, 2 = preprocessor error (no data).
* **Crash handling**: the host ends at once and silently on a native crash (`CrashSilencer`: error mode, vectored handler, unhandled
  exception filter; the app also puts it into a job object), so no Windows "has stopped working" / "Exception Processing Message" box can
  appear. A text that crashed the host is not sent again; after 3 crashes in a row (different texts) the native parser is turned off for the
  session (parser.dll replaced = fresh start) and the built-in C# parser takes over (parser status "built-in"). Hosts that cannot even start
  (missing 32-bit runtime) put the parser to sleep for 15 s instead of respawning on every parse.
* The host exits when its stdin closes or its parent process disappears. Its working folder is a private temp folder (parser.dll writes
  `errors.txt` there).
* `FalloutScriptStudio.ParserHost.exe` sits next to the executable. The build copies it to the app output (`ParserHost.targets`);
  `Publish.ps1` publishes it once as a self-contained, trimmed single file shared by `FalloutScriptStudio.exe` and `FalloutScriptStudio-NET.exe`.

## Tests against the real tools

`FalloutScriptStudio.Core.Tests/Native` runs the real `parser.dll` (through the real host), `compile.exe` and `int2ssl.exe`. They are
found in `_test_files\tools` at the repository root (override: `%FSS_SSLC_DIR%`); the real mod sources are the sample sets of
`_test_files\samples` (FO2tweaksMk2 is shipped; InventoryFilterMk2 and PartyOrdersMk2 are optional; override: `%FSS_SSL_SAMPLES%`).
When something is missing the tests are reported as **skipped** (`[RealToolFact]` / `[RealToolTheory]`), not failed.
The protocol, the crash breaker, timeouts and cancellation are also tested with simulated hosts (`FakeParserHost`).

All files of the tests live in `_test_files` (see its `README.md`): the tools and samples are read-only, the tests create their scratch
folders only through `TestFolder` / `TestFiles.CreateWorkFolder` (`_test_files\work\<name>-<guid>`, git-ignored, deleted on dispose and
swept when older than one hour at the start of a run). `TestFiles.cs` and `TestFolder.cs` (in `FalloutScriptStudio.Core.Tests\Support`)
are linked into both test projects. Tests must never use `Path.GetTempPath()` or any other folder outside `_test_files`.

## Composition

* `App.xaml.cs` is a thin WPF shell; the registrations live in `Startup\AppServices.cs` and the start-up / shutdown decisions in `FalloutScriptStudio\Startup` (`StartupPlan`, `StartupCoordinator`, `ShutdownCoordinator`, tested without a WPF `Application`; `CompositionTests` builds the real container with scope and build validation). No service locator in views / view models.
* Services are registered as singletons; windows and per-window view models as transient.
* Fluent theme: `IThemeService` (light / dark / system) → `ApplicationThemeManager` + `SyntaxTheme` for the editor.
* Dialogs: `IDialogService` (Fluent message boxes, prompts, file/folder pickers).
* Single instance: `SingleInstanceService` (named pipe) forwards command-line files to the running instance.

## Patterns

* MVVM with `CommunityToolkit.Mvvm`. Code-behind only for things XAML cannot express (AvalonEdit plumbing, canvas interaction).
* Long operations are `async` with `CancellationToken` (parsing, compilation, searching).
* Colors/brushes: WPF-UI dynamic resources; syntax colors come from `Editor/Highlighting/SyntaxThemes`.
* Syntax highlighting: `Assets/Syntax/*.xshd` (AvalonEdit v2 format, named colors) + `HighlightingProvider` applies a `SyntaxTheme`
  and injects user macros.

## Problems and long operations

**One problem model.** Everything that goes wrong is a `Problem` (`FalloutScriptStudio.Problems`): a severity (Error / Warning / Info), a
source (Parse, Build, Tool, File, Settings, Message), a message (a `Str.Shell.Problem.*` key with arguments, so it follows a language change,
or the text a tool wrote), an optional file / line / column, an optional fix hint (a key) and an optional `ProblemAction` ("Open the
settings: Compiler", "Open the folder"). `ProblemFactory` creates the known ones; `IProblemService` (singleton `ProblemService`) holds the list.
A problem belongs to an *owner* (the document) and/or an *origin* (the producer, `ProblemOrigins`): it is dropped when the document is
re-parsed (source Parse), recompiled (Build and Settings), saved (a "cannot save" note) or closed (everything), and a producer replaces its
own problems when its situation changes (the native parser works again, the next build starts). The services receive the
`IProblemService` as an optional last constructor parameter.

**One list.** The Errors tab of the output panel (`OutputPanelViewModel`) shows the parser and build entries of the active script (the
legacy semantics: Error / Warning / Message, "compile all" replaces them) *and* every reported problem, grouped by source and filterable by
severity (toggles with the counts) and by source (filter menu). Click, double click or Enter on a row goes to the file and line, opening
the file when needed (the list is not replaced by the list of the file that is opened); the link of a row runs its action; "copy selected"
copies the message and its hint, "copy all" one `file(line,column): kind: message` line per row. **Ctrl+F8 / Ctrl+Shift+F8** select the next /
previous error or warning (F8 compiles, F7 is the scripts list, so the Ctrl variants of the compile key were chosen). The status bar shows
the error and warning counts and opens the tab. Search and references results keep their own closable tabs and are not problems. With no
parser entry, no build entry and no problem at all (`OutputPanelViewModel.HasNoProblems`, independent of the filters) the grid is replaced by
the "No problems" text with an icon; when only the filters hide the rows the grid stays. The grid columns have minimum widths (file 110, line
52, message 200) so that the header is never crushed.

**Failures that are reported as problems:** missing or unstartable compile.exe / preprocessor / usercomp.bat / int2ssl.exe (`ToolFailure`
of `CompilationResult` / `DecompilationResult`, the raw log line is then not duplicated in the list), a compilation that fails without an
error position, the output folder not set, parser.dll missing (note) or broken / switched off after crashes (warning), a parse that threw,
files that cannot be opened, saved, reloaded or extracted from an archive, a file read with the fallback code page (note), includes and
message files that are not found, damaged / unreadable / unsaveable settings files (`ISettingsService.Problems` / `ProblemRaised`), an
external editor that does not start, a background operation that failed (`GuardAsync`). Message boxes stay where the user must answer.

**Cancellable operations.** A build command, a mass compilation and the decompilation of a compiled script are each one `IOperation` of the
`IOperationTracker` (only one runs at a time; the other build commands are disabled meanwhile and refused with a status message). The
status bar shows what runs ("Compiling a.ssl (3/12)", a progress bar) and a Cancel button; **Esc in the output panel**, **Ctrl+Break** and
the Compile menu cancel it too. The token goes down to `ProcessRunner`, which kills the whole process tree (the tool, the `cmd.exe` of a
batch file and what it started; a job object is the safety net) and returns only when the processes are gone. `ScriptCompiler` then
restores the previous `.int` (or deletes the half-written one), deletes the preprocessed intermediate files and the `mcpp.err` / `wcc.err`
log, and `BatchCompileService` cancels every compilation in flight. The operation is always ended in a `finally` (a `using`), so the UI
never stays busy after a cancel or an error. Advanced search keeps its own Cancel button and token.

## Projects: mod folders and `fss-project.json`

A *project* is a folder (a mod) that may hold a `fss-project.json`. It overrides, for that mod only, the global options that differ between
mods; with no project open nothing changes.

* **Active project = `Paths.SolutionFolder`.** The folder of the global settings (the one the project explorer lists) is the project folder, so
  the explorer, the older code paths and a legacy `SolutionFolder` keep working: an old solution folder is simply a *project folder without a
  settings file*. `IProjectService` (`ProjectService`, Core) follows that setting whoever changes it (File > Open Project, the explorer, the
  command line), reads the project file, keeps the recent projects (`RecentFiles.Projects`, newest first, 10 at most, in `recent.json`) and
  raises `ProjectChanged`. A folder passed on the command line is opened as a project.
* **`fss-project.json`** (`ProjectSettings`, `ProjectSettingsFile`): versioned, written atomically (`AtomicFile`), unknown properties are
  kept when it is written again, comments and trailing commas are tolerated. A damaged file is copied to `.corrupt` and the project inherits
  everything until the user creates a new one; a file that cannot be read is never overwritten (saving is refused). A `null` value means
  "inherit the global setting"; for the paths and the selected definition an empty text is an override meaning "not set". Paths are
  stored relative to the project folder with forward slashes (portable between machines) and resolved when loaded.
  Overridable: headers folder, `scripts.h`, output folder, "compile next to the source", "search includes", preprocessor, selected
  definition, optimization, short-circuit, backward compatibility, warnings, debug information, message folders (replace the global list),
  the location of `scripts.lst`, and a display name; the definitions of the project are added to the global ones in the toolbar list.
* **One place combines the two: `ISettingsService.Effective`** (`EffectiveSettings`). Its `Settings` is the global `AppSettings` itself when
  the project overrides nothing, otherwise a copy with the overrides applied (the global settings are never modified). Every consumer of the
  compiler, header, include, definition and message folder options reads them from there: `CompilerOptions`, `ParserOptionsFactory`,
  `IncludeResolver`, `MessageFileOptions`, the register-script dialog (`scripts.lst`), the batch compiler, the toolbar list of definitions.
  Options that are not per project (editor, decompiler, user batch file, window layout...) stay on `ISettingsService.Settings`; the
  Settings window edits the global settings only. `SettingsService.SetActiveProject` swaps the folder and the overrides as one unit and raises
  `SettingsChanged`.
* **Applying a change.** `MainViewModel` reacts to `ProjectChanged` (a project opened or closed, the settings saved) like it does after the
  Settings window: the toolbar definitions, the global macros of the headers folder and every open script are refreshed and parsed again.
* **Detection.** `ProjectLocator` looks for `fss-project.json` in the folder of an opened script and the four folders above it (five levels,
  the nearest wins). When it belongs to a project other than the active one, an *information* problem with a "Switch to this project" link is
  reported (never a dialog), once per project and session.
* **Watching the project file.** `ProjectService` owns a `ProjectFileWatcher` (Core) for the folder of the active project; it is created when
  a project is opened, replaced when the project changes, and disposed when the project is closed or the container is disposed (`IDisposable`).
  A file system event only starts a quiet period (`ProjectFileWatchOptions.Debounce`, 300 ms); a slow safety net compares size and time every
  2 s in case the file system reports nothing (network folders). When the file is examined, a locked file is read again with a bounded
  doubling backoff, a missing file or an invalid content is looked at again a few times (`SettleAttempts`) so that an editor that saves through a
  temporary file and a rename, deletes and recreates the file, or is still writing, is one change and a half-written file is never applied.
  The outcome is a `ProjectFileChange`: *Modified* (valid content: applied through the same path as `Reload()`, which raises
  `ProjectChanged`, so the effective settings, the toolbar definitions, the global macros and every open script follow like after a project
  switch), *Invalid* (the last valid settings stay in effect; `IProjectService.FileChangedOnDisk` carries the parse error with its line and
  column) or *Deleted* (the project becomes a folder without settings). The editor's own saves are recognised by the SHA-256 of the content:
  `SaveSettings` acknowledges the bytes before it writes, `Read` acknowledges what it read, so only a content that differs from what the editor
  holds is a change (no reload loop). The change is posted to the synchronization context the service was created on (the UI thread).
  `MainViewModel` turns `FileChangedOnDisk` into a status text ("Project settings reloaded") or, for an invalid content, a *Warning*
  problem (`ProblemOrigins.ProjectFile`) with the line and column and an "Open the file" link (the editor opens `.json` files as plain text);
  it is cleared by the next valid content or when the project changes. A file saved with a byte order mark (Notepad, PowerShell) or as UTF-16 is read.
* **Which options a project overrides.** `ProjectSettings.GetOverriddenOptions()` / `EffectiveSettings.GetOverriddenOptions()` list the option
  identifiers (`ProjectOptions`, the property names of `ProjectSettings`) that have a value in the file. The Settings window uses them: a
  `SettingCard` carries `OptionId`, the implicit style binds `IsOverridden` to `SettingsViewModel.OverriddenOptions`, and the template shows
  "Overridden by project X" with a link ("Edit project setting" opens the project settings window on that option, `ProjectSettingsViewModel.FocusOptionId`),
  dims the control and sets a tool tip; the global value stays editable and is never replaced by the project value.
* **Open documents per project.** `RecentFiles.ProjectSessions` (in `recent.json`, written with the same atomic and merged saves as the other
  settings) keeps for at most 20 projects (the oldest is dropped) the open documents of the project folder, relative with forward slashes, and
  the active one (`ProjectSession`); the caret line and the folds stay in `positions.json` / `folding.json` and come back when a document is
  opened. `ProjectSessionService` (Workspace) captures the list when the project is left (`ProjectChanged` with another folder, Close Project) and
  when the program ends (`CanCloseAsync`, before the documents are closed); documents outside the project folder and untitled ones are not
  remembered. When a project is opened (menu, recent list, command line folder, or at start-up without files on the command line) and the editor
  option "Restore the open files of a project" (`EditorSettings.RestoreProjectDocuments`, default on) is on, the remembered files that still
  exist are opened in the background at once when nothing is open, with the remembered active one in front; when documents are open already a
  *Information* problem with a "Restore open files" link is reported instead (`ProblemOrigins.ProjectSession`). Missing files are skipped and logged.
  With the option off, or without a project, nothing is offered and nothing changes.
* **UI.** File menu: Open Project, Recent Projects, Create Project Settings, Close Project, Show Project Folder in Explorer; Options menu and
  a click on the project name in the status bar: *Project settings* (`ProjectSettingsWindow` / `ProjectSettingsViewModel`). For each option
  the window offers "Inherit global" / "Override" (`OverrideModeBox`, `ProjectOption<T>`) and the value; the result is saved to
  `fss-project.json`, never to the global files. The project name is shown in the status bar and the window title. The window also has
  "Reload from file" (reads the file again, discarding what was typed and not saved) and "Open fss-project.json" (closes the window and opens the file in the editor).

## Project index and code intelligence

**One index of the whole project.** `IProjectSymbolIndex` (`FalloutScriptStudio.Core.Indexing`, `ProjectSymbolIndex`) holds the procedures, `#define` macros (with their parameters) and script level variables (`variable`, `import variable`, `export variable`) of every `.ssl` and `.h` file below its *roots* (`ProjectIndexRoots`): the project folder, or the folders of the open scripts, plus the headers folder of `ISettingsService.Effective`. It reuses the editor's own scanners (`InternalParser` for procedures, `MacroCollector` for macros) and the token based `SslScriptModel` for variables, keeps only symbols (never the text of a file: about 12 MB for 54 000 symbols in 4 250 files, hard limit 300 000 symbols for the x86 address space) and skips `.int`, binary files and files above 5 MB; files are read with the code page of `Editor.FileEncoding` (BOM, then strict UTF-8, then that code page). A run lists the files, compares size and time stamp with the previous snapshot (a file written less than 2 s before it was read is read again) and reads only what changed, on at most four pool threads, with cancellation between files; the snapshot is replaced as a whole, so a query never sees half an update. `Search` ranks exact > prefix > start of a word > substring > letters in order (`SymbolMatcher`), several words must all match (the next ones may match the file name), `Find` is a dictionary lookup, `FindOccurrencesAsync` scans the files that can *see* a definition (the files that include its file, `GetFilesSeeing`; every file for `import`/`export` names) with the open documents' texts instead of the disk.

**Who keeps it up to date.** `ProjectSymbolService` (`Workspace`) computes the roots, runs the index in the background (debounced) on start, project change, document open / close (only when the roots change), settings change, save / reload (`UpdateFileAsync`) and window activation, and writes "Indexing 1200/5000" / "Indexed N symbols in M files" in the status bar for runs of 10 files and more (it does not use `IOperationTracker`, which would block the build commands). `CodeIntelligenceOperations` implements what the user asks: `NavigateAsync` (definition / declaration / references of a name the document does not resolve, a list `SymbolPickerWindow` in *Choose* mode when there are several candidates, ordered by `SymbolCloseness`), the *Go to Symbol* list (*Search* mode), project wide references, *Delete Unused Procedure*, *Find Unused Variables* and *Extract Procedure*. The editor asks through `IWorkspace` (default interface members, so fakes keep compiling); `NavigationController` keeps the old behaviour whenever the document or its includes resolve the symbol.

**Text refactorings are pure Core.** `SslTokenizer` / `SslScriptModel` (`Core.Analysis`) give tokens (comments and strings skipped, preprocessor lines marked), procedures with bodies and every declared variable; `UnusedVariableFinder` (never used / assigned but never read; macro bodies and the words of included macros count as uses; a declaration is removable when its initialiser calls nothing), `VariableDeclarationEditor`, `SymbolOccurrenceScanner`, `UnusedProcedureRemover` (plan with leading comment block and forward declaration; engine handlers, `*_p_proc` and the names of `prochandlers.ini` are never removed) and `ProcedureExtractor` (checks complete statements, balanced `begin`/`end`, no `return`, `break` / `continue` only for loops inside the block; parameters in order of first use, locals only used inside move, one changed and later read variable is returned) return `TextEdit`s that the workspace applies as one undo group (`TextDocumentEdits.ApplyEdits`). The `ShowUnusedVariables` option (global only) makes `DiagnosticsController` draw `MarkerSeverity.Unused` marks after each parse (about 18 ms on 20 000 lines).
