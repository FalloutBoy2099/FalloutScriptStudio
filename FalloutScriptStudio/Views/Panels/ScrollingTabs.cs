namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Threading;

	/// <summary>
	/// Makes a tab control whose headers scroll (<c>ShellScrollingTabControlStyle</c>) show the selected tab: when the selection changes, the
	/// header of the selected tab is scrolled into view, so a tab chosen from the keyboard or by code is never hidden behind the arrows.
	/// </summary>
	public static class ScrollingTabs
	{
		/// <summary>Identifies the BringSelectedIntoView attached property.</summary>
		public static readonly DependencyProperty BringSelectedIntoViewProperty = DependencyProperty.RegisterAttached(
			"BringSelectedIntoView",
			typeof(Boolean),
			typeof(ScrollingTabs),
			new PropertyMetadata(false, OnBringSelectedIntoViewChanged));

		/// <summary>Gets whether the selected tab is scrolled into view.</summary>
		/// <param name="element">The tab control.</param>
		/// <returns><see langword="true"/> when the selected tab is scrolled into view.</returns>
		public static Boolean GetBringSelectedIntoView(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (Boolean)element.GetValue(BringSelectedIntoViewProperty);
		}

		/// <summary>Sets whether the selected tab is scrolled into view.</summary>
		/// <param name="element">The tab control.</param>
		/// <param name="value">Whether the selected tab is scrolled into view.</param>
		public static void SetBringSelectedIntoView(DependencyObject element, Boolean value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(BringSelectedIntoViewProperty, value);
		}

		private static void OnBringSelectedIntoViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not TabControl tabs)
			{
				return;
			}

			tabs.SelectionChanged -= OnSelectionChanged;
			if ((Boolean)e.NewValue)
			{
				tabs.SelectionChanged += OnSelectionChanged;
			}
		}

		private static void OnSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			if (sender is not TabControl tabs || !ReferenceEquals(e.OriginalSource, tabs))
			{
				return;
			}

			// The header is measured after the selection changed, so the request waits for the layout.
			_ = tabs.Dispatcher.BeginInvoke(
				DispatcherPriority.Loaded,
				new Action(() =>
				{
					FrameworkElement? header = tabs.SelectedItem as TabItem ?? tabs.ItemContainerGenerator.ContainerFromItem(tabs.SelectedItem) as FrameworkElement;
					header?.BringIntoView();
				}));
		}
	}
}