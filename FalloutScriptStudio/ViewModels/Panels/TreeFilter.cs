namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;

	/// <summary>Shows only the nodes of a tree that match a filter text (and their ancestors and descendants).</summary>
	public static class TreeFilter
	{
		/// <summary>Applies a filter to a tree.</summary>
		/// <param name="nodes">The top level nodes.</param>
		/// <param name="text">The text to look for (case-insensitive); empty shows everything.</param>
		/// <returns><see langword="true"/> when at least one node is shown.</returns>
		public static Boolean Apply(IEnumerable<IFilterNode> nodes, String? text)
		{
			ArgumentNullException.ThrowIfNull(nodes);
			return Apply(nodes, (text ?? String.Empty).Trim(), false);
		}

		private static Boolean Apply(IEnumerable<IFilterNode> nodes, String text, Boolean ancestorMatched)
		{
			Boolean any = false;
			Boolean filtering = text.Length > 0;
			foreach (IFilterNode node in nodes)
			{
				if (!filtering)
				{
					node.IsFilterVisible = true;
					Apply(node.FilterChildren, text, false);
					any = true;
					continue;
				}

				if (node.HidesWhenFiltering)
				{
					node.IsFilterVisible = false;
					continue;
				}

				Boolean selfMatches = node.FilterText.Contains(text, StringComparison.OrdinalIgnoreCase);
				Boolean childMatches = Apply(node.FilterChildren, text, ancestorMatched || selfMatches);
				Boolean visible = ancestorMatched || selfMatches || childMatches;
				node.IsFilterVisible = visible;
				if (childMatches && !selfMatches)
				{
					node.IsExpanded = true;
				}

				any |= visible;
			}

			return any;
		}
	}
}
