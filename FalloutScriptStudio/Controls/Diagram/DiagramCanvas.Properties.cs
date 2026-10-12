namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Model;

	// Dependency properties and state accessors of the canvas.
	public sealed partial class DiagramCanvas
	{
		/// <summary>Identifies the <see cref="Zoom"/> property.</summary>
		public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
			nameof(Zoom),
			typeof(Double),
			typeof(DiagramCanvas),
			new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomChanged, CoerceZoom));

		/// <summary>Identifies the <see cref="MinZoom"/> property.</summary>
		public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
			nameof(MinZoom),
			typeof(Double),
			typeof(DiagramCanvas),
			new PropertyMetadata(0.1, OnZoomLimitChanged));

		/// <summary>Identifies the <see cref="MaxZoom"/> property.</summary>
		public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
			nameof(MaxZoom),
			typeof(Double),
			typeof(DiagramCanvas),
			new PropertyMetadata(4.0, OnZoomLimitChanged));

		/// <summary>Identifies the <see cref="HighQuality"/> property.</summary>
		public static readonly DependencyProperty HighQualityProperty = DependencyProperty.Register(
			nameof(HighQuality),
			typeof(Boolean),
			typeof(DiagramCanvas),
			new PropertyMetadata(true, OnRenderOptionChanged));

		/// <summary>Identifies the <see cref="LowDetail"/> property.</summary>
		public static readonly DependencyProperty LowDetailProperty = DependencyProperty.Register(
			nameof(LowDetail),
			typeof(Boolean),
			typeof(DiagramCanvas),
			new PropertyMetadata(false, OnRenderOptionChanged));

		/// <summary>Identifies the <see cref="LinksOnlyForSelection"/> property.</summary>
		public static readonly DependencyProperty LinksOnlyForSelectionProperty = DependencyProperty.Register(
			nameof(LinksOnlyForSelection),
			typeof(Boolean),
			typeof(DiagramCanvas),
			new PropertyMetadata(false, (d, e) => ((DiagramCanvas)d)._surface.LinksOnlyForSelection = (Boolean)e.NewValue));

		/// <summary>Identifies the <see cref="ShowGrid"/> property.</summary>
		public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
			nameof(ShowGrid),
			typeof(Boolean),
			typeof(DiagramCanvas),
			new PropertyMetadata(true, (d, e) => ((DiagramCanvas)d)._surface.ShowGrid = (Boolean)e.NewValue));

		/// <summary>Identifies the <see cref="SnapGridSize"/> property.</summary>
		public static readonly DependencyProperty SnapGridSizeProperty = DependencyProperty.Register(
			nameof(SnapGridSize),
			typeof(Double),
			typeof(DiagramCanvas),
			new PropertyMetadata(5.0));

		/// <summary>Identifies the <see cref="ZoomsOnMouseWheel"/> property.</summary>
		public static readonly DependencyProperty ZoomsOnMouseWheelProperty = DependencyProperty.Register(
			nameof(ZoomsOnMouseWheel),
			typeof(Boolean),
			typeof(DiagramCanvas),
			new PropertyMetadata(true));

		/// <summary>Gets or sets the zoom factor (1.0 = 100 %), limited to <see cref="MinZoom"/> .. <see cref="MaxZoom"/>.</summary>
		public Double Zoom
		{
			get => (Double)this.GetValue(ZoomProperty);
			set => this.SetValue(ZoomProperty, value);
		}

		/// <summary>Gets or sets the smallest zoom factor (default 0.1 = 10 %).</summary>
		public Double MinZoom
		{
			get => (Double)this.GetValue(MinZoomProperty);
			set => this.SetValue(MinZoomProperty, value);
		}

		/// <summary>Gets or sets the largest zoom factor (default 4.0 = 400 %).</summary>
		public Double MaxZoom
		{
			get => (Double)this.GetValue(MaxZoomProperty);
			set => this.SetValue(MaxZoomProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether shadows and anti-aliased edges are used (default <c>true</c>).</summary>
		public Boolean HighQuality
		{
			get => (Boolean)this.GetValue(HighQualityProperty);
			set => this.SetValue(HighQualityProperty, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether nodes are drawn with reduced detail: no header lines, icons or chevrons,
		/// and a bullet before messages.
		/// </summary>
		public Boolean LowDetail
		{
			get => (Boolean)this.GetValue(LowDetailProperty);
			set => this.SetValue(LowDetailProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether, while a node is selected, only the links attached to selected nodes are drawn.</summary>
		public Boolean LinksOnlyForSelection
		{
			get => (Boolean)this.GetValue(LinksOnlyForSelectionProperty);
			set => this.SetValue(LinksOnlyForSelectionProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the dotted background grid is drawn.</summary>
		public Boolean ShowGrid
		{
			get => (Boolean)this.GetValue(ShowGridProperty);
			set => this.SetValue(ShowGridProperty, value);
		}

		/// <summary>Gets or sets the grid, in world units, that dragged and resized items snap to; zero disables snapping (default 5).</summary>
		public Double SnapGridSize
		{
			get => (Double)this.GetValue(SnapGridSizeProperty);
			set => this.SetValue(SnapGridSizeProperty, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the plain mouse wheel zooms (default, like the legacy editor). When
		/// <c>false</c> the wheel scrolls and only Ctrl+wheel zooms. Shift+wheel always scrolls horizontally.
		/// </summary>
		public Boolean ZoomsOnMouseWheel
		{
			get => (Boolean)this.GetValue(ZoomsOnMouseWheelProperty);
			set => this.SetValue(ZoomsOnMouseWheelProperty, value);
		}

		/// <summary>Gets all items from back to front.</summary>
		public ReadOnlyObservableCollection<DiagramItem> Items => this._readOnlyItems;

		/// <summary>Gets the nodes, from back to front.</summary>
		public IEnumerable<DiagramNode> Nodes => this._scene.Items.OfType<DiagramNode>();

		/// <summary>Gets the connector links resolved from <see cref="DiagramRow.LinkTargetName"/> (including links of hidden nodes).</summary>
		public IReadOnlyList<DiagramLink> Links => this._scene.Links;

		/// <summary>Gets the selected items in the order they were selected.</summary>
		public IReadOnlyList<DiagramItem> SelectedItems => this._scene.Selection;

		/// <summary>Gets the most recently selected item, or <c>null</c>.</summary>
		public DiagramItem? SelectedItem => this._scene.Selection.Count > 0 ? this._scene.Selection[this._scene.Selection.Count - 1] : null;

		/// <summary>Gets the item currently under the mouse pointer, or <c>null</c>.</summary>
		public DiagramItem? HoveredItem => this._scene.Hover.Item;

		/// <summary>Gets the row currently under the mouse pointer, or <c>null</c>.</summary>
		public DiagramRow? HoveredRow => this._scene.Hover.Row;

		/// <summary>Gets the world position of the last mouse click (any button). Useful to place a new item where a context menu was opened.</summary>
		public Point LastClickPosition => this._lastClickPosition;

		/// <summary>Gets the horizontal scroll offset in screen units (at the current zoom).</summary>
		public Double HorizontalOffset => this._scrollViewer.HorizontalOffset;

		/// <summary>Gets the vertical scroll offset in screen units (at the current zoom).</summary>
		public Double VerticalOffset => this._scrollViewer.VerticalOffset;

		/// <summary>Gets the size of the visible area in screen units.</summary>
		public Size ViewportSize
		{
			get
			{
				Double w = this._scrollViewer.ViewportWidth;
				Double h = this._scrollViewer.ViewportHeight;
				return w > 0.0 && h > 0.0 ? new Size(w, h) : new Size(this.ActualWidth, this.ActualHeight);
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The signature is fixed by CoerceValueCallback.")]
		private static Object CoerceZoom(DependencyObject d, Object baseValue)
		{
			DiagramCanvas canvas = (DiagramCanvas)d;
			Double value = (Double)baseValue;
			if (Double.IsNaN(value) || Double.IsInfinity(value))
			{
				return canvas.Zoom;
			}

			Double min = Math.Min(canvas.MinZoom, canvas.MaxZoom);
			Double max = Math.Max(canvas.MinZoom, canvas.MaxZoom);
			return Math.Clamp(value, min, max);
		}

		private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((DiagramCanvas)d).ApplyZoom((Double)e.NewValue);
		}

		private static void OnZoomLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			d.CoerceValue(ZoomProperty);
		}

		private static void OnRenderOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((DiagramCanvas)d).RebuildContext();
		}
	}
}
