namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Controls.Diagram.Model;

	// Selecting items.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Selects an item and brings it to the front.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="additive"><c>true</c> to keep the current selection, <c>false</c> to replace it.</param>
		public void Select(DiagramItem item, Boolean additive = false)
		{
			ArgumentNullException.ThrowIfNull(item);
			this.BeginUpdate();
			try
			{
				if (!additive)
				{
					this.DeselectAllExcept(item);
				}

				if (this._scene.SetSelected(item, true))
				{
					this.BringToFront(item);
				}
			}
			finally
			{
				this.EndUpdate();
			}
		}

		/// <summary>
		/// Removes an item from the selection.
		/// </summary>
		/// <param name="item">The item.</param>
		public void Deselect(DiagramItem item)
		{
			ArgumentNullException.ThrowIfNull(item);
			this._scene.SetSelected(item, false);
		}

		/// <summary>
		/// Clears the selection.
		/// </summary>
		public void ClearSelection()
		{
			this.BeginUpdate();
			try
			{
				this.DeselectAllExcept(null);
			}
			finally
			{
				this.EndUpdate();
			}
		}

		/// <summary>
		/// Selects every visible item.
		/// </summary>
		public void SelectAll()
		{
			this.BeginUpdate();
			try
			{
				foreach (DiagramItem item in this._scene.Items.ToList())
				{
					if (!item.IsHidden)
					{
						this._scene.SetSelected(item, true);
					}
				}
			}
			finally
			{
				this.EndUpdate();
			}
		}

		private void DeselectAllExcept(DiagramItem? keep)
		{
			foreach (DiagramItem item in this._scene.Selection.ToList())
			{
				if (!ReferenceEquals(item, keep))
				{
					this._scene.SetSelected(item, false);
				}
			}
		}
	}
}
