namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Markup;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Support;
	using FalloutScriptStudio.Tests.ToolWindows;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.Accessibility;
	using FalloutScriptStudio.Views.ToolWindows;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>The small pieces of code that make the interface accessible: attached behaviors, the settings card, the focus rectangle.</summary>
	public sealed class AccessibilityBehaviorTests
	{
		private const String Namespaces =
			"xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:a11y=\"clr-namespace:FalloutScriptStudio.Views.Accessibility;assembly=FalloutScriptStudio\"";

		/// <summary>Raises a key down on a window with a keyboard device that has the chosen modifiers down: the keys the developer holds while the tests run do not count.</summary>
		private static void RaiseKey(Window window, Key key, ModifierKeys modifiers = ModifierKeys.None)
		{
			window.RaiseEvent(new FakeKeyboardDevice(modifiers).CreateKeyDown(PresentationSource.FromVisual(window), key));
		}

		private static Window ShowOffScreen(Window window)
		{
			OffscreenWindow.Prepare(window);
			window.Show();
			WindowHost.Pump();
			return window;
		}

		[Fact]
		public void FitToWorkArea_LimitsTheSizeAndTheMinimumSize()
		{
			UiThread.Run(() =>
			{
				Window window = new Window { Width = 3000, Height = 2000, MinWidth = 2500, MinHeight = 1800 };

				WindowFit.Fit(window, new Size(1000, 500));

				Assert.Equal(1000, window.Width);
				Assert.Equal(500, window.Height);
				Assert.Equal(1000, window.MinWidth);
				Assert.Equal(500, window.MinHeight);
			});
		}

		[Fact]
		public void FitToWorkArea_KeepsASmallWindowAsItIs()
		{
			UiThread.Run(() =>
			{
				Window window = new Window { Width = 360, Height = 230, MinWidth = 300, MinHeight = 200 };

				WindowFit.Fit(window, new Size(1000, 500));

				Assert.Equal(360, window.Width);
				Assert.Equal(230, window.Height);
				Assert.Equal(300, window.MinWidth);
				Assert.Equal(200, window.MinHeight);
			});
		}

		[Fact]
		public void FitToWorkArea_AppliesWhenTheMarkupIsLoaded()
		{
			UiThread.Run(() =>
			{
				Window window = (Window)XamlReader.Parse("<Window " + Namespaces + " Width=\"90000\" Height=\"90000\" a11y:WindowFit.FitToWorkArea=\"True\" />");

				Assert.True(window.Width <= SystemParameters.WorkArea.Width);
				Assert.True(window.Height <= SystemParameters.WorkArea.Height);
			});
		}

		[Fact]
		public void CloseOnEscape_ClosesTheWindowWhenEscapeIsNotHandled()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				Window window = ShowOffScreen((Window)XamlReader.Parse("<Window " + Namespaces + " a11y:WindowKeys.CloseOnEscape=\"True\" Width=\"200\" Height=\"100\" />"));
				Boolean closed = false;
				window.Closed += (_, _) => closed = true;

				RaiseKey(window, Key.Enter);
				Assert.False(closed);

				RaiseKey(window, Key.Escape);
				Assert.True(closed);
			});
		}

		[Fact]
		public void CloseOnEscape_IgnoresEscapeWithAModifier()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				Window window = ShowOffScreen((Window)XamlReader.Parse("<Window " + Namespaces + " a11y:WindowKeys.CloseOnEscape=\"True\" Width=\"200\" Height=\"100\" />"));
				Boolean closed = false;
				window.Closed += (_, _) => closed = true;

				RaiseKey(window, Key.Escape, ModifierKeys.Control);
				Assert.False(closed);

				RaiseKey(window, Key.Escape);
				Assert.True(closed);
			});
		}

		[Fact]
		public void CloseOnEscape_LeavesEscapeToTheControlThatHandledIt()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextBox box = new TextBox();
				box.KeyDown += (_, e) => e.Handled = e.Key == Key.Escape;
				Window window = (Window)XamlReader.Parse("<Window " + Namespaces + " a11y:WindowKeys.CloseOnEscape=\"True\" Width=\"200\" Height=\"100\" />");
				window.Content = box;
				ShowOffScreen(window);
				Boolean closed = false;
				window.Closed += (_, _) => closed = true;

				box.RaiseEvent(new FakeKeyboardDevice().CreateKeyDown(PresentationSource.FromVisual(window), Key.Escape));

				Assert.False(closed);
				window.Close();
			});
		}

		[Fact]
		public void WithoutTheProperty_EscapeDoesNotCloseTheWindow()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				Window window = ShowOffScreen(new Window { Width = 200, Height = 100 });
				Boolean closed = false;
				window.Closed += (_, _) => closed = true;

				RaiseKey(window, Key.Escape);

				Assert.False(closed);
				window.Close();
			});
		}

		[Fact]
		public void ALiveRegion_IsPolite()
		{
			UiThread.Run(() =>
			{
				TextBlock text = new TextBlock { Text = "Ready" };
				Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(text));

				LiveRegion.SetText(text, "Ready");
				LiveRegion.SetText(text, "Compiling...");

				Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(text));
				Assert.Equal("Compiling...", LiveRegion.GetText(text));
			});
		}

		[Fact]
		public void TheNameOfACodeEditor_IsSetOnTheEditorAndOnItsTextArea()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextEditor editor = new TextEditor();
				Window window = new Window { Content = editor, Width = 300, Height = 200 };
				window.Resources["Str.Test.Editor"] = "Script editor";

				EditorAutomation.SetNameResource(editor, "Str.Test.Editor");
				ShowOffScreen(window);

				Assert.Equal("Script editor", AutomationProperties.GetName(editor));
				Assert.Equal("Script editor", AutomationProperties.GetName(editor.TextArea));
				window.Close();
			});
		}

		[Fact]
		public void TheMarginsOfACodeEditor_HaveANameForAScreenReader()
		{
			WindowHost.RunOnOwnThread(() =>
			{
				TextEditor editor = new TextEditor { ShowLineNumbers = true };
				Window window = new Window { Content = editor, Width = 300, Height = 200 };
				window.Resources["Str.Test.Editor"] = "Script editor";
				window.Resources["Str.Editor.A11y.LineNumbers"] = "Line numbers";
				window.Resources["Str.Editor.A11y.FoldingMargin"] = "Code folding markers";
				window.Resources["Str.Editor.A11y.MarginSeparator"] = "Separator";

				EditorAutomation.SetNameResource(editor, "Str.Test.Editor");
				ShowOffScreen(window);

				// Without a name the items of the list of margins are announced with the name of their type (ICSharpCode.AvalonEdit.Editing.LineNumberMargin).
				ItemsControl margins = RuntimeInspector.Descendants(editor.TextArea).OfType<ItemsControl>().First();
				String[] names = editor.TextArea.LeftMargins.Select(margin => AutomationProperties.GetName(margins.ItemContainerGenerator.ContainerFromItem(margin))).ToArray();
				Assert.Contains("Line numbers", names);
				Assert.Contains("Separator", names);
				Assert.DoesNotContain(names, name => name.Length == 0);
				window.Close();
			});
		}

		[Fact]
		public void TheButtonsOfTheTitleBarAreNamedForAScreenReader()
		{
			String[] names = Array.Empty<String>();
			WindowHost.Run(
				() => (Window)XamlReader.Parse(
					"<Window " + Namespaces + " xmlns:ui=\"http://schemas.lepo.co/wpfui/2022/xaml\" Width=\"300\" Height=\"200\"><ui:TitleBar a11y:TitleBarNames.Localize=\"True\" /></Window>"),
				window =>
				{
					names = RuntimeInspector.Descendants(window).OfType<System.Windows.Controls.Primitives.ButtonBase>().Select(button => button.Name + "=" + AutomationProperties.GetName(button)).ToArray();
				});

			Assert.Contains("PART_MinimizeButton=Minimize", names);
			Assert.Contains("PART_MaximizeButton=Maximize", names);
			Assert.Contains("PART_CloseButton=Close", names);
		}

		[Fact]
		public void TheArrowsAndTheTrackOfAScrollBarAreNamedForAScreenReader()
		{
			String[] names = Array.Empty<String>();
			WindowHost.Run(
				() => (Window)XamlReader.Parse(
					"<Window " + Namespaces + " Width=\"300\" Height=\"200\"><ScrollBar a11y:ScrollBarNames.Localize=\"True\" Orientation=\"Vertical\" Maximum=\"10\" ViewportSize=\"2\" /></Window>"),
				window =>
				{
					names = RuntimeInspector.Descendants(window).OfType<System.Windows.Controls.Primitives.ButtonBase>().Select(button => (button.Command == ScrollBar.PageUpCommand ? "PageUp" : button.Command == ScrollBar.PageDownCommand ? "PageDown" : button.Name) + "=" + AutomationProperties.GetName(button)).ToArray();
				});

			// Without the names the arrows are announced with the name of their icon (CaretUp24) and the track buttons have none.
			Assert.Contains("PART_ButtonScrollUp=Scroll up", names);
			Assert.Contains("PART_ButtonScrollDown=Scroll down", names);
			Assert.Contains("PageUp=Page up", names);
			Assert.Contains("PageDown=Page down", names);
		}

		[Fact]
		public void TheDiagramCanvasShowsWhereTheKeyboardFocusIs()
		{
			UiThread.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();

				Assert.True(canvas.Focusable);
				Assert.NotNull(canvas.FocusVisualStyle);
			});
		}

		[Fact]
		public void ARowOfTheErrorList_IsAnnouncedWithItsPlaceKindAndMessage()
		{
			ErrorItemViewModel row = new ErrorItemViewModel(new BuildError(BuildErrorType.Error, "Undefined symbol", @"C:\scripts\a.ssl", 12, 3), "Build");

			Assert.Equal(@"C:\scripts\a.ssl(12,3): Error: Undefined symbol", row.ToString());
		}

		[Fact]
		public void ACardLabelsItsControlAndPassesItsDescriptionAsHelp()
		{
			WindowHost.Run(
				() => new SettingsWindow(new SettingsViewModel(new FakeSettingsService(@"C:\fss"), new FakeThemeService(), new FakeDialogService(), new FalloutScriptStudio.Tests.FakeFileAssociationService(), new FakeFontCatalog("Consolas"))),
				window =>
				{
					SettingCard card = RuntimeInspector.Descendants(window).OfType<SettingCard>().First(c => c.IsVisible && c.Content is ComboBox);
					ComboBox box = (ComboBox)card.Content;

					TextBlock label = Assert.IsType<TextBlock>(AutomationProperties.GetLabeledBy(box));
					Assert.Equal(card.Title, label.Text);
					Assert.Equal(card.Description, AutomationProperties.GetHelpText(box));
				});
		}
	}
}