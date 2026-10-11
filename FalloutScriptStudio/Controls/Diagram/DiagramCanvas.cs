namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Collections.ObjectModel;
	using System.Windows;
	using System.Windows.Controls;
	using FalloutScriptStudio.Controls.Diagram.Internal;
	using FalloutScriptStudio.Controls.Diagram.Model;

	/// <summary>
	/// A zoomable, scrollable canvas that displays a graph of <see cref="DiagramNode"/> boxes with orthogonal connector
	/// lines and free <see cref="DiagramNote"/> notes, with mouse selection, dragging and resizing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Rendering is done with retained <see cref="DrawingVisual"/> layers instead of one control per node, which keeps ~300
	/// nodes smooth: only the layer that changed is re-recorded and zooming is a pure transform.
	/// Colours come from the WPF-UI Fluent theme resources (<c>CardBackgroundFillColorDefaultBrush</c>,
	/// <c>TextFillColorPrimaryBrush</c>, <c>AccentFillColorDefaultBrush</c>, ...) and follow light/dark theme changes.
	/// </para>
	/// <para>
	/// Interaction: left drag on an item moves it (all selected items move together), Ctrl+click toggles the selection,
	/// left drag on empty space draws a rubber band, the middle button pans, the mouse wheel zooms (or Ctrl+wheel when
	/// <see cref="ZoomsOnMouseWheel"/> is <c>false</c>), selected items show resize handles. Row clicks are reported through
	/// <see cref="RowClicked"/> together with the modifier keys.
	/// </para>
	/// </remarks>
	public sealed partial class DiagramCanvas : UserControl
	{
		private readonly DiagramScene _scene;
		private readonly DiagramSurface _surface;
		private readonly ScrollViewer _scrollViewer;
		private readonly Canvas _editorLayer;
		private readonly DiagramInputHandler _input;
		private readonly ObservableCollection<DiagramItem> _items = new ObservableCollection<DiagramItem>();
		private readonly ReadOnlyObservableCollection<DiagramItem> _readOnlyItems;
		private Int32 _updateDepth;
		private Boolean _selectionChangedPending;
		private Boolean _layoutChangedPending;

		/// <summary>
		/// Initializes an empty canvas.
		/// </summary>
		public DiagramCanvas()
		{
			this._readOnlyItems = new ReadOnlyObservableCollection<DiagramItem>(this._items);
			this.Focusable = true;
			this.FocusVisualStyle = null;
			this.SnapsToDevicePixels = true;
			this.UseLayoutRounding = true;

			this._scene = new DiagramScene(this.CreateContext());
			this._surface = new DiagramSurface(this._scene);
			this._editorLayer = new Canvas { IsHitTestVisible = true };
			Grid host = new Grid();
			host.Children.Add(this._surface);
			host.Children.Add(this._editorLayer);
			this._scrollViewer = new ScrollViewer
			{
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Focusable = false,
				CanContentScroll = false,
				Content = host
			};
			this.Content = this._scrollViewer;

			this._input = new DiagramInputHandler(this, this._scene, this._surface, this._scrollViewer);
			this._scene.Changed += this.OnSceneChanged;
			this._surface.Flushed += this.OnSurfaceFlushed;
			this._scrollViewer.ScrollChanged += this.OnScrollChanged;
			this._input.Attach();

			this.SetResourceReference(ThemeProbeProperty, "TextFillColorPrimaryBrush");
			this.Loaded += this.OnCanvasLoaded;
			this.Unloaded += this.OnCanvasUnloaded;
		}

		/// <summary>Raised after <see cref="Zoom"/> changed.</summary>
		public event EventHandler? ZoomChanged;

		/// <summary>
		/// Raised (coalesced, at render priority) after items were moved, resized, collapsed, added or removed, or after the
		/// zoom changed. Typical use: hide message popups and mark the document as modified.
		/// </summary>
		public event EventHandler? LayoutChanged;

		/// <summary>Raised when the visible area changes: scrolling, zooming or resizing of the control.</summary>
		public event EventHandler? ViewportChanged;

		/// <summary>Raised when the item (or the row of an item) under the mouse pointer changes; <see cref="DiagramItemEventArgs.Item"/> is <c>null</c> when the pointer is over empty space.</summary>
		public event EventHandler<DiagramItemEventArgs>? ItemHover;

		/// <summary>Raised when an item becomes selected.</summary>
		public event EventHandler<DiagramItemEventArgs>? ItemSelected;

		/// <summary>Raised when an item stops being selected (including when it is removed while selected).</summary>
		public event EventHandler<DiagramItemEventArgs>? ItemUnselected;

		/// <summary>Raised once after one or more selection changes were applied.</summary>
		public event EventHandler? SelectionChanged;

		/// <summary>Raised when a row of a node is clicked (without dragging). Clicking the chevron also expands or collapses the row before the event is raised.</summary>
		public event EventHandler<DiagramRowEventArgs>? RowClicked;

		/// <summary>Raised when an item is double-clicked. Notes enter edit mode on their own.</summary>
		public event EventHandler<DiagramItemEventArgs>? ItemDoubleClicked;

		/// <summary>Raised after the user toggled the "show code rows" button of a node.</summary>
		public event EventHandler<DiagramItemEventArgs>? NodeCodeToggled;

		/// <summary>Raised when the Delete key is pressed while the canvas has the keyboard focus; the canvas deletes nothing itself.</summary>
		public event EventHandler? DeleteRequested;

		internal ScrollViewer ScrollHost => this._scrollViewer;

		internal DiagramScene Scene => this._scene;

		internal Boolean IsEditingNote => this._noteEditor != null;

		internal void BeginUpdate()
		{
			this._updateDepth++;
		}

		internal void EndUpdate()
		{
			if (--this._updateDepth == 0 && this._selectionChangedPending)
			{
				this._selectionChangedPending = false;
				this.SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnSceneChanged(Object? sender, SceneChangedEventArgs e)
		{
			if ((e.Change & (SceneChange.Geometry | SceneChange.Layout | SceneChange.Structure)) != 0)
			{
				this._layoutChangedPending = true;
				if (this._editingNote != null && ReferenceEquals(e.Item, this._editingNote))
				{
					this.PositionNoteEditor();
				}
			}

			if ((e.Change & SceneChange.Selection) != 0)
			{
				if (e.Item != null)
				{
					EventHandler<DiagramItemEventArgs>? handler = e.Item.IsSelected ? this.ItemSelected : this.ItemUnselected;
					handler?.Invoke(this, new DiagramItemEventArgs(e.Item));
					if (!e.Item.IsSelected && ReferenceEquals(e.Item, this._editingNote))
					{
						this.CommitNoteEdit();
					}
				}

				this._selectionChangedPending = true;
				if (this._updateDepth == 0)
				{
					this._selectionChangedPending = false;
					this.SelectionChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private void OnSurfaceFlushed(Object? sender, EventArgs e)
		{
			if (this._layoutChangedPending)
			{
				this._layoutChangedPending = false;
				this.LayoutChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnScrollChanged(Object sender, ScrollChangedEventArgs e)
		{
			this._surface.SetViewportSize(new Size(this._scrollViewer.ViewportWidth, this._scrollViewer.ViewportHeight));
			this.ViewportChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
