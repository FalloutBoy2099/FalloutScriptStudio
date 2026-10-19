namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;

	/// <summary>A header node of a tree ("Global Procedures", "Script Variables"…).</summary>
	public sealed class TreeGroupNodeViewModel : TreeNodeViewModel
	{
		/// <summary>Initializes a group node.</summary>
		/// <param name="title">The displayed text.</param>
		/// <param name="key">The stable key; also tells the kind of the group.</param>
		/// <param name="isLocal">True for the group of the procedures / variables of the current script.</param>
		public TreeGroupNodeViewModel(String title, String key, Boolean isLocal)
			: base(title, key)
		{
			this.IsLocal = isLocal;
		}

		/// <summary>Gets a value indicating whether the group lists the items of the current script (not of headers).</summary>
		public Boolean IsLocal { get; }

		/// <summary>Gets a value indicating whether the group has no items (it is shown dimmed).</summary>
		public Boolean IsEmpty => this.Children.Count == 0;

		/// <summary>Tells the view that the children were changed in place.</summary>
		internal void NotifyChildrenChanged()
		{
			this.OnPropertyChanged(nameof(this.IsEmpty));
		}
	}
}