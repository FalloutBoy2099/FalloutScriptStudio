namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls.Primitives;
	using System.Windows.Media;
	using Wpf.Ui.Controls;

	/// <summary>
	/// Gives the minimize, maximize and close buttons that WPF-UI draws in the title bar the localized names a screen reader reads (the
	/// template leaves them empty). It is switched on for every title bar by the implicit style of <c>ShellResources.xaml</c>.
	/// </summary>
	public static class TitleBarNames
	{
		/// <summary>Identifies the Localize attached property.</summary>
		public static readonly DependencyProperty LocalizeProperty = DependencyProperty.RegisterAttached(
			"Localize",
			typeof(Boolean),
			typeof(TitleBarNames),
			new PropertyMetadata(false, OnLocalizeChanged));

		/// <summary>Gets whether the buttons of the title bar are named.</summary>
		/// <param name="element">The title bar.</param>
		/// <returns><see langword="true"/> when the buttons are named.</returns>
		public static Boolean GetLocalize(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (Boolean)element.GetValue(LocalizeProperty);
		}

		/// <summary>Sets whether the buttons of the title bar are named.</summary>
		/// <param name="element">The title bar.</param>
		/// <param name="value">Whether the buttons are named.</param>
		public static void SetLocalize(DependencyObject element, Boolean value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(LocalizeProperty, value);
		}

		private static void OnLocalizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not TitleBar titleBar)
			{
				return;
			}

			titleBar.Loaded -= OnLoaded;
			if ((Boolean)e.NewValue)
			{
				titleBar.Loaded += OnLoaded;
			}
		}

		private static void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (sender is not TitleBar titleBar)
			{
				return;
			}

			titleBar.ApplyTemplate();
			Apply(titleBar);
			if (Window.GetWindow(titleBar) is { } window)
			{
				window.StateChanged -= OnWindowStateChanged;
				window.StateChanged += OnWindowStateChanged;
			}
		}

		private static void OnWindowStateChanged(Object? sender, EventArgs e)
		{
			if (sender is Window window && FindTitleBar(window) is { } titleBar)
			{
				Apply(titleBar);
			}
		}

		private static void Apply(TitleBar titleBar)
		{
			Boolean maximized = Window.GetWindow(titleBar)?.WindowState == WindowState.Maximized;
			foreach (ButtonBase button in Descendants(titleBar).OfType<ButtonBase>())
			{
				String? key = button.Name switch
				{
					"PART_MinimizeButton" => "Str.Common.Minimize",
					"PART_MaximizeButton" => maximized ? "Str.Common.Restore" : "Str.Common.Maximize",
					"PART_CloseButton" => "Str.Common.Close",
					"PART_HelpButton" => "Str.Common.Help",
					_ => null,
				};
				if (key is not null)
				{
					button.SetResourceReference(AutomationProperties.NameProperty, key);
				}
			}
		}

		private static TitleBar? FindTitleBar(DependencyObject root)
		{
			return Descendants(root).OfType<TitleBar>().FirstOrDefault();
		}

		private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
		{
			Int32 count = root is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
			for (Int32 i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				yield return child;
				foreach (DependencyObject descendant in Descendants(child))
				{
					yield return descendant;
				}
			}
		}
	}
}
