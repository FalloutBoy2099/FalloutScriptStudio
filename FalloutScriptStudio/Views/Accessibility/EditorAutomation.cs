namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls;
	using System.Windows.Media;
	using System.Windows.Shapes;
	using System.Windows.Threading;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Editing;
	using ICSharpCode.AvalonEdit.Folding;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// Gives a code editor an accessible name that follows the language. The keyboard focus lives in the text area inside the editor, and a
	/// screen reader announces the name of the control that has the focus, so the name is set on the text area as well. The margins of the
	/// text area (line numbers, folding markers, the separator line) get a name too: without one a screen reader announces the .NET type
	/// of each (<c>ICSharpCode.AvalonEdit.Editing.LineNumberMargin</c>).
	/// </summary>
	public static class EditorAutomation
	{
		/// <summary>Identifies the NameResource attached property: the key of the localized accessible name.</summary>
		public static readonly DependencyProperty NameResourceProperty = DependencyProperty.RegisterAttached(
			"NameResource",
			typeof(String),
			typeof(EditorAutomation),
			new PropertyMetadata(null, OnNameResourceChanged));

		/// <summary>Gets the resource key of the accessible name of the editor.</summary>
		/// <param name="element">The editor.</param>
		/// <returns>The resource key, or <see langword="null"/>.</returns>
		public static String? GetNameResource(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (String?)element.GetValue(NameResourceProperty);
		}

		/// <summary>Sets the resource key of the accessible name of the editor.</summary>
		/// <param name="element">The editor.</param>
		/// <param name="value">The resource key.</param>
		public static void SetNameResource(DependencyObject element, String? value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(NameResourceProperty, value);
		}

		private static void OnNameResourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not TextEditor editor || e.NewValue is not String key)
			{
				return;
			}

			editor.SetResourceReference(AutomationProperties.NameProperty, key);
			editor.TextArea.SetResourceReference(AutomationProperties.NameProperty, key);
			TextArea area = editor.TextArea;
			area.Loaded -= OnTextAreaLoaded;
			area.Loaded += OnTextAreaLoaded;
			if (area.IsLoaded)
			{
				NameMargins(area);
			}
		}

		private static void OnTextAreaLoaded(Object sender, RoutedEventArgs e)
		{
			if (sender is TextArea area)
			{
				NameMargins(area);
				area.LeftMargins.CollectionChanged -= OnMarginsChanged;
				area.LeftMargins.CollectionChanged += OnMarginsChanged;
				void OnMarginsChanged(Object? changed, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
				{
					// The containers of the new margins exist once the items control has generated them.
					_ = area.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => NameMargins(area)));
				}
			}
		}

		private static void NameMargins(TextArea area)
		{
			ItemsControl? list = FindMarginList(area);
			if (list is null)
			{
				return;
			}

			foreach (UIElement margin in area.LeftMargins)
			{
				String? key = margin switch
				{
					LineNumberMargin => "Str.Editor.A11y.LineNumbers",
					FoldingMargin => "Str.Editor.A11y.FoldingMargin",
					Shape => "Str.Editor.A11y.MarginSeparator",
					_ => null,
				};
				if (key is not null && list.ItemContainerGenerator.ContainerFromItem(margin) is FrameworkElement container)
				{
					container.SetResourceReference(AutomationProperties.NameProperty, key);
				}
			}
		}

		private static ItemsControl? FindMarginList(DependencyObject parent)
		{
			// The template of the text area holds one items control: the margins on the left of the text.
			Int32 count = VisualTreeHelper.GetChildrenCount(parent);
			for (Int32 index = 0; index < count; index++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, index);
				if (child is ItemsControl list)
				{
					return list;
				}

				if (child is not TextView && FindMarginList(child) is { } found)
				{
					return found;
				}
			}

			return null;
		}
	}
}