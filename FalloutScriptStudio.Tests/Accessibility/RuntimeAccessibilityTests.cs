namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Windows;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.ToolWindows;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.Flowchart;
	using FalloutScriptStudio.Views.ScriptTools;
	using FalloutScriptStudio.Views.ScriptTools.Dialogs;
	using FalloutScriptStudio.Views.ToolWindows;
	using Wpf.Ui.Appearance;
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
			return new[] { "Settings", "About", "GoToLine", "BatchCompile", "MacroRenamePreview", "NewNode", "Headers", "RegisterScript", "SymbolPicker", "SymbolChoice", "CommandPalette", "ReplacePreview", "FormatProject", "SnippetPicker", "SnippetManager" }.Select(name => new Object[] { name });
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
				"CommandPalette" => new CommandPaletteWindow(CreateCommandPalette()),
				"ReplacePreview" => new ReplacePreviewWindow(CreateReplacePreview()),
				"FormatProject" => new FormatProjectWindow(CreateFormatProject()),
				"SnippetPicker" => new SnippetPickerWindow(CreateSnippetPicker()),
				"SnippetManager" => new SnippetManagerWindow(CreateSnippetManager()),
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

		private static FormatProjectViewModel CreateFormatProject()
		{
			return new FormatProjectViewModel(
				System.Reflection.DispatchProxy.Create<FalloutScriptStudio.Workspace.Formatting.IProjectFormatService, NullProxy>(),
				System.Reflection.DispatchProxy.Create<FalloutScriptStudio.Workspace.IOperationTracker, NullProxy>(),
				new FakeSettingsService(@"C:ss"),
				System.Reflection.DispatchProxy.Create<FalloutScriptStudio.Documents.IWorkspace, NullProxy>());
		}

		private static SnippetPickerViewModel CreateSnippetPicker()
		{
			SnippetPickerViewModel viewModel = new SnippetPickerViewModel();
			viewModel.Initialize(new FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest(FalloutScriptStudio.Core.Snippets.BuiltInSnippets.All, false));
			return viewModel;
		}

		private static SnippetManagerViewModel CreateSnippetManager()
		{
			return new SnippetManagerViewModel(new FalloutScriptStudio.Tests.ToolWindows.FakeSnippetService(), new FakeDialogService());
		}

		private static CommandPaletteViewModel CreateCommandPalette()
		{
			CommandPaletteViewModel viewModel = new CommandPaletteViewModel();
			viewModel.Initialize(new FalloutScriptStudio.Services.ToolWindows.CommandPaletteRequest
			{
				Entries = new[]
				{
					new FalloutScriptStudio.Commands.CommandEntry(
						"SaveCommand",
						new[] { FalloutScriptStudio.Commands.CommandText.FromKey("Str.Shell.MenuBar.File") },
						FalloutScriptStudio.Commands.CommandText.FromKey("Str.Common.Save"),
						new CommunityToolkit.Mvvm.Input.RelayCommand(() => { }),
						null,
						() => FalloutScriptStudio.Commands.CommandAvailability.NeedsDocument),
				},
				RecentIds = new[] { "SaveCommand" },
			});
			return viewModel;
		}

		private static ReplacePreviewViewModel CreateReplacePreview()
		{
			ReplacePreviewViewModel viewModel = new ReplacePreviewViewModel();
			ReplaceMatch match = new ReplaceMatch(0, 3, 5, 40, 3, "old", "new", "x = ", ";");
			ReplacePlan plan = new ReplacePlan(
				new[] { new ReplaceFilePlan(@"C:\mods\a.ssl", new Byte[32], new[] { match }, null) },
				new[] { new ReplaceSkip(@"C:\mods\ro.ssl", ReplaceSkipReason.ReadOnly, 2) },
				2,
				true,
				System.Text.Encoding.UTF8);
			SearchMatcher.TryCreate(new SearchOptions("old", true, false, false), out SearchMatcher? matcher, out _);
			viewModel.Initialize(plan, new ReplaceInFilesRequest { Matcher = matcher!, Replacement = "new" });
			return viewModel;
		}

		/// <summary>A stand-in for an interface whose members are never called by the window.</summary>
		private class NullProxy : System.Reflection.DispatchProxy
		{
			protected override Object? Invoke(System.Reflection.MethodInfo? targetMethod, Object?[]? args)
			{
				return null;
			}
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
		[InlineData("SymbolChoice")]
		[InlineData("SnippetPicker")]
		[InlineData("SnippetManager")]
		public void TheSecondaryTextOfTheSelectedRowIsReadableOnTheAccentColor(String name)
		{
			// The muted text of a row (the location, the kind, the description) keeps its gray when the row is selected unless it switches to
			// the text color of the accent: 1.8 to 1 on the blue of the selection of the light theme.
			WindowHost.Run(
				() => Create(name),
				window =>
				{
					System.Windows.Controls.ListBox list = RuntimeInspector.Descendants(window).OfType<System.Windows.Controls.ListBox>().First();
					list.SelectedIndex = 0;
					WindowHost.Pump();
					DependencyObject item = list.ItemContainerGenerator.ContainerFromIndex(0);
					System.Windows.Media.Color onAccent = ((System.Windows.Media.SolidColorBrush)window.FindResource("TextOnAccentFillColorSecondaryBrush")).Color;
					System.Windows.Controls.TextBlock[] muted = RuntimeInspector.Descendants(item).OfType<System.Windows.Controls.TextBlock>().Where(text => text.FontSize == 11).ToArray();

					Assert.NotEmpty(muted);
					Assert.All(muted, text => Assert.Equal(onAccent, ((System.Windows.Media.SolidColorBrush)text.Foreground).Color));
				},
				ApplicationTheme.Light);
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void NoListItemIsNamedAfterAnObject(String name)
		{
			WindowHost.Run(() => Create(name), window => AssertNone(RuntimeInspector.FindObjectNames(window)));
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