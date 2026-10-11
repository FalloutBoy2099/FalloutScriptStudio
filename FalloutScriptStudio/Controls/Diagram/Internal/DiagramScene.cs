namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;
	using FalloutScriptStudio.Controls.Diagram.Routing;

	/// <summary>
	/// The WPF-visual-free state of a diagram: ordered items, selection, hover, computed node layouts, resolved links and
	/// their routes. The scene listens to its items and reports what changed so that the surface can redraw selectively.
	/// </summary>
	internal sealed class DiagramScene
	{
		private readonly List<DiagramItem> _items = new List<DiagramItem>();
		private readonly HashSet<DiagramItem> _itemSet = new HashSet<DiagramItem>();
		private readonly List<DiagramItem> _selection = new List<DiagramItem>();
		private readonly Dictionary<DiagramNode, NodeLayout> _layouts = new Dictionary<DiagramNode, NodeLayout>();
		private readonly HashSet<DiagramNode> _dirtyLayouts = new HashSet<DiagramNode>();
		private readonly List<DiagramLink> _links = new List<DiagramLink>();
		private readonly List<RoutedLink> _routes = new List<RoutedLink>();
		private readonly Dictionary<String, DiagramNode> _byName = new Dictionary<String, DiagramNode>(StringComparer.OrdinalIgnoreCase);
		private readonly ConnectorRouter _router = new ConnectorRouter();
		private DiagramRenderContext _context;
		private DiagramHitTestResult _hover = DiagramHitTestResult.Nothing;
		private Boolean _namesDirty = true;
		private Boolean _linksDirty = true;
		private Boolean _routesDirty = true;
		private Boolean _boundsDirty = true;
		private Rect _bounds = Rect.Empty;
		private Int32 _applyingLayout;

		public DiagramScene(DiagramRenderContext context)
		{
			this._context = context;
		}

		public event EventHandler<SceneChangedEventArgs>? Changed;

		public DiagramRenderContext Context => this._context;

		/// <summary>Gets the items from back to front.</summary>
		public IReadOnlyList<DiagramItem> Items => this._items;

		/// <summary>Gets the selected items in the order they were selected.</summary>
		public IReadOnlyList<DiagramItem> Selection => this._selection;

		public DiagramHitTestResult Hover => this._hover;

		/// <summary>Gets or sets the canvas zoom; the scene only needs it to size the resize handles.</summary>
		public Double Zoom { get; set; } = 1.0;

		public IReadOnlyList<DiagramLink> Links
		{
			get
			{
				this.EnsureLinks();
				return this._links;
			}
		}

		public IReadOnlyList<RoutedLink> Routes
		{
			get
			{
				this.EnsureRoutes();
				return this._routes;
			}
		}

		public void SetContext(DiagramRenderContext context)
		{
			this._context = context;
			foreach (DiagramItem item in this._items)
			{
				if (item is DiagramNode node)
				{
					this._dirtyLayouts.Add(node);
				}
			}

			this._routesDirty = true;
			this._boundsDirty = true;
			this.Raise(SceneChange.Context | SceneChange.Layout, null);
		}

		public Boolean Contains(DiagramItem item)
		{
			return this._itemSet.Contains(item);
		}

		public void Add(DiagramItem item)
		{
			ArgumentNullException.ThrowIfNull(item);
			if (this._itemSet.Contains(item))
			{
				throw new InvalidOperationException("The item is already on this canvas.");
			}

			this._items.Add(item);
			this._itemSet.Add(item);
			this.Attach(item);
			this.Raise(SceneChange.Structure, item);
		}

		public void Insert(Int32 index, DiagramItem item)
		{
			ArgumentNullException.ThrowIfNull(item);
			if (this._itemSet.Contains(item))
			{
				throw new InvalidOperationException("The item is already on this canvas.");
			}

			this._items.Insert(Math.Clamp(index, 0, this._items.Count), item);
			this._itemSet.Add(item);
			this.Attach(item);
			this.Raise(SceneChange.Structure, item);
		}

		public Int32 IndexOf(DiagramItem item)
		{
			return this._items.IndexOf(item);
		}

		public Boolean Remove(DiagramItem item)
		{
			if (!this._items.Remove(item))
			{
				return false;
			}

			this._itemSet.Remove(item);
			Boolean wasSelected = this._selection.Remove(item);
			item.SetSelected(false);
			item.PropertyChanged -= this.OnItemPropertyChanged;
			if (item is DiagramNode node)
			{
				this._layouts.Remove(node);
				this._dirtyLayouts.Remove(node);
			}

			if (ReferenceEquals(this._hover.Item, item))
			{
				this._hover = DiagramHitTestResult.Nothing;
			}

			this._namesDirty = true;
			this._linksDirty = true;
			this._routesDirty = true;
			this._boundsDirty = true;
			this.Raise(wasSelected ? SceneChange.Structure | SceneChange.Selection : SceneChange.Structure, item);
			return true;
		}

		public void Clear()
		{
			List<DiagramItem> all = new List<DiagramItem>(this._items);
			foreach (DiagramItem item in all)
			{
				this.Remove(item);
			}
		}

		public void BringToFront(DiagramItem item)
		{
			Int32 index = this._items.IndexOf(item);
			if (index < 0 || index == this._items.Count - 1)
			{
				return;
			}

			this._items.RemoveAt(index);
			this._items.Add(item);
			this.Raise(SceneChange.Structure, item);
		}

		public void SendToBack(DiagramItem item)
		{
			Int32 index = this._items.IndexOf(item);
			if (index <= 0)
			{
				return;
			}

			this._items.RemoveAt(index);
			this._items.Insert(0, item);
			this.Raise(SceneChange.Structure, item);
		}

		public DiagramNode? FindNode(String name)
		{
			this.EnsureNames();
			return name != null && this._byName.TryGetValue(name, out DiagramNode? node) ? node : null;
		}

		public NodeLayout GetLayout(DiagramNode node)
		{
			this.EnsureLayouts();
			return this._layouts[node];
		}

		/// <summary>
		/// Recomputes every stale node layout and publishes the resulting node heights (and automatic widths).
		/// </summary>
		public void EnsureLayouts()
		{
			if (this._dirtyLayouts.Count == 0)
			{
				return;
			}

			List<DiagramNode> dirty = new List<DiagramNode>(this._dirtyLayouts);
			this._dirtyLayouts.Clear();
			this._applyingLayout++;
			try
			{
				foreach (DiagramNode node in dirty)
				{
					NodeLayout layout = NodeLayoutBuilder.Build(node, node.Width, this._context);
					this._layouts[node] = layout;
					if (node.Width <= 0.0)
					{
						node.Width = layout.Width;
					}

					node.SetMeasuredHeight(layout.Height);
				}
			}
			finally
			{
				this._applyingLayout--;
			}

			this._routesDirty = true;
			this._boundsDirty = true;
		}

		public Rect GetItemBounds(DiagramItem item)
		{
			if (!this._itemSet.Contains(item))
			{
				return item.Bounds;
			}

			if (item is DiagramNode node)
			{
				NodeLayout layout = this.GetLayout(node);
				return new Rect(node.X, node.Y, layout.Width, layout.Height);
			}

			return new Rect(item.X, item.Y, item.Width, item.Height);
		}

		/// <summary>
		/// Gets the union of the bounds of all visible items, or <see cref="Rect.Empty"/>.
		/// </summary>
		public Rect GetContentBounds()
		{
			this.EnsureLayouts();
			if (this._boundsDirty)
			{
				Rect bounds = Rect.Empty;
				foreach (DiagramItem item in this._items)
				{
					if (!item.IsHidden)
					{
						bounds.Union(this.GetItemBounds(item));
					}
				}

				this._bounds = bounds;
				this._boundsDirty = false;
			}

			return this._bounds;
		}

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

		private void Attach(DiagramItem item)
		{
			item.PropertyChanged += this.OnItemPropertyChanged;
			if (item is DiagramNode node)
			{
				this._dirtyLayouts.Add(node);
				this.EnsureLayouts();
			}

			this._namesDirty = true;
			this._linksDirty = true;
			this._routesDirty = true;
			this._boundsDirty = true;
		}

		private void OnItemPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (this._applyingLayout > 0 || sender is not DiagramItem item)
			{
				return;
			}

			DiagramNode? node = item as DiagramNode;
			switch (e.PropertyName)
			{
				case nameof(DiagramItem.X):
				case nameof(DiagramItem.Y):
					this._routesDirty = true;
					this._boundsDirty = true;
					this.Raise(SceneChange.Geometry, item);
					break;
				case nameof(DiagramItem.Width):
				case nameof(DiagramItem.Height):
					if (node != null)
					{
						if (e.PropertyName == nameof(DiagramItem.Width))
						{
							this._dirtyLayouts.Add(node);
						}
						else
						{
							return;
						}
					}

					this._routesDirty = true;
					this._boundsDirty = true;
					this.Raise(SceneChange.Layout, item);
					break;
				case nameof(DiagramItem.IsHidden):
					this._namesDirty = true;
					this._linksDirty = true;
					this._routesDirty = true;
					this._boundsDirty = true;
					this.Raise(SceneChange.Structure, item);
					if (item.IsHidden)
					{
						this.SetSelected(item, false);
						if (ReferenceEquals(this._hover.Item, item))
						{
							this._hover = DiagramHitTestResult.Nothing;
						}
					}

					break;
				case nameof(DiagramItem.IsSelected):
				case nameof(DiagramItem.Tag):
					break;
				case nameof(DiagramNode.Name):
					this._namesDirty = true;
					this._linksDirty = true;
					this.MarkLayoutDirty(node);
					this.Raise(SceneChange.Layout, item);
					break;
				case nameof(DiagramNode.Rows):
					this._linksDirty = true;
					this.MarkLayoutDirty(node);
					this.Raise(SceneChange.Layout, item);
					break;
				case nameof(DiagramNode.Kind):
				case nameof(DiagramNode.IsCollapsed):
				case nameof(DiagramNode.ShowCodeRows):
				case nameof(DiagramNode.ShowCodeButton):
					this.MarkLayoutDirty(node);
					this.Raise(SceneChange.Layout, item);
					break;
				default:
					this.Raise(SceneChange.Appearance, item);
					break;
			}
		}

		private void MarkLayoutDirty(DiagramNode? node)
		{
			if (node != null)
			{
				this._dirtyLayouts.Add(node);
			}

			this._routesDirty = true;
			this._boundsDirty = true;
		}

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

		private void Raise(SceneChange change, DiagramItem? item)
		{
			this.Changed?.Invoke(this, new SceneChangedEventArgs(change, item));
		}
	}
}
