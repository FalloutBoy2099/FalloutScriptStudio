namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Functions;

	/// <summary>A node of the functions tree: a category, a separator line or a function that inserts code.</summary>
	public sealed partial class FunctionNodeViewModel : ObservableObject, IFilterNode
	{
		/// <summary>Initializes a node and, recursively, its children.</summary>
		/// <param name="node">The catalog node.</param>
		/// <param name="parent">The parent node, or <see langword="null"/> for a top level node.</param>
		public FunctionNodeViewModel(FunctionNode node, FunctionNodeViewModel? parent)
		{
			ArgumentNullException.ThrowIfNull(node);
			this.Node = node;
			this.Parent = parent;
			this.Key = (parent is null ? String.Empty : parent.Key + "/") + node.Name;
			foreach (FunctionNode child in node.Children)
			{
				this.Children.Add(new FunctionNodeViewModel(child, this));
			}
		}

		/// <summary>Gets the catalog node.</summary>
		public FunctionNode Node { get; }

		/// <summary>Gets the parent node, or <see langword="null"/>.</summary>
		public FunctionNodeViewModel? Parent { get; }

		/// <summary>Gets the path of the node (names joined by slashes), stable across reloads of the catalog.</summary>
		public String Key { get; }

		/// <summary>Gets the child nodes.</summary>
		public ObservableCollection<FunctionNodeViewModel> Children { get; } = new ObservableCollection<FunctionNodeViewModel>();

		/// <inheritdoc/>
		String IFilterNode.FilterText => this.Node.Name;

		/// <inheritdoc/>
		Boolean IFilterNode.HidesWhenFiltering => this.IsSeparator;

		/// <inheritdoc/>
		IEnumerable<IFilterNode> IFilterNode.FilterChildren => this.Children;

		/// <summary>True when the node passes the filter of the panel.</summary>
		[ObservableProperty]
		public partial Boolean IsFilterVisible { get; set; } = true;

		/// <summary>Gets the displayed name.</summary>
		public String Name => this.Node.Name;

		/// <summary>Gets the tooltip description of a function.</summary>
		public String Description => this.Node.Description;

		/// <summary>Gets a value indicating whether the node belongs to the user functions file.</summary>
		public Boolean IsUser => this.Node.IsUser;

		/// <summary>Gets a value indicating whether the node is a function (it has code to insert).</summary>
		public Boolean IsFunction => this.Node.Kind == FunctionNodeKind.Function;

		/// <summary>Gets a value indicating whether the node is a horizontal separator.</summary>
		public Boolean IsSeparator => this.Node.Kind == FunctionNodeKind.Separator;

		/// <summary>True when the node is expanded.</summary>
		[ObservableProperty]
		public partial Boolean IsExpanded { get; set; }

		/// <summary>True when the node is selected.</summary>
		[ObservableProperty]
		public partial Boolean IsSelected { get; set; }
	}
}
