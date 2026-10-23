namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows;
	using System.Windows.Automation.Peers;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;

	/// <summary>
	/// Accessibility rules checked on the live visual tree of a shown window: the name a screen reader reads (through the automation
	/// peer), the keyboard reachability and the dialog conventions. It complements the static scan of the markup.
	/// </summary>
	internal static class RuntimeInspector
	{
		private static readonly Regex ObjectName = new(@"^\s*\w+\s*\{.*=.*\}\s*$|^(?:FalloutScriptStudio|System|ICSharpCode|Wpf)\.[\w.+`]+$", RegexOptions.Compiled);

		/// <summary>Gets every visible element of the visual tree, depth first.</summary>
		public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
		{
			yield return root;
			Int32 count = root is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
			for (Int32 i = 0; i < count; i++)
			{
				foreach (DependencyObject child in Descendants(VisualTreeHelper.GetChild(root, i)))
				{
					yield return child;
				}
			}
		}

		/// <summary>Finds interactive controls for which a screen reader would announce no name.</summary>
		public static IReadOnlyList<String> FindMissingNames(Window window)
		{
			List<String> findings = [];

			// A screen reader walks the tree and so creates the peers of the labels before it asks for the name of a labelled control.
			foreach (UIElement element in Descendants(window).OfType<UIElement>())
			{
				_ = UIElementAutomationPeer.CreatePeerForElement(element);
			}

			foreach (FrameworkElement element in Descendants(window).OfType<FrameworkElement>().Where(IsChecked))
			{
				AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(element);
				String name = peer?.GetName() ?? String.Empty;
				if (name.Trim().Length == 0)
				{
					// A screen reader falls back to the label of the control (LabeledBy) when the control has no name of its own.
					name = peer?.GetLabeledBy()?.GetName() ?? String.Empty;
				}

				if (name.Trim().Length == 0)
				{
					findings.Add($"{window.GetType().Name}: {Describe(element)} has no accessible name");
				}
			}

			return findings;
		}

		/// <summary>
		/// Finds the items of lists and trees whose name is the text of an object (<c>Item { Page = Editor, Title = ... }</c> for a record,
		/// <c>Namespace.TypeName</c> for a class): what a screen reader reads when the data item has no name of its own.
		/// </summary>
		public static IReadOnlyList<String> FindObjectNames(Window window)
		{
			List<String> findings = [];
			foreach (ItemsControl list in Descendants(window).OfType<ItemsControl>())
			{
				AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(list);
				foreach (AutomationPeer item in peer?.GetChildren() ?? [])
				{
					String name = item.GetName();
					if (ObjectName.IsMatch(name))
					{
						findings.Add($"{window.GetType().Name}: {Describe(list)} has an item named '{name}'");
					}
				}
			}

			return findings;
		}

		/// <summary>Finds interactive controls that the Tab key cannot reach.</summary>
		public static IReadOnlyList<String> FindUnreachable(Window window)
		{
			List<String> findings = [];
			foreach (Control element in Descendants(window).OfType<Control>().Where(IsChecked).Where(control => control is not ProgressBar))
			{
				// The items of a list or of a tree take the Tab stop of their control, so the control itself only has to take the focus.
				Boolean stop = element is ItemsControl || element.IsTabStop;
				if (!element.Focusable || !stop)
				{
					findings.Add($"{window.GetType().Name}: {Describe(element)} is not a tab stop");
				}
			}

			return findings;
		}

		/// <summary>Finds the violations of the dialog conventions: a cancel button or Escape, and an initial focus.</summary>
		/// <param name="window">The shown window.</param>
		/// <param name="escapeInCode">Whether the code of the window handles Escape itself.</param>
		public static IReadOnlyList<String> FindDialogProblems(Window window, Boolean escapeInCode)
		{
			List<String> findings = [];
			Boolean hasCancel = Descendants(window).OfType<Button>().Any(button => button.IsCancel);
			if (!hasCancel && !escapeInCode && !Views.Accessibility.WindowKeys.GetCloseOnEscape(window))
			{
				findings.Add($"{window.GetType().Name}: Escape does not close the window");
			}

			if (FocusManager.GetFocusedElement(window) is null)
			{
				findings.Add($"{window.GetType().Name}: nothing has the initial focus");
			}

			return findings;
		}

		private static Boolean IsChecked(DependencyObject element)
		{
			if (element is not FrameworkElement { IsVisible: true } frameworkElement)
			{
				return false;
			}

			// The headers of a grid are not controls of their own: the markup scan checks that every column has a header.
			if (element is System.Windows.Controls.Primitives.DataGridColumnHeader)
			{
				return false;
			}

			if (!(element is ButtonBase or TextBoxBase or PasswordBox or Selector or TreeView or RangeBase or Wpf.Ui.Controls.NumberBox or ICSharpCode.AvalonEdit.Editing.TextArea))
			{
				return false;
			}

			// Parts of the template of another control (the text of a combo box, the buttons of a scroll bar or of the title bar) are not
			// separate controls for the user; a control produced by a data template (templated parent: the content presenter) is.
			return frameworkElement.TemplatedParent is null or ContentPresenter && element is not RepeatButton && element is not Thumb;
		}

		private static String Describe(FrameworkElement element)
		{
			String name = String.IsNullOrEmpty(element.Name) ? String.Empty : " '" + element.Name + "'";
			String content = element is ContentControl { Content: String text } ? " \"" + text + "\"" : String.Empty;
			String tip = element.ToolTip as String is { } tooltip ? " (" + tooltip + ")" : String.Empty;
			return element.GetType().Name + name + content + tip;
		}
	}
}