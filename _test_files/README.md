# Test files

Everything the automated tests (`FalloutScriptStudio.Core.Tests` and `FalloutScriptStudio.Tests`) read or write lives in this folder,
and nowhere else: nothing is created in `%TEMP%`, in the user profile or next to the sources, and nothing outside the repository is
read. The folder is located by walking up from the test output until `FalloutScriptStudio.sln` is found (`TestFiles.cs`, one source file
in `FalloutScriptStudio.Core.Tests\Support` that is linked into both test projects).

| Folder | Content | Used by |
|---|---|---|
| `tools/` | Real sfall tools: `compile.exe` (sslc), `parser.dll` (x86 parser library of the Sfall Script Editor), `int2ssl.exe` (decompiler) and their `readme.md` | `Core.Tests\Native`: `RealCompilerTests` and `RealCompilerWorkspace` (compile.exe, int2ssl.exe), `NativeParserFixture`, `NativeParserIntegrationTests`, `NativeParserSampleTests` and `ParserHostProcessTests` (parser.dll through the real `FalloutScriptStudio.ParserHost.exe`) |
| `samples/FO2tweaksMk2/source/` | The scripts and headers of the author's mod FO2tweaksMk2 (61 files, 467 KB: 27 `.ssl`, their `.tssl` sources, `.ts` and `.h` files) | `NativeParserSampleTests.Parse_Fo2TweaksScripts_...` parses every `gl_g_*.ssl` with `parser.dll`, compiles it with `compile.exe` and compares both verdicts and the reported lines |
| `samples/InventoryFilterMk2/source/` | Optional, **not shipped**: the sources of the author's mod InventoryFilterMk2 (`gl_InvenFilter.ssl`, its modules and `InvenFilter.h`) | `NativeParserSampleTests.Parse_InventoryFilterMainScript_...` (skipped when the folder is absent) |
| `samples/PartyOrdersMk2/source/` | Optional, **not shipped**: the sources of the author's mod PartyOrdersMk2 (`gl_p_party_orders.ssl`; its `headers/rp` are deliberately missing) | `NativeParserSampleTests.Parse_ScriptWhoseHeadersAreMissing_...` (skipped when the folder is absent) |
| `work/ui-smoke/` | The pictures (`.png`), the UI Automation trees and `report.txt` of the real-UI smoke tests (`scripts\Run-UiSmoke.ps1`, `FalloutScriptStudio.UiTests`); also `work/ui-smoke-app-*` (private copies of the program) and `work/ui-smoke-data-*` (its data folders) while they run | `FalloutScriptStudio.UiTests` (only with `FSS_RUN_UI_TESTS=1`); delete `work/ui-smoke` when you have looked at it |
| `work/` | Scratch folders of the running tests, `work/<name>-<guid>`; created on demand, **git-ignored**, deleted by each test when it ends | every test that needs files on disk (`TestFolder`) |

## Rules

* The tests never modify `tools/` or `samples/`: a test that compiles or parses a sample first copies it into `work/`.
* Scratch folders are created only through `TestFolder` / `TestFiles.CreateWorkFolder` and deleted when disposed. At the start of a
  test run `TestFiles` also removes whatever a crashed or killed run left in `work/` (older than one hour), so `work/` is empty (or
  absent) after a normal run.
* A test that needs a real tool or a sample set is reported as **skipped** (not failed) when it is missing: `[RealToolFact]` /
  `[RealToolTheory]`, see `SslcTools.GetSkipReason`. With the files of this folder only the two tests of the optional sample sets
  (InventoryFilterMk2, PartyOrdersMk2) are skipped; copy the sources of those mods into `samples/InventoryFilterMk2/source` and
  `samples/PartyOrdersMk2/source` to run them.
* Environment variables for a developer who keeps the files elsewhere: `FSS_SSLC_DIR` (folder with `compile.exe`, `parser.dll`,
  `int2ssl.exe`; default `tools/`) and `FSS_SSL_SAMPLES` (folder with the sample sets, laid out like `samples/`).
* `.gitattributes` keeps the bytes of these files: the sample sources are CRLF text, the tools are binary.

## Origin and licence

`compile.exe`, `parser.dll` and `int2ssl.exe` come from the sfall project (GPL) and the sources of Sfall Script Editor; the sample
scripts are the author's own mods (FO2tweaksMk2, InventoryFilterMk2, PartyOrdersMk2). The files remain the property of their respective
owners and are not covered by the licence of this project. Remove a file only together with the tests that use it.
