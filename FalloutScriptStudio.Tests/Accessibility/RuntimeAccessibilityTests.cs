namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Windows;
	using FalloutScriptStudio.Tests.ToolWindows;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.Flowchart;
	using FalloutScriptStudio.Views.ScriptTools;
	using FalloutScriptStudio.Views.ScriptTools.Dialogs;
	using FalloutScriptStudio.Views.ToolWindows;
	using Xunit;

	/// <summary>
	/// The accessibility rules checked on the live visual tree of the real windows (shown off-screen, themed, with English texts).
	/// Only the windows that load without the resources of the application are listed here (the others refer to its styles while they load);
	/// the other windows and the panels are covered by the static scan of the markup in <see cref="AccessibilityInventoryTests"/>.
	/// </summary>
	public sealed class RuntimeAccessibilityTests
	{
		public static IEnumerable<Object[]> Windows()
		{
			return new[] { "Settings", "About", "GoToLine", "BatchCompile", "MacroRenamePreview", "NewNode", "Headers", "RegisterScript", "SymbolPicker", "SymbolChoice" }.Select(name => new Object[] { name });
		}

		private static Window Create(String name)
		{
			return name switch
			{
				"Settings" => new SettingsWindow(new SettingsViewModel(new FakeSettingsService(@"C:\fss"), new FakeThemeService(), new FakeDialogService(), new FalloutScriptStudio.Tests.FakeFileAssociationService(), new FakeFontCatalog("Consolas"))),
				"About" => new AboutWindow(new AboutViewModel()),
				"GoToLine" => new GoToLineWindow(new GoToLineViewModel()),
				"BatchCompile" => new BatchCompileWindow(new BatchCompileViewModel(new FakeBatchCompileService())),
				"MacroRenamePreview" => new MacroRenamePreviewWindow(Uninitialized<MacroRenamePreviewViewModel>()),
				"NewNode" => new NewNodeWindow(Uninitialized<NewNodeViewModel>()),
				"Headers" => new HeadersWindow(Uninitialized<HeadersViewModel>(), new FakeSettingsService(@"C:ss")),
				"RegisterScript" => new RegisterScriptWindow(Uninitialized<RegisterScriptViewModel>()),
				"SymbolPicker" => new SymbolPickerWindow(CreateSymbolPicker(FalloutScriptStudio.Services.ToolWindows.SymbolPickerMode.Search)),
				"SymbolChoice" => new SymbolPickerWindow(CreateSymbolPicker(FalloutScriptStudio.Services.ToolWindows.SymbolPickerMode.Choose)),
				_ => throw new ArgumentOutOfRangeException(nameof(name)),
			};
		}

		private static SymbolPickerViewModel CreateSymbolPicker(FalloutScriptStudio.Services.ToolWindows.SymbolPickerMode mode)
		{
			SymbolPickerViewModel viewModel = new SymbolPickerViewModel();
			viewModel.Initialize(new FalloutScriptStudio.Services.ToolWindows.SymbolPickerRequest
			{
				Mode = mode,
				Name = "name",
				Index = new FakeSymbolIndex(),
				Candidates = new[] { new FalloutScriptStudio.Core.Indexing.IndexedSymbol("name", FalloutScriptStudio.Core.Indexing.IndexedSymbolKind.Procedure, @"C:\a.ssl", 1) },
			});
			return viewModel;
		}

		private static T Uninitialized<T>()
			where T : class
		{
			return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
		}

		private static void AssertNone(IReadOnlyList<String> findings)
		{
			Assert.True(findings.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, findings));
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void EveryControlHasTheNameAScreenReaderReads(String name)
		{
			WindowHost.Run(() => Create(name), window => AssertNone(RuntimeInspector.FindMissingNames(window)));
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void EveryControlIsReachableWithTheTabKey(String name)
		{
			WindowHost.Run(() => Create(name), window => AssertNone(RuntimeInspector.FindUnreachable(window)));
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void TheWindowHasACancelButtonAndAnInitialFocus(String name)
		{
			// The scripts list editor handles Escape in code, like the message editor and the search window.
			WindowHost.Run(() => Create(name), window => AssertNone(RuntimeInspector.FindDialogProblems(window, name == "RegisterScript")));
		}
	}
}
