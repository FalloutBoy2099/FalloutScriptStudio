namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;

	/// <summary>A node of a tree that the filter box of a panel can hide.</summary>
	public interface IFilterNode
	{
		/// <summary>Gets the text the filter is matched against.</summary>
		String FilterText { get; }

		/// <summary>Gets or sets a value indicating whether the node is shown.</summary>
		Boolean IsFilterVisible { get; set; }

		/// <summary>Gets or sets a value indicating whether the node is expanded.</summary>
		Boolean IsExpanded { get; set; }

		/// <summary>Gets a value indicating whether the node is hidden while a filter is active (separators).</summary>
		Boolean HidesWhenFiltering { get; }

		/// <summary>Gets the child nodes.</summary>
		IEnumerable<IFilterNode> FilterChildren { get; }
	}
}
