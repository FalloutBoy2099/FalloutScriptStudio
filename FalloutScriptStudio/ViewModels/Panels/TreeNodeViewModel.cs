namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>Base class of the nodes of the procedures and variables trees.</summary>
	public abstract partial class TreeNodeViewModel : ObservableObject, IFilterNode
	{
		/// <summary>Initializes a node.</summary>
		/// <param name="title">The displayed text.</param>
		/// <param name="key">A key that is stable across rebuilds of the tree (used to remember expansion).</param>
		protected TreeNodeViewModel(String title, String key)
		{
			this.Title = title;
			this.Key = key;
		}

		/// <summary>Gets the key that identifies the node across rebuilds.</summary>
		public String Key { get; }

		/// <summary>Gets a value indicating whether the node is a procedure that is defined in the current script and can be renamed, moved or deleted.</summary>
		public virtual Boolean CanModify => false;

		/// <summary>Gets a value indicating whether the node is a procedure or variable (not a group header).</summary>
		public virtual Boolean IsSymbol => false;

		/// <summary>Gets the child nodes.</summary>
		public ObservableCollection<TreeNodeViewModel> Children { get; } = new ObservableCollection<TreeNodeViewModel>();

		/// <inheritdoc/>
		String IFilterNode.FilterText => this.Title;

		/// <inheritdoc/>
		Boolean IFilterNode.HidesWhenFiltering => false;

		/// <inheritdoc/>
		IEnumerable<IFilterNode> IFilterNode.FilterChildren => this.Children;

		/// <summary>True when the node passes the filter of the panel.</summary>
		[ObservableProperty]
		public partial Boolean IsFilterVisible { get; set; } = true;

		/// <summary>The displayed text.</summary>
		[ObservableProperty]
		public partial String Title { get; set; }

		/// <summary>The tooltip text.</summary>
		[ObservableProperty]
		public partial String ToolTip { get; set; } = String.Empty;

		/// <summary>True when the node is expanded.</summary>
		[ObservableProperty]
		public partial Boolean IsExpanded { get; set; }

		/// <summary>True when the node is selected.</summary>
		[ObservableProperty]
		public partial Boolean IsSelected { get; set; }
	}
}
