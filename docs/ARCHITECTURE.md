# Fallout Script Studio — architecture

Rewrite of the legacy *Sfall Script Editor* (`_reference/`, WinForms / .NET Framework 4.8 / ICSharpCode.TextEditor) as a
.NET 10 WPF application with the Fluent look of **WPF-UI** and the **AvalonEdit** text editor.

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
                compile.exe (sslc), parser.dll, mcpp.exe, wcc386.exe              (NOT shipped: user supplies them, all optional)
                int2ssl.exe, int2ssl_v35.exe                                       (optional: only for the "External int2ssl.exe" decompiler)
                syntax\Ssl.xshd, syntax\Msg.xshd                                   (optional: replace the built-in syntax highlighting definitions, same format as Assets/Syntax)
settings\       settings.json, recent.json, ... (created at runtime)
scrTemp\        scratch folder, wiped at start-up
```

No external tool is needed to **read `.dat` archives** (`FalloutScriptStudio.Core.Archives`: Fallout 1 and Fallout 2 formats, or a folder
standing in for an archive) or to **decompile `.int` scripts** (`FalloutScriptStudio.Core.Decompilation.BuiltInScriptDecompiler`, pure C#).
`undat`/`dat2` are gone. `int2ssl.exe` can still be plugged in: Settings > Compiler > Decompiler > "External int2ssl.exe" with an optional
path (`CompilerSettings.Decompiler` / `ExternalDecompilerPath`; `int2ssl_v35.exe` is the fallback, or the only tool with "Use the old decompiler").
Compilation still needs the user-supplied `compile.exe` (sslc); the preprocessors `mcpp`/`wcc` and `parser.dll` stay optional.

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

`FalloutScriptStudio.Core.Tests/Native` runs the real `parser.dll` (through the real host), `compile.exe` and `int2ssl.exe`. They look in
`%FSS_SSLC_DIR%` or `D:\Git\Fallout\FO2tweaksMk2\tools\sslc-sfall5`; real mod sources are taken from `%FSS_SSL_SAMPLES%` (default
`D:\Git\Fallout`). When something is missing the tests are reported as **skipped** (`[RealToolFact]` / `[RealToolTheory]`), not failed.
The protocol, the crash breaker, timeouts and cancellation are also tested with simulated hosts (`FakeParserHost`).

## Composition

* `App.xaml.cs` is the composition root (`Microsoft.Extensions.DependencyInjection`). No service locator in views / view models.
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

