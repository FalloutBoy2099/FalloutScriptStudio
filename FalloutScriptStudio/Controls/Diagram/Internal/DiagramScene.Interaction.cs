namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;
	using FalloutScriptStudio.Controls.Diagram.Routing;

	/// <summary>Hit testing, selection and hover.</summary>
	internal sealed partial class DiagramScene
	{
		public DiagramHitTestResult HitTest(Point world, Boolean includeGrips)
		{
			this.EnsureLayouts();
			if (includeGrips)
			{
				for (Int32 i = this._items.Count - 1; i >= 0; i--)
				{
					DiagramItem item = this._items[i];
					if (!item.IsSelected || item.IsHidden)
					{
						continue;
					}

					Rect bounds = this.GetItemBounds(item);
					for (DiagramHitPart part = DiagramHitPart.ResizeWest; part <= DiagramHitPart.ResizeSouth; part++)
					{
						if (GripGeometry.Has(item, part) && GripGeometry.GetHitRect(bounds, part, this.Zoom).Contains(world))
						{
							return new DiagramHitTestResult(item, null, part);
						}
					}
				}
			}

			for (Int32 i = this._items.Count - 1; i >= 0; i--)
			{
				DiagramItem item = this._items[i];
				if (item.IsHidden)
				{
					continue;
				}

				Rect bounds = this.GetItemBounds(item);
				if (!bounds.Contains(world))
				{
					continue;
				}

				if (item is DiagramNode node)
				{
					(DiagramHitPart part, DiagramRow? row) = this._layouts[node].HitTest(new Point(world.X - bounds.X, world.Y - bounds.Y));
					return new DiagramHitTestResult(node, row, part);
				}

				return new DiagramHitTestResult(item, null, DiagramHitPart.Body);
			}

			return DiagramHitTestResult.Nothing;
		}

		/// <summary>
		/// Changes the selection state of one item.
		/// </summary>
		/// <returns><c>true</c> when the state changed.</returns>
		public Boolean SetSelected(DiagramItem item, Boolean selected)
		{
			if (!this._itemSet.Contains(item) || item.IsSelected == selected)
			{
				return false;
			}

			if (selected)
			{
				this._selection.Add(item);
			}
			else
			{
				this._selection.Remove(item);
			}

			item.SetSelected(selected);
			this.Raise(SceneChange.Selection, item);
			return true;
		}

		public Boolean SetHover(DiagramHitTestResult hit)
		{
			if (hit == this._hover)
			{
				return false;
			}

			DiagramItem? previous = this._hover.Item;
			this._hover = hit;
			if (previous != null)
			{
				this.Raise(SceneChange.Hover, previous);
			}

			if (hit.Item != null && !ReferenceEquals(previous, hit.Item))
			{
				this.Raise(SceneChange.Hover, hit.Item);
			}

			return true;
		}
	}
}
