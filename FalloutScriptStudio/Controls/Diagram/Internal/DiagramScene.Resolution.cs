namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;
	using FalloutScriptStudio.Controls.Diagram.Routing;

	/// <summary>Lazy resolution of the node names, the links and their routes.</summary>
	internal sealed partial class DiagramScene
	{
		private void EnsureNames()
		{
			if (!this._namesDirty)
			{
				return;
			}

			this._byName.Clear();
			foreach (DiagramItem item in this._items)
			{
				if (item is DiagramNode node)
				{
					this._byName.TryAdd(node.Name, node);
				}
			}

			this._namesDirty = false;
		}

		private void EnsureLinks()
		{
			if (!this._linksDirty)
			{
				return;
			}

			this._links.Clear();
			this.EnsureNames();
			foreach (DiagramItem item in this._items)
			{
				if (item is not DiagramNode source)
				{
					continue;
				}

				foreach (DiagramRow row in source.Rows)
				{
					if (!String.IsNullOrEmpty(row.LinkTargetName) && this._byName.TryGetValue(row.LinkTargetName, out DiagramNode? target))
					{
						this._links.Add(new DiagramLink(source, row, target));
					}
				}
			}

			this._linksDirty = false;
			this._routesDirty = true;
		}

		private void EnsureRoutes()
		{
			this.EnsureLayouts();
			this.EnsureLinks();
			if (!this._routesDirty)
			{
				return;
			}

			this._routes.Clear();
			foreach (DiagramLink link in this._links)
			{
				if (link.Source.IsHidden || link.Target.IsHidden)
				{
					continue;
				}

				NodeLayout sourceLayout = this._layouts[link.Source];
				NodeLayout targetLayout = this._layouts[link.Target];
				Rect sourceRect = new Rect(link.Source.X, link.Source.Y, sourceLayout.Width, sourceLayout.Height);
				Rect targetRect = new Rect(link.Target.X, link.Target.Y, targetLayout.Width, targetLayout.Height);

				RowLayout? rowLayout = link.Source.IsCollapsed ? null : sourceLayout.FindRow(link.SourceRow);
				Double anchor = rowLayout?.AnchorY ?? sourceLayout.DefaultSourceAnchorOffset;
				Point start = new Point(sourceRect.Right, sourceRect.Top + anchor);
				ConnectorRoute route = this._router.Route(start, sourceRect, targetRect, targetLayout.TargetAnchorOffset);
				this._routes.Add(new RoutedLink(link, route, rowLayout != null));
			}

			this._routesDirty = false;
		}
	}
}
