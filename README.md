# Fallout Script Studio (by Kain)

SSL script editor for Fallout 1/2 (sfall) — a complete rewrite in **.NET 10 / WPF / Fluent UI (WPF-UI)** of the legacy *Sfall Script Editor* (WinForms).

## Features

- AvalonEdit editor: SSL/MSG syntax highlighting (light / dark / system themes), procedure folding, auto-completion, tooltips (opcodes, macros, procedures, messages), indentation, block completion (Tab), comments, formatting, rename, references, Back/Forward history.
- Script analysis: native sfall `parser.dll` shipped in `resources\` (exact procedures, variables, references and includes), with the built-in C# parser as a fallback.
- Compilation / preprocessing / round trip / mass compilation (through `compile.exe`; mcpp or wcc optional). Every long operation shows what it does in the status bar and can be cancelled (button, Esc in the log panel, Ctrl+Break): the tool processes are killed and their files cleaned up.
- One list of problems (Errors tab): compiler and parser messages, missing tools, files that cannot be opened or saved, damaged settings... grouped and filterable, with a fix hint and a link to the right settings page; Ctrl+F8 / Ctrl+Shift+F8 go to the next / previous problem and the status bar counts them. With nothing to report the tab shows a friendly "No problems" text instead of an empty grid, and its columns keep a readable minimum width.
- **No external executable is needed to read `.dat` archives (Fallout 1 & 2) or to decompile `.int` scripts**: the DAT reader and the decompiler are written in C#. An external `int2ssl.exe` can still be plugged in (Options → Compiler → Decompiler).
- `.msg` file editor, registration in `scripts.lst`, advanced search in files.
- Dialogue node flowchart (`.fcd` compatible), node code editor, dialogue preview / test, dialogue function rules.
- Function tree (`functions.ini` + user functions), project explorer, import of the legacy settings (`settings.dat`).
- **Projects**: a mod folder can hold a `fss-project.json` that overrides, for that mod only, the headers / output / message folders, `scripts.lst`, the preprocessor and its definitions and the compiler flags (each option either inherits the global setting or overrides it; paths are stored relative to the project). File > Open Project / Recent Projects / Close Project, a hint when an opened script belongs to another project, the project name in the status bar and the window title. `fss-project.json` is **watched**: a hand edit (or a save by another editor, a `git pull`) is reloaded at once without restarting, with a notice in the status bar; an invalid edit keeps the last valid settings and is reported as a warning with the line and column and a link to the file; deleting the file reverts to a folder without settings. The Settings window marks every option the open project overrides ("Overridden by project X", the global control dimmed, a link "Edit project setting" that opens the project settings on that option), the project settings window can reload the file or open it in the editor, and the documents that were open in a project are remembered and opened again with it (option "Restore the open files of a project", a hint with a "Restore open files" link when other documents are open).

- **Project-wide navigation**: F12 (definition), Ctrl+F12 (declaration) and Alt+F12 (references) also work across the whole project. A symbol that is not defined in the script or in its includes (a procedure of another script, a macro of a header the script does not include, an `import`ed procedure or variable) is looked up in a background **project index** (the project folder, or the folders of the open scripts, plus the headers folder; incremental, rebuilt on save and on project change, progress "Indexing 1200/5000" in the status bar). When a name is defined several times a small list offers the places, the nearest first (the current file, the files it includes, the same folder). **Ctrl+T (Go to Symbol)** lists the procedures, macros and variables of the whole project with their kind, file and line; type to filter (prefix, then start of a word, then any part of the name), arrow keys and Enter open the symbol.
- **More refactorings** (Code menu, context menus of the editor and of the procedures / variables panels): *Delete Unused Procedure* (previews the procedure with its comment and forward declaration, refuses when the project still uses it or when the game calls it), *Find Unused Variables* (locals, arguments and script variables that are never used or never read, listed as information problems with a *Remove the declaration* fix; optional faint dotted mark in the editor with *Settings > Editor > Mark unused variables*) and *Extract Procedure* (Ctrl+Shift+E: selected complete statements become a procedure placed before the current one, the variables they use become parameters, a single changed variable is returned). Each one is a single undo step.

## Structure

| Folder | Role |
|---|---|
| `FalloutScriptStudio` | WPF application (x86) |
| `FalloutScriptStudio.ParserHost` | Small x86 console process that runs the native `parser.dll` (a crash of the DLL only kills that request, not the editor) |
| `FalloutScriptStudio.Core` | Business logic without UI (parsing, compilation, DAT, decompilation, messages, dialogues, refactoring, settings) |
| `FalloutScriptStudio.Tests`, `FalloutScriptStudio.Core.Tests` | xunit tests |
| `docs` | `CONVENTIONS.md` (code style enforced by `.editorconfig`), `ARCHITECTURE.md`, `HOW_TO.md` (developer how-to), `GLOSSARY.md` (the common terms of the 11 languages) |

**Developer how-to:** [docs/HOW_TO.md](docs/HOW_TO.md) lists, step by step, the files and tests to touch to add a menu command with a shortcut, a setting, a language or a localized string, a problem producer, an editor command, a test file, a parser/compiler option, and how the release is published.

**Translators and authors of UI texts:** [docs/GLOSSARY.md](docs/GLOSSARY.md) lists the canonical translation of the ~60 domain terms (procedure, node, header, compile, flowchart...) in the 11 languages; a test fails when a translation does not use them.

## Build

```
dotnet build FalloutScriptStudio.sln
dotnet test FalloutScriptStudio.Core.Tests
dotnet test FalloutScriptStudio.Tests
```

The application is built for **x86** (the native `parser.dll` is 32-bit). The application never loads `parser.dll` itself:
`FalloutScriptStudio.ParserHost.exe` (next to the executable, copied by the build and by `Publish.ps1`) hosts it in a separate process,
which is restarted automatically after a crash; after 3 crashes in a row the native parser is turned off for the session and the built-in
C# parser takes over.

`Publish.ps1` produces in `publish\`: `FalloutScriptStudio.exe` (needs the .NET 10 x86 runtime), `FalloutScriptStudio-NET.exe` (runtime included) and
`FalloutScriptStudio.ParserHost.exe` (self-contained, shared by both), plus `descriptions\` and `resources\`.

### Integration tests with the real tools

The tests in `FalloutScriptStudio.Core.Tests\Native` run the real `parser.dll`, `compile.exe` and `int2ssl.exe` (copies in
`_test_files\tools`, overridable with `FSS_SSLC_DIR`) on real scripts (`_test_files\samples`, overridable with `FSS_SSL_SAMPLES`).
Every file the tests use or create stays in `_test_files` (see its `README.md`), never in `%TEMP%`; `_test_files\work` is the git-ignored scratch folder.
When the tools or the optional sample sets are missing the tests are **skipped**, not failed.

## License and credits

Distributed under the **GNU GPL v3** license (see `LICENSE`). Rewrite of the original *Sfall Script Editor* (same license);
`parser.dll` (shipped in `resources\`) and the optional `compile.exe` / `int2ssl.exe` tools come from the sfall project and keep their own licenses.

User data: next to the executable (portable mode); if that folder is not writable (for example `Program Files`), in `%LOCALAPPDATA%\FalloutScriptStudio`.
