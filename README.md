# Fallout Script Studio (by Kain)

SSL script editor for Fallout 1/2 (sfall) — a complete rewrite in **.NET 10 / WPF / Fluent UI (WPF-UI)** of the legacy *Sfall Script Editor* (WinForms).

## Features

- AvalonEdit editor: SSL/MSG syntax highlighting (light / dark / system themes), procedure folding, auto-completion, tooltips (opcodes, macros, procedures, messages), indentation, block completion (Tab), comments, formatting, rename, references, Back/Forward history.
- Script analysis: built-in C# parser (procedures, macros, includes); optional native `parser.dll`.
- Compilation / preprocessing / round trip / mass compilation (through `compile.exe`; mcpp or wcc optional).
- **No external executable is needed to read `.dat` archives (Fallout 1 & 2) or to decompile `.int` scripts**: the DAT reader and the decompiler are written in C#. An external `int2ssl.exe` can still be plugged in (Options → Compiler → Decompiler).
- `.msg` file editor, registration in `scripts.lst`, advanced search in files.
- Dialogue node flowchart (`.fcd` compatible), node code editor, dialogue preview / test, dialogue function rules.
- Function tree (`functions.ini` + user functions), project explorer, import of the legacy settings (`settings.dat`).

## Structure

| Folder | Role |
|---|---|
| `FalloutScriptStudio` | WPF application (x86) |
| `FalloutScriptStudio.ParserHost` | Small x86 console process that runs the native `parser.dll` (a crash of the DLL only kills that request, not the editor) |
| `FalloutScriptStudio.Core` | Business logic without UI (parsing, compilation, DAT, decompilation, messages, dialogues, refactoring, settings) |
| `FalloutScriptStudio.Tests`, `FalloutScriptStudio.Core.Tests` | xunit tests |
| `docs` | `CONVENTIONS.md` (code style enforced by `.editorconfig`), `ARCHITECTURE.md` |

## Build

```
dotnet build FalloutScriptStudio.sln
dotnet test FalloutScriptStudio.Core.Tests
dotnet test FalloutScriptStudio.Tests
```

The application is built for **x86** (the optional native `parser.dll` is 32-bit). The application never loads `parser.dll` itself:
`FalloutScriptStudio.ParserHost.exe` (next to the executable, copied by the build and by `Publish.ps1`) hosts it in a separate process,
which is restarted automatically after a crash; after 3 crashes in a row the native parser is turned off for the session and the built-in
C# parser takes over.

`Publish.ps1` produces in `publish\`: `FalloutScriptStudio.exe` (needs the .NET 10 x86 runtime), `FalloutScriptStudio-NET.exe` (runtime included) and
`FalloutScriptStudio.ParserHost.exe` (self-contained, shared by both), plus `descriptions\` and `resources\`.

### Integration tests with the real tools

The tests in `FalloutScriptStudio.Core.Tests\Native` run the real `parser.dll`, `compile.exe` and `int2ssl.exe` (folder
`FSS_SSLC_DIR`, by default `tools\sslc-sfall5`; mod sources in `FSS_SSL_SAMPLES`).
When the tools are missing the tests are **skipped**, not failed.

## License and credits

Distributed under the **GNU GPL v3** license (see `LICENSE`). Rewrite of the original *Sfall Script Editor* (same license);
the optional `compile.exe` / `parser.dll` / `int2ssl.exe` tools come from the sfall project and keep their own licenses.

User data: next to the executable (portable mode); if that folder is not writable (for example `Program Files`), in `%LOCALAPPDATA%\FalloutScriptStudio`.
