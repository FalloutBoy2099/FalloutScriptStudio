# Fallout Script Studio — code conventions

Project: **FalloutScriptStudio** (by Kain) — rewrite in .NET 10 / WPF / Fluent UI
(WPF-UI) of the legacy "Sfall Script Editor" (WinForms, .NET Framework 4.8).

These rules are **mandatory**: the `.editorconfig` at the root is enforced at build time
(`EnforceCodeStyleInBuild`) and `IDE0049` / `IDE0065` violations fail the compilation.

## 1. C# style (.editorconfig)

* Indentation with **tabs** (4), **CRLF** line endings, UTF-8 **without BOM**.
* **Block-scoped namespace**: `namespace X { ... }` — never the file-scoped `namespace X;`.
* **`using` directives go INSIDE the namespace**, never before it.
* **CLR types** rather than C# keywords for the predefined types, everywhere (locals, parameters, members, static access):
  `Int32`, `Int64`, `UInt16`, `Byte`, `SByte`, `Boolean`, `String`, `Char`, `Double`, `Single`, `Decimal`, `Object`.
  So `String.IsNullOrEmpty(x)`, `Int32.TryParse(...)`, `String.Empty`, `Boolean flag`, `List<String>`, `String[]`.
  Technical exceptions: `void` (no alternative); `var` is allowed (not governed by the editorconfig) but
  prefer the explicit type when it is not obvious.
* Names of types, methods, properties, variables, comments, log messages and UI texts: **in English**
  (translate the Russian of the legacy code).
* One public class per file, file name = type name.
* **XAML is formatted with XamlStyler** with its default settings (4 spaces, one attribute per line, no `Settings.XamlStyler` file), as in the other projects of the author. Install it once with `dotnet tool install --global XamlStyler.Console`, then run `xstyler -f <file>` (or `xstyler -d <folder> -r`) before committing; `xstyler -p -f <file>` only checks. XamlStyler writes a UTF-8 BOM: remove it, XAML files are UTF-8 without BOM like the rest.

Compliant example:

```csharp
namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;

	public sealed class Example
	{
		private readonly List<String> _names = new List<String>();

		public Boolean Contains(String name)
		{
			return !String.IsNullOrEmpty(name) && this._names.Contains(name);
		}
	}
}
```

## 2. Quality / "improve the code"

* `Nullable` enabled: annotate correctly (`String?`), no gratuitous `!`.
* **No mutable static state** (the legacy code had `Settings.*`, static buffers, `TextEditor.parserIsRunning`…).
  Everything goes through dependency injection (constructor); services are registered in `Microsoft.Extensions.DependencyInjection`.
* `Core` references **neither WPF nor WinForms** and **never** shows a MessageBox: it returns results / throws exceptions / notifies through events or `ILogger`.
* `Core` does not read a configuration singleton: it receives immutable *options* (records) built by the application from `AppSettings`.
* An option that a project may override (compiler flags, headers / output / message folders, definitions) is read through `ISettingsService.Effective`, never from `ISettingsService.Settings` (the global values), so that the active project's `fss-project.json` is honoured everywhere.
* Long operations (compilation, external parsing, I/O): `async`/`await` + `CancellationToken`, never `Application.DoEvents()`, never a polling `Thread.Sleep`.
* Prefer `record`, `readonly`, `init`, pattern matching, collection expressions, `switch` expressions, readable LINQ, explicit `StringComparison.Ordinal(IgnoreCase)`.
* No dead code, no decorative `#region`, XML `///` comments in English on the public API.
* A type that owns an `IDisposable` field (for example the `SemaphoreSlim` of a service) implements `IDisposable` (CA1001); singletons created by the container are released when the `ServiceProvider` is disposed (`App.OnExit`).
* Logging uses source-generated `[LoggerMessage]` partial methods (CA1848), one internal static `<Area>Log` class per area (`CompilationLog`, `ConfigurationLog`, `WorkspaceLog`...) with its own event id range; never `logger.LogError("...")` directly. Analyzer debt is checked with `dotnet build -p:AnalysisMode=Recommended --no-incremental`; the few justified suppressions are in `GlobalSuppressions.cs` of each project, those of the tests in their `.editorconfig`.
* Failures are reported as `Problem`s (`ProblemFactory`, `Str.Shell.Problem.*` message + hint keys, an action when there is an obvious fix), not only as a message box, the status bar or the log; every long operation is an `IOperation` with a `CancellationToken` that reaches the tool processes and ends in a `finally`.
* UI texts: **use the terms of the glossary** ([GLOSSARY.md](GLOSSARY.md), data in `FalloutScriptStudio\Localization\Glossary.json`): the same domain term is translated the same way in every window of a language, and the English texts use one wording per concept ("message file", "dialogue node", "header file"). `GlossaryTests` fail when a translated text does not contain the canonical term of an English term of the same key, or uses a forbidden synonym; fix the translation, do not extend the allow-list `GlossaryExceptions.json` (a few justified entries at most).
* UI texts: never store an already translated text in the state of a view model (it would not follow a language change); store a structured state (such as `ParserStatus`) or a key + arguments, and compute the text through `Loc` when it is read.
* Subscribing to `Loc.LanguageChanged` (static event): only through `LanguageChangedSubscription` (weak reference, automatic unsubscription), never with a direct `+=`.
* WPF: **MVVM** with `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), code-behind reduced to the strict minimum (interaction with AvalonEdit / Canvas).
* Visual resources: colors and brushes through the WPF-UI dynamic resources (`{DynamicResource ...}`) so that they follow the light/dark theme.

## 3. Encodings (a .NET Core pitfall)

`Encoding.Default` is **UTF-8** in .NET Core; the legacy code read scripts in the Windows ANSI code page.
Use `FalloutScriptStudio.Core.IO.TextEncodings` (`Ansi` = system ANSI code page from `GetACP`, not the regional format of the user; `Oem` = `GetOEMCP`, what
cmd.exe batch files print in; `Oem866`, `Utf8`) — never `Encoding.Default`.

## 4. Native

`parser.dll` (x86, from the sfall project) is shipped in `FalloutScriptStudio\Content\resources`; `compile.exe`, `mcpp` and `wcc` are **not** in the repository (distributed separately in `resources\`).
The application is therefore built for **x86**. The code must degrade gracefully (built-in parser) when a tool is missing, including `parser.dll` if the user removed it.
`.dat` archives and the `.int` → `.ssl` decompilation are pure C# (`Core.Archives`, `Core.Decompilation`); `int2ssl.exe` is only an
**optional** external decompiler (`Decompiler` setting).

## 5. Structure

```
FalloutScriptStudio.sln
FalloutScriptStudio.Core   (net10.0)          business logic without UI
FalloutScriptStudio        (net10.0-windows)  WPF application
```

Namespaces: `FalloutScriptStudio.Core.<Domain>` and `FalloutScriptStudio.<Folder>` (`Views`, `ViewModels`, `Controls`, `Services`, `Editor`, ...).

## 6. Accessibility and keyboard (every window, panel and dialog)

`AccessibilityInventoryTests` (static scan of the XAML), `RuntimeAccessibilityTests` (live visual tree), `AccessKeyTests` and `ShortcutDocumentTests` fail when new XAML breaks one of these rules.

* **Name every control that has no text of its own.** An icon-only `ui:Button` / `ToggleButton` gets `AutomationProperties.Name` (the same `{DynamicResource Str.*}` as its `ToolTip`; the shell button styles `ShellIconButtonStyle`, `ShellIconToggleStyle`, `ShellCounterToggleStyle` already copy the tool tip into the name). A tool tip alone is not a name. Text boxes, combo boxes, lists, trees, grids, progress bars and code editors get `AutomationProperties.Name` or `LabeledBy`; a control placed in a `local:SettingCard` is labelled by the title of the card (and gets the description as help text); a code editor uses `a11y:EditorAutomation.NameResource="Str.*"` (the name must reach its text area). A grid column that shows an icon has a header with an `AutomationProperties.Name`. A button whose text is a symbol or an abbreviation ("Aa", ".*") needs a name too.
* **Names are localized**: `{DynamicResource Str.<Area>.<Key>}` (or a binding to a view model text that is computed through `Loc`), with the key in all 11 languages.
* **Dialogs**: `IsDefault` on the main action (Enter), `IsCancel` on Cancel/Close (Esc); a tool window without a Cancel button closes on Esc with `a11y:WindowKeys.CloseOnEscape="True"` (or handles `Key.Escape` itself). The main field has the initial focus (`FocusManager.FocusedElement="{Binding ElementName=...}"` on the window, or `Focus()` in `Loaded`). The Tab order is the reading order of the markup (do not use positive `TabIndex` except to fix a real problem).
* **Access keys**: dialog buttons and the top level menus have an underscore in their text (`_Save`, `Fi_le`; Chinese: `保存(_S)`) that is unique inside the window or menu, in every language. Never put an underscore in a text that other code shows as plain text (`TextBlock`, message boxes, `Loc.Get`): add a separate `...Btn...` key for the button. The menu bar must not take the letters of the always-on Alt shortcuts (E, R, L, P, M).
* **Keyboard**: anything that works with the mouse works with the keyboard. A click on a text is a `ui:Button` (status bar). A tree has a `KeyDown` handler (Enter = double click, F2 = rename, Delete = delete, Menu key / Shift+F10 = context menu); a grid or list that opens its items on double click handles Enter. No interactive control is `Focusable="False"` or `IsTabStop="False"`; the focus rectangle is never removed (`FocusVisualStyle="{x:Null}"` is rejected).
* **Live regions**: a text that changes by itself (status bar, search status, toast) is bound twice, to `Text` and to `a11y:LiveRegion.Text`, so that a screen reader announces it.
* **Colors**: never in the markup. Text, background, border and fill use the WPF-UI theme brushes (`TextFillColor*Brush`, `CardBackgroundFillColorDefaultBrush`, `SystemFillColor*Brush`, ...) with `DynamicResource`; the information carried by a color is also carried by an icon, a symbol or a text (error / warning icons, counts). Minimum font size: 11.
* **Scale**: every window sets `UseLayoutRounding="True"` and `SnapsToDevicePixels="True"`; a window higher than 480 units sets `a11y:WindowFit.FitToWorkArea="True"` so that it fits a small screen at 150% or 200% scale; long forms scroll (`ScrollViewer`) and texts wrap or trim, they are never clipped.
* **Shortcuts**: every `KeyBinding` and every `EditorCommands` gesture is listed in `docs\SHORTCUTS.md`, which is generated (run the tests with `FSS_WRITE_SHORTCUTS=1`); a menu item that shows an `InputGestureText` shows a bound gesture. The keys that controls handle in code (trees, grids) are written in the hand-written part of `docs\SHORTCUTS.md`.
