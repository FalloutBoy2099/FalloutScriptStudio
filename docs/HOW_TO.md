# Fallout Script Studio — developer how-to

Step-by-step guides for the changes that touch several files at once. Each guide lists the files to edit (paths are relative to the
repository root) and the tests to update or add. The rules in [CONVENTIONS.md](CONVENTIONS.md) and the layout in
[ARCHITECTURE.md](ARCHITECTURE.md) apply to everything below.

* [0. Common rules](#0-common-rules)
* [1. A menu command with a keyboard shortcut](#1-a-menu-command-with-a-keyboard-shortcut)
* [2. A setting](#2-a-setting)
* [3. A language](#3-a-language)
* [4. A localized string](#4-a-localized-string)
* [5. A Problem producer](#5-a-problem-producer)
* [6. An editor command](#6-an-editor-command)
* [7. A test file under `_test_files`](#7-a-test-file-under-_test_files)
* [8. A parser or compiler option](#8-a-parser-or-compiler-option)
* [9. Publishing](#9-publishing)
* [10. A rule of the semantic diagnostics](#10-a-rule-of-the-semantic-diagnostics)
* [11. A formatting option](#11-a-formatting-option)
* [12. A built-in snippet, a snippet variable or a part of the snippet UI](#12-a-built-in-snippet-a-snippet-variable-or-a-part-of-the-snippet-ui)
* [13. A tab of the right browser, a global variable function or a help topic](#13-a-tab-of-the-right-browser-a-global-variable-function-or-a-help-topic)
* [14. Run the UI smoke tests](#14-run-the-ui-smoke-tests)

The guides refer to each other by these numbers.

## 0. Common rules

* Build and test before you stop (close the running editor first, it locks the output):

  ```
  dotnet build FalloutScriptStudio.sln -c Debug
  dotnet test FalloutScriptStudio.Core.Tests
  dotnet test FalloutScriptStudio.Tests
  ```

  The build must give 0 warnings and 0 errors (`IDE0049` / `IDE0065` are errors).
* C# and XAML files are CRLF, UTF-8 without BOM. After editing a `.xaml` file run `xstyler -f <file>`, remove the BOM it adds and check
  with `xstyler -p -f <file>`. This includes the string dictionaries under `FalloutScriptStudio\Localization\Strings\`.
* There is no constants class for the localization keys: `Str.<Area>.<Key>` is a plain string that is looked up in the dictionaries
  (`Loc.Get`, `Loc.Format`, `{DynamicResource ...}`). A key that does not exist is shown as the key itself.
* Tests that need files use `_test_files` only (guide 7). Shell and editor tests that need WPF run on an STA thread through
  `ShellStaRunner.Run` (`FalloutScriptStudio.Tests\Shell\Support\ShellStaRunner.cs`) or `UiThread.Run`
  (`FalloutScriptStudio.Tests\Editor\Support\UiThread.cs`).

## 1. A menu command with a keyboard shortcut

There are two kinds of commands. A *shell command* lives in `MainViewModel` (open a file, compile, show a window). An *editor text
command* works on the active editor through `IEditorSurface.Execute` (comment, indent, go to declaration); for those, start with
guide 6 and come back for the menu item. This guide is the shell command.

1. **Command.** Add a `[RelayCommand]` method to the `MainViewModel` partial that matches the menu, in
   `FalloutScriptStudio\ViewModels\Main\`: `MainViewModel.File.cs` (file, project, recent), `MainViewModel.Edit.cs` (Edit and Code menus,
   navigation, search), `MainViewModel.Tools.cs` (Script, Headers, Dialog, Compile, Options, Help), `MainViewModel.Projects.cs`,
   `MainViewModel.Problems.cs`, or `MainViewModel.cs` itself (the class is split by menu; if the files changed, put the command next to a
   command of the same menu). `public async Task FooAsync()` generates `FooCommand` (the `Async` suffix is
   dropped). A command that must be disabled while something runs uses `[RelayCommand(CanExecute = nameof(CanBuild))]` like the build
   commands in `MainViewModel.Tools.cs`. A long operation is an `IOperation` (see `BuildCoordinator`, guide 5 for failures).
   If the command needs a new service call, add it to the service interface (`FalloutScriptStudio\Services\ToolWindows\IToolWindowService.cs`,
   `Services\IDialogService.cs`, `Services\IShellService.cs`, `Services\ScriptTools\IScriptToolService.cs`, ...) and to **every fake** in `FalloutScriptStudio.Tests`
   (`Shell\Support\FakeToolWindowService.cs`, `FakeScriptToolService.cs`, `FakeDialogService.cs`, `FakeShellService.cs`, and the
   `FakeDialogService` copies in `ToolWindows`, `ScriptTools`, `ScriptToolsDialogs`, `MessageEditor`, `Flowchart\FakeDialogs.cs`).
2. **Menu item.** In `FalloutScriptStudio\Views\Shell\MainWindow.xaml`, inside `<Menu x:Name="MainMenu">`, add:

   ```xml
   <MenuItem
       Command="{Binding FooCommand}"
       Header="{DynamicResource Str.Shell.Menu.Foo}"
       InputGestureText="Ctrl+Alt+K"
       Style="{StaticResource DocumentMenuItemStyle}" />
   ```

   `DocumentMenuItemStyle` enables the item only when a document is open, `ScriptMenuItemStyle` for a script (`IsScript`),
   `ParsableMenuItemStyle` for a parsable one (`IsParsable`); omit the style for an always-enabled item. A tool tip is
   `ToolTip="{DynamicResource Str.Shell.Tip.Foo}"`. An icon is `<MenuItem.Icon><ui:SymbolIcon Symbol="..." /></MenuItem.Icon>`.
   A toolbar button, if wanted, goes in the `<!-- Toolbar -->` section of the same file.
3. **Gesture.** `InputGestureText` is only a label: WPF does not bind it. Add the real binding to `<Window.InputBindings>` in the same
   file:

   ```xml
   <KeyBinding Key="K" Command="{Binding FooCommand}" Modifiers="Ctrl+Alt" />
   ```

   Write the modifiers in the order Ctrl, Alt, Shift in both places, and several gestures of one item as `"Ctrl+Space / Alt+Space"`
   (`MainWindowGestureTests` compares the normalized text). Special keys are compared as: `OemPlus`/`Add` = `+`, `OemMinus`/`Subtract` = `-`,
   `OemQuestion`/`Divide` = `/`, `OemPeriod` = `.`, `Multiply` = `*`, `Cancel` = `Break`, `D0`..`D9` = the digit. Check by hand that the gesture is not
   already used by an editor command (`FalloutScriptStudio\Editor\EditorCommands.cs`) or by AvalonEdit: no test compares the two.
4. **Localization.** Add `Str.Shell.Menu.Foo` (and `Str.Shell.Tip.Foo`) to `Shell.en.xaml` and the ten other `Shell.<lang>.xaml`
   files (guide 4).
4b. **Command palette.** Add the menu item to `CommandCatalog.BuildDefinitions` (`FalloutScriptStudio\Commands\CommandCatalog.cs`): the menu it is in (`MenuFile`...), the key of its header, the command property (`"FooCommand"`, or `Command(...)` / `Edit(...)` / `Toggle(...)` for a command, an editor command, a check item) and when it is enabled (`CommandRequirement`: the same as its style or `IsEnabled`). A command that has a key binding but no menu item goes to `BuildExtraDefinitions`. `CommandCatalogTests` fail when the table and `MainWindow.xaml` differ, and tell which menu item is missing.
5. **Shortcuts help.** There is no shortcuts window or help page to update. The shortcuts are listed in the menu (`InputGestureText`)
   and, for the problem and cancel keys, in `docs\ARCHITECTURE.md` (`ProblemShortcutTests.The_shortcuts_are_documented` requires
   `Ctrl+F8`, `Ctrl+Shift+F8` and `Ctrl+Break` to stay there). Add a line to `README.md` or `docs\ARCHITECTURE.md` only for a shortcut
   users must know about.

Tests:

* `FalloutScriptStudio.Tests\Shell\MainWindowGestureTests.cs` reads `MainWindow.xaml` and needs no change for a new shortcut:
  `EveryShortcutShownInAMenuIsBound` fails when an `InputGestureText` has no `KeyBinding` (or editor gesture).
  `_legacyShortcuts` in that file lists the shortcuts of the legacy editor; add to it only when restoring a legacy shortcut.
* `FalloutScriptStudio.Tests\Shell\ProblemShortcutTests.cs`: `No_two_key_bindings_of_the_main_window_use_the_same_gesture` fails on a
  duplicate gesture.
* Add a test of the command to the matching `FalloutScriptStudio.Tests\Shell\MainViewModel*Tests.cs`
  (`MainViewModelFileTests`, `MainViewModelEditTests`, `MainViewModelToolsTests`, `MainViewModelProjectTests`) with
  `ShellHarness` (`Shell\Support\ShellHarness.cs`) inside `ShellStaRunner.Run`.
* `FalloutScriptStudio.Tests\Localization\LocalizationTests.cs` checks the new keys (guide 4).

## 2. A setting

First decide where the value lives:

* **A user option** edited in the Settings window: steps 1 to 7.
* **A state the user does not edit there** (window layout, zoom, last folder, search switches): put it in `WindowSettings` or the
  section that already holds similar values, list it in `SettingsDialogMerge.ForeignOptions` (step 4) and skip the window.
  View toggles that are not persisted at all live in `FalloutScriptStudio\Services\EditorViewOptions.cs`.
* **A per-mod override** of a compiler / path option: also do guide 8.

Steps:

1. **Model.** Add a property with a default value and an XML comment to the section class in
   `FalloutScriptStudio.Core\Configuration\`: `PathSettings`, `CompilerSettings`, `EditorSettings`, `MessageSettings`,
   `FlowchartSettings` (or `WindowSettings`). Nothing else is needed for persistence: `SettingsService` serializes the whole
   `AppSettings` with `System.Text.Json` (`SettingsJson`: camelCase, enums as camelCase text, null omitted), so the default is used for a
   file written by an older version. Consequences:
   * Never rename or remove an enum value that was released: an unknown name makes the file count as damaged
     (`SettingsServiceTests.Load_UnknownEnumNamesCountAsCorruption_AndOutOfRangeValuesAreRepaired`).
   * A value with a valid range is repaired in `AppSettings.Normalize()` (run on load) and in the static `SettingsViewModel.Normalize`
     (run when the Settings window is confirmed, `FalloutScriptStudio\ViewModels\ToolWindows\SettingsViewModel.cs`).
   * A mutable collection or other reference type in `PathSettings` or `CompilerSettings` must be copied in their internal `Copy()`
     (`EffectiveSettings` uses it); `AppSettings.Clone()` copies through JSON.
2. **Persistence, defaults, merge.** `FalloutScriptStudio.Core\Configuration\SettingsService.cs` needs no change for a property: it
   saves a file only when this process changed it and merges a file changed on disk meanwhile property by property (three-way JSON
   merge). Only a new *file* (a new model next to `settings.json`, `recent.json`, ...) means editing `AppPaths`, `SerializeModels` and
   `ApplyLoaded` in `SettingsService.cs`; avoid it.
3. **Who reads it.** Read an option that a project may override (compiler flags, headers / output / message folders, definitions)
   from `ISettingsService.Effective.Settings`, everything else from `ISettingsService.Settings`.
   * Core services receive an immutable options record built from the settings (guide 8).
   * The editor receives `EditorOptions` (`FalloutScriptStudio\Editor\EditorOptions.cs`, `EditorOptions.From`); add the field there and
     apply it in the controller that uses it (for example `Editor\Controllers\AppearanceController.cs`).
     `ScriptEditorServices` re-applies the options when `SettingsChanged` is raised.
   * A setting that is also a main-menu toggle: a property on `MainViewModel` that writes through `SetSetting(...)`
     (`MainViewModel.File.cs`; examples in `MainViewModel.Tools.cs` and `MainViewModel.Edit.cs`), and its name added to the array in
     `RefreshSettingsProperties()` in `MainViewModel.cs` so that it follows changes made elsewhere.
4. **Settings window.** The window edits `SettingsViewModel.Draft`, a clone of the settings; nothing changes until OK.
   * Simple option: no view-model code. Add a `SettingCard` to the page in `FalloutScriptStudio\Views\ToolWindows\SettingsWindow.xaml`:

     ```xml
     <local:SettingCard Title="{DynamicResource Str.ToolWindows.Opt.Foo.Title}" Description="{DynamicResource Str.ToolWindows.Opt.Foo.Desc}">
         <ui:ToggleSwitch IsChecked="{Binding Draft.Compiler.Foo}" />
     </local:SettingCard>
     ```

   * Derived state (enabled/disabled, a list of choices, a file picker): add it to the `SettingsViewModel` partial of the page,
     `FalloutScriptStudio\ViewModels\ToolWindows\SettingsViewModel.<Page>.cs` (`Appearance`, `Association`, `Compiler`, `Defines`, `Editor`,
     `Messages`, `Parser`, `Paths`; base class `SettingsViewModel.cs`). `Parser` is the smallest example; a list of choices uses
     `Choice<T>` (`ViewModels\ToolWindows\Choice.cs`) as `OptimizationChoices` does in `SettingsViewModel.Compiler.cs`.
   * A new page: add a value to `SettingsPage` (`ViewModels\ToolWindows\SettingsPage.cs`), a `SettingsPageItem` in the `Pages` list of
     the `SettingsViewModel` constructor, an `Is<Page>Page` property with its `OnPropertyChanged` in `OnSelectedPageItemChanged`, a
     `Str.ToolWindows.Page.<Page>` key (the "open the settings" problem action uses it too) and a `ScrollViewer` page in
     `SettingsWindow.xaml`.
   * `SettingsViewModel.Commit` copies the edited options into the live settings with `SettingsDialogMerge.CopyOwnedOptions`
     (`ViewModels\ToolWindows\SettingsDialogMerge.cs`). It copies **every** public read/write property of `Paths`, `Compiler`,
     `Editor`, `Messages` and `Flowchart` by reflection, so a new option is saved without code. An option that other components
     write while the window is open must be added to `SettingsDialogMerge.ForeignOptions` as `"Section.Property"`.
5. **Project override.** Only for options that differ between mods: guide 8, step "project override". The Settings window edits global
   settings only.
6. **Legacy importer.** `FalloutScriptStudio.Core\Configuration\LegacySettingsImporter.cs` reads the *binary* `settings.dat` of the old
   editor in the exact order the old editor wrote it. A new option does not exist there: **do not touch the importer** (adding a read
   shifts every later value). Edit it only to import a legacy field that is not imported yet, with a test in
   `FalloutScriptStudio.Core.Tests\Configuration\LegacySettingsImporterTests.cs`.
7. **Localization.** `Str.ToolWindows.Opt.<Name>.Title` and `.Desc` in the `ToolWindows` area (11 files, guide 4); `Page` keys for a
   new page.

Tests:

* `FalloutScriptStudio.Core.Tests\Configuration\SettingsServiceTests.cs`: add the default to `Defaults_MatchTheLegacyEditor` and the
  value to `SaveThenLoad_RoundTripsEverything` (both list options by hand).
* `FalloutScriptStudio.Tests\ToolWindows\SettingsViewModelTests.cs`: a test of any derived state you added.
  `MergeCopiesEveryOptionExceptTheOnesTheWindowDoesNotOwn` and `MergeForeignOptionsNameRealProperties` cover the merge. They use
  `FalloutScriptStudio.Tests\ToolWindows\SettingsOptionMutator.cs`, which supports `Boolean`, `Int32`, `String`, enums and
  `List<String>` (and their nullable forms); an option of another type makes it throw `NotSupportedException`, so extend `Change(...)`.
* Range repair: a test of `AppSettings.Normalize()` or `SettingsViewModel.Normalize` (`SettingsViewModelTests.Normalize_RepairsTypedValues`).
* If you changed `ISettingsService`, update `SettingsService` and the three fakes
  `FalloutScriptStudio.Tests\ToolWindows\FakeSettingsService.cs`, `ScriptTools\FakeSettingsService.cs`,
  `MessageEditor\FakeSettingsService.cs`.
* Menu toggle: extend `FalloutScriptStudio.Tests\Shell\MainViewModelToolsTests.cs` (`The_menu_toggles_read_and_write_the_settings`).

## 3. A language

The interface languages are a list, not a configuration: one entry plus the dictionary files.

1. **Register it.** In `FalloutScriptStudio\Localization\Loc.cs` add `new LanguageInfo("xx", "Native name")` to `Loc.Languages`
   (`LanguageInfo` is in `Localization\LanguageInfo.cs`). `xx` is the two-letter ISO 639-1 code; it is also the file suffix and the
   culture name (`CultureInfo.GetCultureInfo`). The order of the list is the order of the language menu, which
   (`Views\Shell\MainWindow.xaml.cs`, `OnLanguageMenuOpened` and `OnLanguageButtonClick`) is built from the list. The choice is stored
   in the shell layout (`ShellLayout.Language`), and `Loc.DetectSystemLanguage` picks it for a system in that language: nothing else to do.
2. **Create the dictionaries.** One file per area in `FalloutScriptStudio\Localization\Strings\`: `Common`, `Shell`, `Panels`, `ToolWindows`,
   `MessageEditor`, `Flowchart`, `ScriptTools`, `Editor` (`Loc.Areas`), named `<Area>.xx.xaml`: 8 new files (11 languages x 8 areas =
   88 files today). Copy `<Area>.en.xaml` and translate the values only:
   * the same keys, no duplicate, no missing key;
   * the same `{0}`, `{1}` placeholders (any order);
   * the same number of line breaks (`&#10;` with `xml:space="preserve"` on the element) and, for `Str.Shell.Filter.*`, the same number
     of `|`.
   A missing key would show the English text at run time, but the tests refuse it.
3. Format the new files with `xstyler`, strip the BOM, keep CRLF (guide 0).
4. The `Messages.Language` setting (`"english"`, `"french"`, ...) is the name of the *game's* message folder, not the interface
   language: do not change it.

Tests: nothing to edit. `FalloutScriptStudio.Tests\Localization\LocalizationTests.cs` runs for every entry of `Loc.Languages` and every
area: `EveryAreaHasAnEnglishFile`, `TranslationDefinesTheSameKeysAsEnglish` (same keys, no duplicates),
`TranslationKeepsTheFormatPlaceholders`; `FalloutScriptStudio.Tests\Shell\ShellLocalizationTests.cs` checks line breaks and filter
parts of the `Shell` area. Run them.

## 4. A localized string

1. **Pick the area and the key.** Area by where the text is shown: `Common` (words shared everywhere), `Shell` (main window, menus,
   status bar, problems, dialogs of the shell), `Panels`, `ToolWindows` (settings and small windows), `MessageEditor`, `Flowchart`,
   `ScriptTools`, `Editor`. Key: `Str.<Area>.<Group>.<Name>`, placed next to the related keys of the file (the files are grouped, not
   sorted).
2. **Add it to all 11 files.** `FalloutScriptStudio\Localization\Strings\<Area>.<lang>.xaml` for `en`, `de`, `es`, `fr`, `it`, `pt`, `ru`,
   `sv`, `pl`, `cs`, `zh`:

   ```xml
   <sys:String x:Key="Str.Shell.Build.Foo">Compiled {0} of {1} script(s).</sys:String>
   ```

   The translations are real translations, not copies of the English text. Escape `&` and `<`; a line break is `&#10;` and the
   element needs `xml:space="preserve"`. Keep the placeholders; use them with `Loc.Format`.

   **Use the glossary.** The English text uses one wording per concept ("message file", not ".msg file"; "dialogue node", not "dialog node";
   "header file"; "Round trip"), and every translation must use the canonical term of [GLOSSARY.md](GLOSSARY.md) for each term it
   translates, with the right inflection (cs/pl/ru cases, de plural, sv definite form). `GlossaryTests` check this for every key and report
   all the findings at once (key, language, expected term); fix the translation. A term that the glossary does not know yet is added to
   `FalloutScriptStudio\Localization\Glossary.json` and the document is regenerated with `FSS_WRITE_GLOSSARY=1 dotnet test FalloutScriptStudio.Tests`.
   Only a text in which the term is legitimately not used (idiom, term not translated) goes into the short allow-list
   `FalloutScriptStudio.Tests\Localization\GlossaryExceptions.json`, with a one-line justification.
3. **Use it.**
   * XAML: `{DynamicResource Str.Shell.Build.Foo}` (follows a language change at once).
   * C#: `Loc.Get(key)` / `Loc.Format(key, arguments)` at the moment the text is shown. Never keep the result in view-model state: keep
     a key and arguments (as `Problem.MessageKey` / `MessageArguments`) or a structured state (`ParserStatus`) and compute the text when
     it is read. A view model that must raise `PropertyChanged` on a language change subscribes with
     `LanguageChangedSubscription.Subscribe(this, static vm => ...)` (`Localization\LanguageChangedSubscription.cs`), never with `+=`.
4. Run `xstyler` on the 11 files (guide 0).

Tests: `FalloutScriptStudio.Tests\Localization\GlossaryTests.cs` (glossary terms, forbidden synonyms, generated `docs\GLOSSARY.md`),
`FalloutScriptStudio.Tests\Localization\LocalizationTests.cs` (same keys in all languages, no duplicates, same placeholders, and
`EveryKeyUsedBySourcesExists`) and, for the `Shell` area, `Shell\ShellLocalizationTests.cs`. Caveat: `EveryKeyUsedBySourcesExists` matches
only the first three segments of a key in the sources (`Str.Shell.Build`), so a misspelled last segment is *not* detected. Check
the final key by hand, or test it as `Tests\Problems\ProblemFactoryTests.cs` does for problem keys (`Loc.TryGet`). A test that changes the
language belongs to the collection `[Collection(LanguageCollection.Name)]` (`Tests\Localization\LanguageCollection.cs`, example
`Shell\ParserStatusLanguageTests.cs`) because the change is announced process-wide.

## 5. A Problem producer

A *problem* is anything that goes wrong and that the user can act on (a missing tool, a file that cannot be saved). It is a `Problem`
shown in the Errors tab; do not report it only with a message box, the status bar or the log.

1. **Model.** `FalloutScriptStudio\Problems\Problem.cs`: severity (`ProblemSeverity` Error / Warning / Info), source (`ProblemSource`
   Parse, Build, Tool, File, Settings, Message), a message key plus arguments (or the `Text` a tool wrote), an optional hint key, an
   optional `ProblemAction` (`ProblemAction.OpenSettings(SettingsPage.X)`, `OpenFolder`, `SwitchProject`, `OpenFile`, `RestoreDocuments`), file / line / column,
   `Owner` (the document: the problem is dropped when it is re-parsed, recompiled, saved or closed) and `Origin`.
2. **Origin.** If the producer must replace or remove its own problems when its situation changes, add a constant to
   `FalloutScriptStudio\Problems\ProblemOrigins.cs` and clear it with `IProblemService.ClearOrigin` (examples: `BuildTool` cleared when a
   build starts, `NativeParser` cleared by `ParseCoordinator` when the native parser works again). Reporting an equal problem again
   (`Problem.Identity`) replaces the first one. `ParseCoordinator` drops the problems of source `Parse` of a document at every parse, so a producer
   that follows the text of a script (`LiveDiagnosticsService`, `DialogueQualityService`) publishes its findings again after a parse of the same text
   (it keeps them with the text version) and lets them go when the text changed.
3. **Factory.** Add a static method to `FalloutScriptStudio\Problems\ProblemFactory.cs` (message and hint keys are
   `Str.Shell.Problem.<Name>` and `Str.Shell.Problem.<Name>Hint`; the private `KeyPrefix` is the prefix). Set `Owner` for a document
   problem, `Origin` when it has one, and an `Action` when there is an obvious fix.
4. **Keys.** The two keys in the `Shell` area, 11 files (guide 4). A `ProblemAction.OpenSettings` label needs no new key unless you added
   a settings page (`Str.ToolWindows.Page.<Page>`).
5. **Report it.** A service takes `IProblemService? problems = null` as its **last** constructor parameter and falls back to
   `new ProblemService()` (see `Workspace\ArchiveScriptExtractor.cs`, `DocumentFileService.cs`, `ParseCoordinator.cs`), then calls
   `Report`, `ClearOwner`, `ClearOrigin` or `Remove`. The container supplies the singleton
   (`services.AddSingleton<IProblemService, ProblemService>()` in the container registration, `FalloutScriptStudio\Startup\AppServices.cs`). Call it on the UI thread. The failure
   of a background operation started by the shell goes through `IWorkspaceManager.GuardAsync`; a long operation is an `IOperation` of
   `IOperationTracker` ended in a `finally`/`using` (`Workspace\BuildCoordinator.cs`).
6. **Display.** Nothing to do: `OutputPanelViewModel` lists every reported problem, grouped by source. A new `ProblemSource` value
   is a bigger change (`GroupName` and the filter toggles in `FalloutScriptStudio\ViewModels\Main\OutputPanelViewModel.cs`, a
   `Str.Shell.Output.Group*` key); prefer an existing source.

Tests:

* `FalloutScriptStudio.Tests\Problems\ProblemFactoryTests.cs`: add the new factory call to `AllProblems()`. The theories then check
  that the message and hint keys exist, no `{n}` is left unformatted, every placeholder has an argument and the action has a label.
  Every problem listed there needs a hint.
* A scenario test in `FalloutScriptStudio.Tests\Shell\ProblemProducerTests.cs` (the producer reports it, the owner / origin rules clear
  it) with `ShellHarness` (its `Problems` property) inside `ShellStaRunner.Run`.
* `Problems\ProblemServiceTests.cs` for the service itself, `Shell\OutputPanelProblemsTests.cs` for what the Errors tab shows.

## 6. An editor command

### A text or navigation command (`EditorCommand`)

1. **Enum.** Add a member, with a comment, to `FalloutScriptStudio\Documents\EditorCommand.cs`.
2. **Logic.** Pure text transformations belong in `FalloutScriptStudio.Core\TextTools\` (`CommentTools`, `WhitespaceTools`, `CaseTools`,
   `NumberBaseConverter`, `CodeFormatter`, ...) and are tested in `FalloutScriptStudio.Core.Tests\TextTools\`. Applying edits to the
   AvalonEdit document goes through `FalloutScriptStudio\Editor\Text\DocumentEdits.cs`. Behaviour that needs the editor state is a method
   of the controller that owns it (`FalloutScriptStudio\Editor\Controllers\`: `FoldingController`, `NavigationController`,
   `BracketController`, `OccurrenceController`, `CompletionController`, ...).
3. **Handler.** `FalloutScriptStudio\Editor\EditorCommandHandler.cs`: add a `case` to `CanExecute` (the `default` returns `false`, so an
   unlisted command is disabled) and to `Execute` (it runs as one undoable operation; use `Edit(...)`/`Replace(...)`).
4. **Routed command and gesture.** `FalloutScriptStudio\Editor\EditorCommands.cs`: add an entry to the `_byCommand` dictionary:

   ```csharp
   [EditorCommand.Foo] = Create("Str.Editor.Cmd.Foo", nameof(EditorCommand.Foo), new KeyGesture(Key.K, ModifierKeys.Control | ModifierKeys.Alt)),
   ```

   `ScriptEditorView.RegisterCommandBindings` (`FalloutScriptStudio\Views\Editor\ScriptEditorView.xaml.cs`) binds every entry of
   `EditorCommands.All` to `IEditorSurface.Execute`/`CanExecute` itself, so the gesture works when the editor has the focus. Optional: a
   context-menu entry in `BuildContextMenu` of that file (`CreateItem(EditorCommand.Foo, SymbolRegular...)`).
5. **Main menu.** `FalloutScriptStudio\Views\Shell\MainWindow.xaml`:

   ```xml
   <MenuItem
       Command="{Binding EditCommand}"
       CommandParameter="{x:Static docs:EditorCommand.Foo}"
       Header="{DynamicResource Str.Shell.Menu.Foo}"
       InputGestureText="Ctrl+Alt+K"
       Style="{StaticResource ParsableMenuItemStyle}" />
   ```

   The gesture needs no `KeyBinding` in the window: `MainWindowGestureTests` accepts the gestures of `EditorCommands.All`.
6. **Keys.** `Str.Editor.Cmd.Foo` (`Editor` area, the command text and the context-menu header) and `Str.Shell.Menu.Foo` (`Shell` area).
7. If you changed `IEditorSurface` (`FalloutScriptStudio\Documents\IEditorSurface.cs`), update `FalloutScriptStudio.Tests\Shell\Support\FakeEditorSurface.cs`
   (the fake implementation). A new `EditorCommand` alone does not need it.

### A new feature controller

A class in `FalloutScriptStudio\Editor\Controllers\` that implements `IDisposable` with an `Attach()` (subscribe to the text area /
document, add renderers) and a `Dispose()` that undoes everything (events, `InputBindings`, renderers). Create it in the constructor
of `FalloutScriptStudio\Editor\EditorSession.cs`, call `Attach()` in the start sequence and `Dispose()` in the stop sequence of the same
file. A controller that exists per text control must also be created in `EditorSession.ShowSecondary` (split view) and added to
`EditorPane.cs` and to the disposal of the second pane. Services come from `EditorContext` / `ScriptEditorServices`
(`FalloutScriptStudio\Editor\ScriptEditorServices.cs`; registered by `AddScriptEditor` in `Editor\EditorServiceCollectionExtensions.cs`).
Keys typed in the text area: a `PreviewKeyDown` handler (`InputController`, `CompletionController`) or a `KeyBinding` removed in `Dispose`.

### Tests (`FalloutScriptStudio.Tests\Editor`)

* `EditorKeyActionsTests.cs`: add an `InlineData` to `Commands_HaveTheLegacyGestures` for the gesture, and a behaviour test on
  `UiThread.Run` + `EditorTestEnvironment` (`Editor\Support\EditorTestEnvironment.cs`: `CreateDocument`, `StartSession`).
* `ScriptEditorViewTests.cs` (the routed command through the view), `EditorSessionTests.cs` / `EditorBehaviourTests.cs` (controllers
  and typing), `SplitViewTests.cs` (second pane).
* `FalloutScriptStudio.Tests\Shell\MainViewModelEditTests.cs` with `FakeEditorSurface` (the menu reaches the surface).
* `FalloutScriptStudio.Tests\Shell\MainWindowGestureTests.cs` runs without change.
* The pure logic in `FalloutScriptStudio.Core.Tests\TextTools\`.

## 7. A test file under `_test_files`

Everything a test reads or writes lives in `_test_files` at the repository root; read `_test_files\README.md` first. Never use
`Path.GetTempPath()`, the registry or a real user folder.

* **A file the test creates** (the usual case). Use a scratch folder, never a path of your own:

  ```csharp
  using TestFolder folder = new TestFolder("parsing");          // _test_files\work\parsing-<guid>, deleted on Dispose
  String script = folder.Write(@"scripts\a.ssl", "procedure start begin\r\nend\r\n");
  ```

  `TestFolder` and `TestFiles` (`CreateWorkFolder`, `DeleteWorkFolder`, `Root`, `Tools`, `Samples`, `Work`, `RepositoryRoot`) are in
  `FalloutScriptStudio.Core.Tests\Support\TestFolder.cs` and `TestFiles.cs`, namespace `FalloutScriptStudio.TestSupport`. Each area has
  a thin subclass: `Core.Tests\Parsing\TempDirectory.cs`, `Core.Tests\Functions\TempFolder.cs`, `Tests\Shell\Support\TempFolder.cs`,
  `Tests\ScriptTools\ScriptToolsTempDirectory.cs`; `Core.Tests\Compilation\TempWorkspace.cs` wraps a `TestFolder`. Reuse one or add a
  four-line subclass (`: base("name")`). `_test_files\work\` is git-ignored; a crashed run is swept at the next start (folders older than one hour).
* **A file that is checked in** (a fixture). Put it under `_test_files\samples\<Set>\source\` (SSL sources: CRLF text, kept byte for byte
  by `.gitattributes`) or `_test_files\tools\` (real tools: binary, `-text`). A new top-level folder under `_test_files` needs a rule in
  `.gitattributes`. Add a row to the table of `_test_files\README.md` (folder, content, used by). `tools\` and `samples\` are
  **read-only**: a test that compiles or parses a sample copies it into a scratch folder first
  (`NativeParserFixture.CopyDirectory` in `Core.Tests\Native\NativeParserFixture.cs` shows how).
* **A test that needs the real tools or an optional sample set.** Use `[RealToolFact(RealTools.Compiler)]` / `[RealToolTheory(...)]`
  (`FalloutScriptStudio.Core.Tests\Native\`); the flags of `RealTools` combine. A test is then *skipped*, not failed, when the file is
  absent. For a new optional set add a flag to `RealTools.cs`, a path and a `Has...` property to `SslcTools.cs`, and a branch in
  `SslcTools.GetSkipReason`. `FSS_SSLC_DIR` and `FSS_SSL_SAMPLES` override the tools and samples folders.
* **Linking.** `FalloutScriptStudio.Tests` includes `..\FalloutScriptStudio.Core.Tests\Support\*.cs` (see its `.csproj`), so any new
  `.cs` file you add to `FalloutScriptStudio.Core.Tests\Support\` is automatically compiled into both test projects (keep it `internal`,
  namespace `FalloutScriptStudio.TestSupport`, and free of anything the other project cannot reference). A support class used by one
  project only goes next to its tests.
* **Reading a file of the repository itself** (a `.xaml`, `docs\`, `Content\`): walk up from `AppContext.BaseDirectory` to
  `FalloutScriptStudio.sln`, or use `TestFiles.RepositoryRoot` / `RepositoryFiles.Root`
  (`Core.Tests\Functions\RepositoryFiles.cs`); examples: `LocalizationTests`, `MainWindowGestureTests`.

## 8. A parser or compiler option

The chain for an option, traced on `ShortCircuit` (grep it to see every hop):

1. **Setting.** A property on `CompilerSettings` (or `PathSettings`) in `FalloutScriptStudio.Core\Configuration\` (guide 2).
2. **Immutable options.** A property on the options record that Core receives; Core never reads `AppSettings`:
   `FalloutScriptStudio.Core\Compilation\CompilerOptions.cs` (compiler and decompiler), `FalloutScriptStudio.Core\Parsing\ParserOptions.cs`
   (parsers), `FalloutScriptStudio.Core\Compilation\BatchCompileOptions.cs`, `FalloutScriptStudio.Core\Messages\MessageFileOptions.cs`.
3. **Mapping.**
   * Compiler: `AppSettingsProjections.ToCompilerOptions` (`FalloutScriptStudio.Core\Configuration\AppSettingsProjections.cs`); the same
     file maps `ToBatchCompileOptions`, `ToMessageFileOptions` and `ToIncludeResolver`.
   * Parser: `FalloutScriptStudio\Workspace\ParserOptionsFactory.cs`. It reads `settings.Effective` and is evaluated for every parse
     (`ScriptParser` takes a `Func<ParserOptions>`, registered by `AddScriptParsing` in `Startup\AppServices.cs`), so no restart is needed;
     `MainViewModel.AfterSettingsChangedAsync` parses the open scripts again after the settings or the project changed.
   * An option the *native* parser needs also crosses the host process: `FalloutScriptStudio.Core\Parsing\Interop\Host\HostRequest.cs`,
     `HostMapper.cs`, `NativeParser.cs` and a `HostProtocol.Version` bump. Avoid it unless required.
4. **Use.** The consumer reads the record: `CompilerCommandLineBuilder` (flags of sslc and of the batch files), `ScriptCompiler`,
   `BatchCompileService` (builds one `CompilationRequest` per script), the decompilers. Callers obtain the options from
   `ISettingsService.Effective`, never from `ISettingsService.Settings`:

   ```csharp
   CompilerOptions options = this._settings.Effective.ToCompilerOptions(this._settings.Paths);
   ```

5. **Global UI.** Guide 2 (Compiler page: `SettingsViewModel.Compiler.cs` and `SettingsWindow.xaml`).
6. **Project override** (only for options that differ between mods; the editor, message, flowchart and window sections are never
   overridden). The value is read from `fss-project.json`, `null` means "inherit":
   1. `FalloutScriptStudio.Core\Configuration\ProjectSettings.cs`: a nullable property (`Boolean?`, an enum `?`, or a `String` where an
      empty text means "not set"); also `CreateFrom` (copy the value in effect). A list or a path needs `Clone`, `Normalize` and
      `ConvertPaths` as well (paths are stored relative to the project folder).
   2. `FalloutScriptStudio.Core\Configuration\EffectiveSettings.cs`, `Combine`: `compiler.Foo = project.Foo ?? compiler.Foo;` (for text,
      `Pick(project.Foo, global.Foo)`). Only `Paths` and `Compiler` are combined.
   3. `FalloutScriptStudio\ViewModels\ToolWindows\ProjectSettingsViewModel.cs`: a `ProjectOption<T>` property
      (`FlagOption`/`TextOption`/`new ProjectOption<T>(...)`), its initialization in `Load()` and `StructValue(...)`/`TextValue(...)` in
      `ToSettings()`.
   4. `FalloutScriptStudio\Views\ToolWindows\ProjectSettingsWindow.xaml`: a `SettingCard` with `local:OverrideModeBox`
      (`SelectedIndex="{Binding Foo.ModeIndex}"`) and the editing control bound to `Foo.Value` / enabled by `Foo.IsOverridden`; reuse the
      `Str.ToolWindows.Opt.Foo.Title/Desc` keys of the global page. Give the card `OptionId="Foo"`: the window scrolls to it when the link
      "Edit project setting" of the Settings window points at it.
   5. `FalloutScriptStudio.Core\Configuration\ProjectOptions.cs` and `ProjectSettings.GetOverriddenOptions()`: add the identifier
      (the property name) and the line that tells the option has a value. In `FalloutScriptStudio\Views\ToolWindows\SettingsWindow.xaml` put
      the same `OptionId="Foo"` on the card of the global option: the card then shows "Overridden by project X", dims its control and links to the
      project settings by itself (`SettingsViewModel.ProjectOverrides.cs`). An option without a global control only needs the first part.
   6. `ProjectSettingsFile` (read / write, unknown properties kept) needs no change; the file is watched and reloaded by itself
      (`ProjectFileWatcher`), a new property needs nothing for that.
   7. An option that is also part of a compile profile (optimization, short-circuit, backward compatibility, debug information, the
      definition): add it to `CompileProfile` (`ApplyTo`, `Normalize` when it needs repair), to `CompileProfileItem` and
      `CompileProfilesEditor.xaml`, and, if it changes what the compiler produces, to `BuildFingerprint.Create` (otherwise the scripts built
      with another value stay "up to date"; `StalenessMatrixTests.Option_changes_make_the_script_stale` lists the options that count).
7. **Interfaces.** A new member of `ISettingsService` also goes to `SettingsService` and the three test fakes (guide 2).

Tests:

* `FalloutScriptStudio.Core.Tests\Compilation\CompilerCommandLineBuilderTests.cs` (the flag), `Configuration\SettingsModelsTests.cs`
  (`Projection_BuildsCompilerOptionsFromSettingsAndPaths`).
* `FalloutScriptStudio.Core.Tests\Configuration\EffectiveSettingsTests.cs` (`EveryOverrideWins`,
  `CompilerOptions_FollowTheProject_AndFallBackToTheGlobalValues`, `CreateFrom_CopiesTheValuesInEffect`,
  `GetOverriddenOptions_ListsEveryOptionWithAValueInTheProject`: add the option to its list), `ProjectSettingsFileTests.cs`
  (file round trip), `ProjectServiceTests.cs`, `ProjectFileWatchTests.cs` (the watching, with the real file system).
* `FalloutScriptStudio.Tests\ToolWindows\ProjectOverrideMarkupTests.cs` (`Every_option_that_has_a_global_control_is_marked_on_its_card_in_the_settings_window`
  lists the options that have a global control: add yours) and `SettingsViewModelProjectOverrideTests.cs`.
* `FalloutScriptStudio.Tests\ToolWindows\ProjectSettingsViewModelTests.cs` (inherit, override, save only the overridden options) and
  `Shell\MainViewModelProjectTests.cs` (`Effective` follows the project).
* Parser option: `FalloutScriptStudio.Tests\Shell\IncludeSearchWorkspaceTests.cs` and `MainViewModelProjectTests.cs` call
  `ParserOptionsFactory.Create`; the host protocol is covered by `FalloutScriptStudio.Core.Tests\Native\HostProtocolTests.cs` and
  `OutOfProcessNativeParserTests.cs`.
* A flag that changes what the real compiler produces: `FalloutScriptStudio.Core.Tests\Native\RealCompilerTests.cs` with
  `[RealToolFact(RealTools.Compiler)]` (example `Compile_ShortCircuitOption_ChangesTheGeneratedCode`).

## 9. Publishing

`Publish.ps1` (repository root) builds the distributable folder `publish\` (git-ignored). Prerequisite: the .NET 10 SDK. Run
`.\Publish.ps1` (`-Configuration Release|Debug`, default Release; `-Runtime win-x86`, the only value, because `parser.dll` is 32-bit).

What it does, in order:

1. `dotnet publish FalloutScriptStudio\FalloutScriptStudio.csproj` twice into `publish.tmp\`: **framework-dependent** single file
   (`FalloutScriptStudio.exe`, needs the .NET 10 Desktop Runtime x86) and **self-contained** single file with compression
   (renamed `FalloutScriptStudio-NET.exe`, about 70 MB). Both are built under the assembly name `FalloutScriptStudio.exe`.
2. `dotnet publish FalloutScriptStudio.ParserHost\FalloutScriptStudio.ParserHost.csproj` once, self-contained, single file, trimmed
   (`PublishTrimmed`, `TrimMode=partial`), so the same file works for both executables.
3. Only when both succeeded, `publish\` is deleted and rebuilt: the two executables, `FalloutScriptStudio.ParserHost.exe`, and the data
   folders `descriptions` and `resources` copied from the first build (the `$dataFolders` list).
4. A `finally` block removes `publish.tmp` and the `bin\` / `obj\` folders of the configuration for the three projects, because the
   self-contained trimmed builds would otherwise poison the next ordinary build. The next `dotnet build` recreates them.

Shipped in `publish\`: `FalloutScriptStudio.exe`, `FalloutScriptStudio-NET.exe`, `FalloutScriptStudio.ParserHost.exe`,
`descriptions\` (`functions.ini`, `opcodes.txt`, `prochandlers.ini`, `userfunctions.ini`) and `resources\` (`mcpp.bat`, `wcc.bat`,
`usercomp.bat`, `icon_*.ico`, `templates\`). These two folders come from `FalloutScriptStudio\Content\` (`<Content Include="Content\**\*"
CopyToOutputDirectory="PreserveNewest" ...>` in `FalloutScriptStudio\FalloutScriptStudio.csproj`).

Shipped in `resources\`: `parser.dll` (sfall native parser). Not shipped (the user supplies them in `resources\`, all optional at run time): `compile.exe` (sslc, needed to compile),
`mcpp.exe`, `wcc386.exe`, `int2ssl.exe`, `int2ssl_v35.exe`, optional `syntax\*.xshd`. Also not shipped: `docs\`, `_test_files\`
(including its copies of the tools), the test projects, and `settings\` / `scrTemp\`, which are created at run time.

How the parser host gets next to the executables:

* **Development builds.** `ParserHost.targets` (repository root) is imported by `FalloutScriptStudio\FalloutScriptStudio.csproj` and by
  `FalloutScriptStudio.Core.Tests\FalloutScriptStudio.Core.Tests.csproj`. It adds a `ProjectReference` with `ReferenceOutputAssembly="false"`
  (build order only: the host is started as a process, never loaded) and a `CopyParserHost` target (after `Build`) that copies the host's
  output into `$(OutDir)$(ParserHostDestination)`. The application leaves `ParserHostDestination` empty (next to the exe); the Core tests
  set it to `ParserHost\` (`SslcTools.HostExe` looks there).
* **Published builds.** The application's own `dotnet publish` does not carry the host; step 2 of `Publish.ps1` produces it and moves it
  to `publish\`. At run time `NativeParserHostOptions.ForDirectory(paths.ProgramDirectory)` (`Startup\AppServices.cs`) looks for
  `FalloutScriptStudio.ParserHost.exe` next to the executable.

To change what is shipped:

* **A new data file.** Put it under `FalloutScriptStudio\Content\descriptions\` or `Content\resources\`; the `Content` glob picks it up and
  both folders are copied to `publish\`. A new top-level data folder must also be added to `$dataFolders` in `Publish.ps1`.
  A shipped batch file must stay CRLF (`FalloutScriptStudio.Core.Tests\Compilation\ShippedBatchFilesTests.cs`, `.gitattributes`).
* **A new helper executable** started as a process: copy the pattern of the host (a project referenced through `ParserHost.targets`-style
  `ProjectReference`, a publish step in `Publish.ps1`, a `Move-Item` into `publish\`, and a start path relative to `ProgramDirectory`).
* **Update the documents:** the runtime layout in `docs\ARCHITECTURE.md` and the `Publish.ps1` paragraph of `README.md`.

Verify a publish: run `.\Publish.ps1`, check the three `.exe` files and the two folders in `publish\`, then start each editor
executable and open a script (the parser status in the status bar says "Built-in parser" only if `resources\parser.dll` was removed). Guide 14 does this off-screen:
`PublishedExecutableTests` of the UI smoke tests starts both executables of `publish\`.

## 10. A rule of the semantic diagnostics

`ScriptDiagnostics` (`FalloutScriptStudio.Core\Analysis`) finds the mistakes that `sslc` only reports at compile time (see
[ARCHITECTURE.md](ARCHITECTURE.md), "Semantic diagnostics"). A new rule must be right about the real compiler: it reports a script
**exactly when `compile.exe` rejects it for that reason**, and nothing it reports as an error may compile.

1. **Find out what the compiler does.** Compile small scripts with `_test_files\tools\compile.exe` (`compile.exe -q -l -O1 -p t.ssl -o t.int`) and note
   which are accepted and which rejected, and the message. Do not trust the documentation or the grammar you remember: the scope of a
   local is the procedure (not the block), a `procedure x;` of a standard handler fails even when unused...
2. **Code.** Add the rule to `ScriptDiagnosticCodes` (`SSL1xxx` for what sslc enforces, `SSL2xxx` for a comparison with `opcodes.txt` /
   `functions.ini`, which is never more than a warning) and to its `All` list; report it from the pass that has the facts
   (`ScriptAnalyzer.Outline.cs` for declarations and structure, `.Bodies.cs` / `.Names.cs` for names and calls, `.Finish.cs` for what needs the
   whole outline). Whatever the analysis cannot be sure of must make it quiet, not wrong: use `IsUnreliable` (headers not read, macros that
   declare), `_structuralMacroUsed`, the condition frames (declarations in different branches of an `#if` are not duplicates) and
   `softenInConditional`. An optional `ScriptQuickFix` carries the id, the argument and the place; the edits are built from the **current** text
   in `ScriptQuickFixes.CreateEdits` (add the id there and a label in `ScriptFixText.Describe`).
3. **Texts.** `Str.Editor.Diag.<Name>` (the message, arguments are the strings the analysis passes) and `Str.Editor.Diag.<Name>Hint`, in the
   11 `Editor.<lang>.xaml` files (guide 4); the glossary applies. `ScriptDiagnosticProblemTests` checks the keys of every rule in every language.
4. **Tests.** Add cases to `FalloutScriptStudio.Core.Tests\Support\ScriptDiagnosticCases.cs`: one script the compiler rejects (`Rejected("SSLxxxx", ...)`)
   and, for each way the rule could be wrong, one it accepts (`Accepted(...)`). `ScriptDiagnosticTableTests` checks the analysis with CRLF and
   LF without the compiler, `RealScriptDiagnosticsTests*` compile every case with the real `compile.exe` and compare the verdict with the table
   (skipped when the tool is absent). Rules of the engine functions are checked against the compiler by `RealFunctionCatalogTests`. Run the
   analysis over the sample scripts (`RealFunctionCatalogTests.No_script_of_the_FO2tweaksMk2_sample...`) and the generated 20 000 line
   script (`DiagnosticsPerformanceTests`): neither may get an error.

## 11. A formatting option

The formatter (`FalloutScriptStudio.Core\Formatting`, see [ARCHITECTURE.md](ARCHITECTURE.md), "Code formatting") must keep three promises: the **default options give the
text the original formatter gave**, **only white space changes**, and **formatting twice is formatting once**.

1. **Model.** Add the property to `FormatOptions` (with its default = what the original formatter did, its range in `Normalize()` and, if it needs the structure
   of the script, in `IsStructural`), to `FormatSettings` (property, `CopyFrom`, `Normalize`, `ToOptions`) and to the copy in `SettingsOptionMutator`
   (`FalloutScriptStudio.Tests\ToolWindows`) so that the "every option is copied" tests see it. The project file needs nothing else: `ProjectSettings.Formatting`
   is the whole style.
2. **Pass.** Put the rule in the pass that owns it (`SpacingPass`, `PlacementPass`, `IndentPass`, `WrapPass`, `BlankLinePass`...) or add a pass to `FormatPipeline` in
   the position that keeps the result stable. Work on the tokens of `FormatLexer`/`FormatLine` so that strings, comments and the preprocessor lines that must stay
   verbatim are never touched; keep the line breaks of the text.
3. **Tests** (`FalloutScriptStudio.Core.Tests\Formatting`): a before / after case in `FormatOptionTests` for every value of the option, an option set in
   `FormatSamples.Sets` (the stability tests, the generated scripts of `FormatPerformanceTests` and the compiler tests `FormatCompilerTests` run on every set: the
   compiled sample must not change). Run them all; fix the pass, never the test oracle.
4. **Settings window.** A row in `FormattingOptionsEditor.xaml` (a `SettingCard` with a toggle, a combo of `Choice<T>` or a number box), the property on
   `FormattingEditorViewModel` (it writes the style and refreshes the preview), the texts `Str.ToolWindows.Fmt.<Name>.Title` / `.Desc` (and `Str.ToolWindows.Fmt.Choice.*`
   for the values) in the 11 files of `ToolWindows` (guide 4, glossary applies), then XamlStyler (guide 0). The project settings window shows the same user
   control, so it needs nothing more.

## 12. A built-in snippet, a snippet variable or a part of the snippet UI

* **A built-in snippet.** Add it to `BuiltInSnippets` (trigger, a key for the description, the body) and the description `Str.Editor.Snippet.<Key>` to the 11 `Editor`
  string files. The trigger must not be a word of the block completion (`SnippetValidator.IsReserved`; a test checks all of them). Check the body with
  `SnippetTemplate.Parse(body).Problems` (a test does it for every built-in snippet).
* **A variable.** Add the name to `SnippetVariables.All`, its value to `SnippetContext` (a property the editor fills in `SnippetController.CreateContext`) and its
  expansion to `SnippetExpander`; test it in `SnippetExpanderTests` (a body with the variable and without a value, with CRLF and LF) and document it in the help text
  `Str.ToolWindows.Snippets.SyntaxHelp` of every language. A variable name the parser does not know is reported by the manager as a mistake of the body.
* **Editor behaviour.** Test it with the invisible harness (`EditorSnippetTests`: `EditorTestEnvironment`, `HiddenEditorPopups`; the user's snippets are written with
  `env.Services.Snippets.TrySave`, the project's by opening a project folder of `_test_files\work`; the answer of the Insert Snippet list is
  `env.Recorder.SnippetAnswer`). Keys are sent as routed events (`HwndSource` without a window) or with `MoveToNextStop`; never open a real popup.
* **The manager and the list.** View-model tests use `FakeSnippetService` (in memory) and `FakeDialogService`; a window is only built by the accessibility tests
  (`RuntimeAccessibilityTests`) and never shown.

## 13. A tab of the right browser, a global variable function or a help topic

* **A tab.** The tabs of `RightBrowserView.xaml` are selected by index (`MainViewModel.RightTabIndex`, saved in the layout): add the new tab at the end
  (`GlobalsTabIndex = 4`, `HelpTabIndex = 5` in `MainViewModel.Globals.cs`), a view model registered as a singleton in `AppServices`, an optional parameter at the end of
  the `MainViewModel` constructor (the tests keep compiling), a property on the view model (`Globals`, `Help`) that the tab binds its `DataContext` to, a `Show...`
  command that sets `ShowRightBrowser` and `RightTabIndex`, the View menu item and its `CommandCatalog` line (guide 1), and the tab texts `Str.Panels.Tab<Name>` /
  `Str.Panels.TabTip<Name>`. `ShellHarness` builds the panels with fakes; `GlobalsAndHelpAccessibilityTests` checks the markup (named tree, filter box and buttons, no
  color in the markup, no font below 11). A tree that can hold tens of thousands of rows creates the children of a node when it is expanded
  (`GlobalVariableNodeViewModel`) and turns on `VirtualizingStackPanel.IsVirtualizing`.
* **A function that reads or writes a global variable.** Add it to `GlobalFunctionTable` (its name as `opcodes.txt` writes it, the family, the access, the argument that
  holds the value written); the first argument is the variable. A test checks that the name is in the description files. A function that only passes hook arguments
  does not belong there.
* **A finding about global variables.** A value of `GlobalFindingKind`, the rule in `GlobalVariableAnalyzer` (pure; test it in
  `FalloutScriptStudio.Core.Tests\Globals\GlobalVariableAnalyzerTests`), the `Str.Shell.Problem.Global<Name>` message and hint in the 11 `Shell` files (guide 5; the hint
  may use the arguments of the message), the line in `ProblemFactory.GlobalVariable` and in the `AllProblems()` of `ProblemFactoryTests`.
* **A help topic.** The topics come from `opcodes.txt` and the functions tree (`FunctionHelpCatalog`), from the project (`ProjectHelp`). The page of the sfall
  documentation of a category is a line of `SfallDocumentation._categoryPages`; the test `Every_page_the_catalog_chooses_is_a_page_of_the_site` lists the pages of the
  site, so a page name is checked there. The popup and the Help tab share `HelpTopicView`; the editor tests use `EditorTestEnvironment(withHelp: true)` (an
  `opcodes.txt` in the scratch folder, a `FakeShellService` that records the opened addresses) and `HiddenEditorPopups`, so no window is ever opened.

## 14. Run the UI smoke tests

The unit tests (`FalloutScriptStudio.Tests`) never show a real window, so they cannot see what only the real program shows: tab headers that wrap, a text that
is cut, an illegible color, a window that stays empty, an exception in `sse.log`. The project `FalloutScriptStudio.UiTests` starts the real
`FalloutScriptStudio.exe` (a private copy of the build output, `_test_files\work\ui-smoke-app-<guid>`), drives it through UI Automation, captures every window
with `PrintWindow` and checks: the window opens with its title, the picture is not blank, no text sticks out of its control, no UI Automation name is a type name or a
record text, no exception in the log, the program exits with code 0. It uses the **UI test mode** (`FSS_UI_TEST=1`, `docs\ARCHITECTURE.md`, "UI smoke tests and the UI
test mode"): every window of the program sits far outside of the monitors, is never activated and has no taskbar button, so **you can keep working while it runs**.

1. **Run it.** `.\scripts\Run-UiSmoke.ps1` (`-Configuration Release`, `-Filter "FullyQualifiedName~DialogueTests"` for one class, `-NoBuild`, `-Keep` to keep the data
   folders, `-OutputDirectory`). It sets `FSS_RUN_UI_TESTS=1`, builds the program and the tests and runs `dotnet test FalloutScriptStudio.UiTests`; the whole run takes a
   few minutes. Without `FSS_RUN_UI_TESTS=1` (a plain `dotnet test FalloutScriptStudio.sln`) every smoke test is reported as **skipped** and nothing is started.
2. **Look at the result.** `_test_files\work\ui-smoke\report.txt` (one line per step, the captured windows, the findings), `<test>\<capture>.png` (open them: the
   assertions catch a blank or clipped window, only your eyes catch a wrapped header or a pale text) and `<test>\<capture>.txt` (the UI Automation tree of the window).
   The folder is git-ignored; delete it when you are done (`_test_files\work` should be empty after a run).
3. **The published executables.** `PublishedExecutableTests` starts both `publish\*.exe` (copied to `_test_files\work`), opens a script and exits. Run
   `.\Publish.ps1` first: the test is skipped when `publish\` is older than the sources, because an old build does not know the test mode and would show its windows.
4. **A new scenario.** Copy a class of `FalloutScriptStudio.UiTests\Smoke` (`[Collection(UiSmokeCollection.Name)]`, `[UiSmokeFact]`): `AppSession.Start(...)` with an
   `AppLaunch` (arguments, language, theme, a `settings.json`, `WithoutParser`), `SmokeSteps.Run("what", () => ...)` for each step (a failing step is retried once, the
   windows left open are closed), `app.InvokeMenu("Code", "Format Project")`, `app.OpenNewWindow(...)`, `app.Shot(window, "name")`, `app.AssertNothingClipped(window)`,
   `UiChecks.AssertReadableNames(...)`, `app.Close()` and `app.AssertCleanLog()`. The files of a scenario are created by `Fixtures` under `_test_files\work` (a copy of the
   sample mod, a small dialogue mod with its `.msg` and `scripts.lst`). Rules: poll with `Uia.WaitUntil` / `Uia.Wait`, never sleep for a state; drive with UI Automation
   patterns (`Invoke`, `Select`, `ExpandCollapse`, `Value`), never with `SendInput`, `SetForegroundWindow` or the mouse; type into the editor with `app.TypeInEditor` /
   `app.PressInEditor` (the program has no keyboard focus); name menu items in English (`LocalizedStrings` translates them for another language).
5. **A defect found on screen.** Fix it, then add a test to the normal invisible suites where it is possible (a markup test on the XAML, `RuntimeAccessibilityTests`,
   `AccessibilityBehaviorTests`, a view model test; windows are shown only through `Support\OffscreenWindow`, see `NoVisibleUiGuardTests`). The smoke suite is the net, not the
   regression test.
6. **What cannot be driven, on purpose.** The native file and folder dialogs (not WPF windows: they cannot be kept off-screen, so the tests never open them), the code
   completion list (it stalls the UI for about 10 seconds when its window is off-screen: the scenarios that type turn `editor.autocomplete` off), tool tips and hover (layered
   popups; a captured popup has a black background), key chords with modifiers and the mouse (the keyboard is simulated for text, Return, Tab and the arrows only).
   The Find window fills its text from the clipboard of the developer.