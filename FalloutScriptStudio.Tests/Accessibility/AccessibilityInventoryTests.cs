namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>
	/// The accessibility inventory of every window and panel of the application, checked on the markup (so it does not depend on a
	/// display, a theme or a DPI setting): accessible names, keyboard reachability, theme colors, dialog conventions.
	/// New XAML must follow the rules of <c>docs\CONVENTIONS.md</c>, these tests fail otherwise.
	/// </summary>
	public sealed class AccessibilityInventoryTests
	{
		private const Double MinimumFontSize = 11;

		private readonly XamlAccessibilityScanner _scanner = new XamlAccessibilityScanner();

		public static IEnumerable<Object[]> Views()
		{
			return SourceTree.ViewFiles().Select(file => new Object[] { Path.GetRelativePath(SourceTree.AppFolder, file) });
		}

		public static IEnumerable<Object[]> Windows()
		{
			return SourceTree.WindowFiles().Select(file => new Object[] { Path.GetRelativePath(SourceTree.AppFolder, file) });
		}

		public static IEnumerable<Object[]> StyledFiles()
		{
			return SourceTree.StyledFiles().Select(file => new Object[] { Path.GetRelativePath(SourceTree.AppFolder, file) });
		}

		private static void AssertNone(IReadOnlyList<String> findings)
		{
			Assert.True(findings.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, findings));
		}

		[Fact]
		public void TheInventoryCoversEveryWindowOfTheApplication()
		{
			String[] expected =
			{
				"MainWindow", "SettingsWindow", "ProjectSettingsWindow", "AboutWindow", "GoToLineWindow", "BatchCompileWindow", "HeadersWindow",
				"ProcedureNameWindow", "RegisterScriptWindow", "AdvancedSearchWindow", "MessageEditorWindow", "NodeCodeEditorWindow",
				"FlowchartWindow", "DialoguePreviewWindow", "MessageTextWindow", "NewNodeWindow", "DialogueRulesWindow", "UserFunctionWindow",
				"MacroRenamePreviewWindow", "SymbolPickerWindow", "CommandPaletteWindow", "ReplacePreviewWindow", "FormatProjectWindow", "SnippetPickerWindow", "SnippetManagerWindow", "CompareMessagesWindow",
			};
			String[] windows = SourceTree.ViewFiles().Select(Path.GetFileNameWithoutExtension).Select(name => name!).ToArray();
			String[] panels =
			{
				"ProceduresPanelView", "VariablesPanelView", "FunctionsPanelView", "ProjectExplorerView", "RightBrowserView", "OutputPanelView", "GlobalsPanelView", "HelpPanelView", "HelpTopicView",
				"SearchBarView", "DocumentTabStrip", "WelcomeView", "ScriptEditorView",
			};

			Assert.Empty(expected.Except(windows));
			Assert.Empty(panels.Except(windows));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void EveryInteractiveControlHasAnAccessibleName(String relativePath)
		{
			AssertNone(this._scanner.FindMissingNames(Path.Combine(SourceTree.AppFolder, relativePath)));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void EveryInteractiveControlIsReachableWithTheKeyboard(String relativePath)
		{
			AssertNone(this._scanner.FindUnreachableControls(Path.Combine(SourceTree.AppFolder, relativePath)));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void NoControlHidesItsKeyboardFocusRectangle(String relativePath)
		{
			AssertNone(this._scanner.FindHiddenFocusVisuals(Path.Combine(SourceTree.AppFolder, relativePath)));
		}

		[Theory]
		[MemberData(nameof(StyledFiles))]
		public void ACustomTemplateKeepsTheFocusRectangleOfTheTheme(String relativePath)
		{
			String file = Path.Combine(SourceTree.AppFolder, relativePath);
			AssertNone(this._scanner.FindStylesWithoutFocusVisual(file, SourceTree.Load(file)));
		}

		[Theory]
		[MemberData(nameof(StyledFiles))]
		public void ColorsComeFromTheThemeAndNotFromTheMarkup(String relativePath)
		{
			AssertNone(this._scanner.FindHardCodedColors(Path.Combine(SourceTree.AppFolder, relativePath)));
		}

		[Theory]
		[MemberData(nameof(StyledFiles))]
		public void NoTextIsSmallerThanTheMinimumFontSize(String relativePath)
		{
			AssertNone(this._scanner.FindSmallFonts(Path.Combine(SourceTree.AppFolder, relativePath), MinimumFontSize));
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void EveryWindowFollowsTheDialogConventions(String relativePath)
		{
			if (relativePath.EndsWith("MainWindow.xaml", StringComparison.Ordinal))
			{
				return;
			}

			AssertNone(this._scanner.FindWindowConventionViolations(Path.Combine(SourceTree.AppFolder, relativePath)));
		}

		[Theory]
		[MemberData(nameof(Windows))]
		public void EveryWindowHasCrispLinesAndFitsASmallScreen(String relativePath)
		{
			String file = Path.Combine(SourceTree.AppFolder, relativePath);
			AssertNone(this._scanner.FindLayoutProblems(file, SourceTree.Load(file)));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void TheTabOrderFollowsTheReadingOrder(String relativePath)
		{
			String file = Path.Combine(SourceTree.AppFolder, relativePath);
			AssertNone(this._scanner.FindReadingOrderProblems(file, SourceTree.Load(file)));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void ListsAndTreesCanBeOperatedWithTheKeyboard(String relativePath)
		{
			String file = Path.Combine(SourceTree.AppFolder, relativePath);
			AssertNone(this._scanner.FindMouseOnlyLists(file, SourceTree.Load(file)));
		}

		[Fact]
		public void TheStatusBarAnnouncesItsChanges()
		{
			String text = File.ReadAllText(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml"));

			Assert.Contains("a11y:LiveRegion.Text=\"{Binding StatusMessage}\"", text, StringComparison.Ordinal);
			Assert.Contains("a11y:LiveRegion.Text=\"{Binding OperationText}\"", text, StringComparison.Ordinal);
			Assert.Contains("a11y:LiveRegion.Text=\"{Binding ProblemsTip}\"", text, StringComparison.Ordinal);
			Assert.Contains("AutomationProperties.Name=\"{DynamicResource Str.Shell.A11y.Progress}\"", text, StringComparison.Ordinal);
			Assert.Contains("LiveRegion.SetText(this.Toast", File.ReadAllText(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml.cs")), StringComparison.Ordinal);
			Assert.Contains("a11y:LiveRegion.Text=\"{Binding StatusText}\"", File.ReadAllText(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "SearchBarView.xaml")), StringComparison.Ordinal);
		}

		[Fact]
		public void TheStatusBarValuesAreButtons_NotTextThatReactsToTheMouse()
		{
			XDocument window = SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml"));

			// The parser status, the zoom and the project are commands of the status bar: they must take the keyboard focus.
			Assert.Equal(3, window.Descendants().Count(e => e.Attribute("Style")?.Value.Contains("ShellStatusTextButtonStyle", StringComparison.Ordinal) == true));
		}

		[Theory]
		[MemberData(nameof(Views))]
		public void NoGestureIsBoundTwiceInAView(String relativePath)
		{
			AssertNone(XamlAccessibilityScanner.FindDuplicateGestures(Path.Combine(SourceTree.AppFolder, relativePath)));
		}
	}
}