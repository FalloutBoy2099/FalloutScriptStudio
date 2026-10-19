namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;

	/// <summary>Small visual tree helpers shared by the tree based panels.</summary>
	internal static class TreeViewHelper
	{
		/// <summary>Finds the nearest ancestor of a given type, including the element itself.</summary>
		public static T? FindAncestor<T>(DependencyObject? start)
			where T : DependencyObject
		{
			DependencyObject? current = start;
			while (current is not null)
			{
				if (current is T match)
				{
					return match;
				}

				current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
			}

			return null;
		}

		/// <summary>Selects the tree item under the mouse (used before a context menu opens).</summary>
		public static void SelectItemUnderMouse(DependencyObject? originalSource)
		{
			TreeViewItem? item = FindAncestor<TreeViewItem>(originalSource);
			if (item is not null)
			{
				item.IsSelected = true;
				item.Focus();
			}
		}
	}
}