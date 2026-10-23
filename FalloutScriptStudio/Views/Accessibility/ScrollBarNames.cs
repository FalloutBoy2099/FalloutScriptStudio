namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;

	/// <summary>
	/// Gives the buttons of the scroll bars that WPF-UI draws (the arrows at the ends and the two parts of the track) localized names a
	/// screen reader reads: without them the arrows are announced with the name of their icon (<c>CaretUp24</c>) and the track buttons have
	/// no name. It is switched on for every scroll bar by the implicit style of <c>ShellResources.xaml</c>.
	/// </summary>
	public static class ScrollBarNames
	{
		/// <summary>Identifies the Localize attached property.</summary>
		public static readonly DependencyProperty LocalizeProperty = DependencyProperty.RegisterAttached(
			"Localize",
			typeof(Boolean),
			typeof(ScrollBarNames),
			new PropertyMetadata(false, OnLocalizeChanged));

		/// <summary>Gets whether the buttons of the scroll bar are named.</summary>
		/// <param name="element">The scroll bar.</param>
		/// <returns><see langword="true"/> when the buttons are named.</returns>
		public static Boolean GetLocalize(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (Boolean)element.GetValue(LocalizeProperty);
		}

		/// <summary>Sets whether the buttons of the scroll bar are named.</summary>
		/// <param name="element">The scroll bar.</param>
		/// <param name="value">Whether the buttons are named.</param>
		public static void SetLocalize(DependencyObject element, Boolean value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(LocalizeProperty, value);
		}

		private static void OnLocalizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not ScrollBar scrollBar)
			{
				return;
			}

			scrollBar.Loaded -= OnLoaded;
			if ((Boolean)e.NewValue)
			{
				scrollBar.Loaded += OnLoaded;
			}
		}

		private static void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (sender is not ScrollBar scrollBar)
			{
				return;
			}

			scrollBar.ApplyTemplate();
			foreach (ButtonBase button in Descendants(scrollBar).OfType<ButtonBase>())
			{
				String? key = button.Name switch
				{
					"PART_ButtonScrollUp" => "Str.Common.ScrollUp",
					"PART_ButtonScrollDown" => "Str.Common.ScrollDown",
					"PART_ButtonScrollLeft" => "Str.Common.ScrollLeft",
					"PART_ButtonScrollRight" => "Str.Common.ScrollRight",
					_ => GetTrackKey(button.Command),
				};
				if (key is not null)
				{
					button.SetResourceReference(AutomationProperties.NameProperty, key);
				}
			}
		}

		private static String? GetTrackKey(ICommand? command)
		{
			// The two parts of the track have no name in the template: they are known by their command.
			if (ReferenceEquals(command, ScrollBar.PageUpCommand))
			{
				return "Str.Common.PageUp";
			}

			if (ReferenceEquals(command, ScrollBar.PageDownCommand))
			{
				return "Str.Common.PageDown";
			}

			if (ReferenceEquals(command, ScrollBar.PageLeftCommand))
			{
				return "Str.Common.PageLeft";
			}

			return ReferenceEquals(command, ScrollBar.PageRightCommand) ? "Str.Common.PageRight" : null;
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