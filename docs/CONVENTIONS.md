# Fallout Script Studio — code conventions

Project: **FalloutScriptStudio** (by Kain) — rewrite in .NET 10 / WPF / Fluent UI
(WPF-UI) of the legacy "Sfall Script Editor" (WinForms, .NET Framework 4.8) kept in `_reference/`.

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
* Long operations (compilation, external parsing, I/O): `async`/`await` + `CancellationToken`, never `Application.DoEvents()`, never a polling `Thread.Sleep`.
* Prefer `record`, `readonly`, `init`, pattern matching, collection expressions, `switch` expressions, readable LINQ, explicit `StringComparison.Ordinal(IgnoreCase)`.
* No dead code, no decorative `#region`, XML `///` comments in English on the public API.
* A type that owns an `IDisposable` field (for example the `SemaphoreSlim` of a service) implements `IDisposable` (CA1001); singletons created by the container are released when the `ServiceProvider` is disposed (`App.OnExit`).
* UI texts: never store an already translated text in the state of a view model (it would not follow a language change); store a structured state (such as `ParserStatus`) or a key + arguments, and compute the text through `Loc` when it is read.
* Subscribing to `Loc.LanguageChanged` (static event): only through `LanguageChangedSubscription` (weak reference, automatic unsubscription), never with a direct `+=`.
* WPF: **MVVM** with `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), code-behind reduced to the strict minimum (interaction with AvalonEdit / Canvas).
* Visual resources: colors and brushes through the WPF-UI dynamic resources (`{DynamicResource ...}`) so that they follow the light/dark theme.

## 3. Encodings (a .NET Core pitfall)

`Encoding.Default` is **UTF-8** in .NET Core; the legacy code read scripts in the Windows ANSI code page.
Use `FalloutScriptStudio.Core.IO.TextEncodings` (`Ansi`, `Oem866`, `Utf8`) — never `Encoding.Default`.

## 4. Native

`parser.dll` (x86) and `compile.exe`, `mcpp`, `wcc` are **not** in the repository (distributed separately in `resources\`).
The application is therefore built for **x86**. The code must degrade gracefully (built-in parser) when they are missing.
`.dat` archives and the `.int` → `.ssl` decompilation are pure C# (`Core.Archives`, `Core.Decompilation`); `int2ssl.exe` is only an
**optional** external decompiler (`Decompiler` setting).

## 5. Structure

```
FalloutScriptStudio.sln
FalloutScriptStudio.Core   (net10.0)          business logic without UI
FalloutScriptStudio        (net10.0-windows)  WPF application
```

Namespaces: `FalloutScriptStudio.Core.<Domain>` and `FalloutScriptStudio.<Folder>` (`Views`, `ViewModels`, `Controls`, `Services`, `Editor`, ...).
