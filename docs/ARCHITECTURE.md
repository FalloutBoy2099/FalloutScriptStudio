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
the error and warning counts and opens the tab. Search and references results keep their own closable tabs and are not problems.

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
* **UI.** File menu: Open Project, Recent Projects, Create Project Settings, Close Project, Show Project Folder in Explorer; Options menu and
  a click on the project name in the status bar: *Project settings* (`ProjectSettingsWindow` / `ProjectSettingsViewModel`). For each option
  the window offers "Inherit global" / "Override" (`OverrideModeBox`, `ProjectOption<T>`) and the value; the result is saved to
  `fss-project.json`, never to the global files. The project name is shown in the status bar and the window title.
