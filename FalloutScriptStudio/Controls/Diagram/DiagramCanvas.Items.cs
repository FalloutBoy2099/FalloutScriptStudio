namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using Model;

	// Adding, removing and ordering items.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Adds an item at the front. A node without a width gets an automatic one (content width, at most 350 units) and,
		/// like every node, its <see cref="DiagramItem.Height"/> is valid as soon as this method returns.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void AddItem(DiagramItem item)
		{
			this._scene.Add(item);
			this._items.Add(item);
		}

		/// <summary>
		/// Replaces an item by another one at the same position in the z-order. The selection state is carried over.
		/// </summary>
		/// <param name="oldItem">The item to remove; it must be on this canvas.</param>
		/// <param name="newItem">The item to insert in its place.</param>
		public void ReplaceItem(DiagramItem oldItem, DiagramItem newItem)
		{
			ArgumentNullException.ThrowIfNull(oldItem);
			ArgumentNullException.ThrowIfNull(newItem);
			Int32 index = this._scene.IndexOf(oldItem);
			if (index < 0)
			{
				throw new InvalidOperationException("The item is not on this canvas.");
			}

			Boolean wasSelected = oldItem.IsSelected;
			this.BeginUpdate();
			try
			{
				this.RemoveItem(oldItem);
				this._scene.Insert(index, newItem);
				this._items.Insert(Math.Min(index, this._items.Count), newItem);
				if (wasSelected)
				{
					this._scene.SetSelected(newItem, true);
				}
			}
			finally
			{
				this.EndUpdate();
			}
		}

		/// <summary>
		/// Removes an item. Hidden items and selected items can be removed as well.
		/// </summary>
		/// <param name="item">The item to remove.</param>
		/// <returns><c>true</c> when the item was on the canvas.</returns>
		public Boolean RemoveItem(DiagramItem item)
		{
			ArgumentNullException.ThrowIfNull(item);
			if (item is DiagramNote note && ReferenceEquals(note, this._editingNote))
			{
				this.CancelNoteEdit();
			}

			if (!this._scene.Remove(item))
			{
				return false;
			}

			this._items.Remove(item);
			return true;
		}

		/// <summary>
		/// Removes every item and clears the selection.
		/// </summary>
		public void Clear()
		{
			this.CancelNoteEdit();
			this.BeginUpdate();
			try
			{
				this._scene.Clear();
				this._items.Clear();
			}
			finally
			{
				this.EndUpdate();
			}
		}

		/// <summary>
		/// Finds a node by name (case-insensitive), including hidden nodes.
		/// </summary>
		/// <param name="name">The node name.</param>
		/// <returns>The node, or <c>null</c>.</returns>
		public DiagramNode? FindNode(String name)
		{
			return this._scene.FindNode(name);
		}

		/// <summary>
		/// Gets a value indicating whether a node with the given name exists (case-insensitive), including hidden nodes.
		/// </summary>
		/// <param name="name">The node name.</param>
		public Boolean ContainsNode(String name)
		{
			return this._scene.FindNode(name) != null;
		}

		/// <summary>
		/// Moves an item in front of all others.
		/// </summary>
		/// <param name="item">The item.</param>
		public void BringToFront(DiagramItem item)
		{
			this._scene.BringToFront(item);
			Int32 index = this._items.IndexOf(item);
			if (index >= 0 && index < this._items.Count - 1)
			{
				this._items.Move(index, this._items.Count - 1);
			}
		}

		/// <summary>
		/// Moves an item behind all others.
		/// </summary>
		/// <param name="item">The item.</param>
		public void SendToBack(DiagramItem item)
		{
			this._scene.SendToBack(item);
			Int32 index = this._items.IndexOf(item);
			if (index > 0)
			{
				this._items.Move(index, 0);
			}
		}
	}
}