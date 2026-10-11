namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Threading;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;

	/// <summary>
	/// The element that is scrolled inside the canvas. It paints the background grid itself and hosts four retained
	/// <see cref="DrawingVisual"/> layers (links, unselected items, selected items, overlay) so that interaction only
	/// re-records the layers that actually changed. The layers are scaled with a transform, so zooming does not re-record them.
	/// </summary>
	internal sealed class DiagramSurface : FrameworkElement
	{
		private const Double ExtentMargin = 120.0;
		private const Double GridSpacing = 20.0;

		private readonly DiagramScene _scene;
		private readonly DiagramRenderer _renderer;
		private readonly VisualCollection _visuals;
		private readonly DrawingVisual _lowerLayer = new DrawingVisual();
		private readonly DrawingVisual _linkLayer = new DrawingVisual();
		private readonly DrawingVisual _upperLayer = new DrawingVisual();
		private readonly DrawingVisual _overlayLayer = new DrawingVisual();
		private SurfaceLayers _dirty = SurfaceLayers.All;
		private Boolean _renderQueued;
		private Double _zoom = 1.0;
		private Size _viewportSize;
		private Rect _rubberBand = Rect.Empty;
		private Rect _lastBounds = Rect.Empty;
		private Boolean _showGrid = true;
		private Boolean _linksOnlyForSelection;
		private Point _appliedOrigin;
		private Double _appliedZoom = Double.NaN;

		public DiagramSurface(DiagramScene scene)
		{
			this._scene = scene;
			this._renderer = new DiagramRenderer(scene);
			this._visuals = new VisualCollection(this)
			{
				this._linkLayer,
				this._lowerLayer,
				this._upperLayer,
				this._overlayLayer
			};

			this.ClipToBounds = true;
			this.UseLayoutRounding = true;
			this.SnapsToDevicePixels = true;
			this.Focusable = false;
			TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
			foreach (DrawingVisual layer in new[] { this._lowerLayer, this._linkLayer, this._upperLayer, this._overlayLayer })
			{
				TextOptions.SetTextFormattingMode(layer, TextFormattingMode.Ideal);
			}

			scene.Changed += this.OnSceneChanged;
		}

		/// <summary>Raised after the layers were re-recorded; the canvas uses it to publish coalesced notifications.</summary>
		public event EventHandler? Flushed;

		public Double Zoom
		{
			get => this._zoom;
			set
			{
				if (Math.Abs(this._zoom - value) < 1e-9)
				{
					return;
				}

				this._zoom = value;
				this._scene.Zoom = value;
				this.InvalidateMeasure();
				this.InvalidateVisual();
				this.InvalidateLayers(SurfaceLayers.Links | SurfaceLayers.Overlay);
			}
		}

		public Boolean ShowGrid
		{
			get => this._showGrid;
			set
			{
				this._showGrid = value;
				this.InvalidateVisual();
			}
		}

		public Boolean LinksOnlyForSelection
		{
			get => this._linksOnlyForSelection;
			set
			{
				this._linksOnlyForSelection = value;
				this.InvalidateLayers(SurfaceLayers.Links);
			}
		}

		public Rect RubberBand
		{
			get => this._rubberBand;
			set
			{
				this._rubberBand = value;
				this.InvalidateLayers(SurfaceLayers.Overlay);
			}
		}

		/// <summary>Gets the world coordinate that is drawn at the top-left corner of the surface.</summary>
		public Point Origin
		{
			get
			{
				Rect bounds = this._scene.GetContentBounds();
				if (bounds.IsEmpty)
				{
					return new Point(0, 0);
				}

				return new Point(Math.Min(0.0, bounds.X), Math.Min(0.0, bounds.Y));
			}
		}

		protected override Int32 VisualChildrenCount => this._visuals.Count;

		public Point WorldToSurface(Point world)
		{
			Point o = this.Origin;
			return new Point((world.X - o.X) * this._zoom, (world.Y - o.Y) * this._zoom);
		}

		public Point SurfaceToWorld(Point surface)
		{
			Point o = this.Origin;
			return new Point((surface.X / this._zoom) + o.X, (surface.Y / this._zoom) + o.Y);
		}

		public void SetViewportSize(Size size)
		{
			if (this._viewportSize == size)
			{
				return;
			}

			this._viewportSize = size;
			this.InvalidateMeasure();
		}

		public void InvalidateLayers(SurfaceLayers layers)
		{
			this._dirty |= layers;
			if (this._renderQueued)
			{
				return;
			}

			this._renderQueued = true;
			Dispatcher dispatcher = this.Dispatcher;
			if (!dispatcher.HasShutdownStarted)
			{
				dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(this.FlushRendering));
			}
		}

		/// <summary>
		/// Brings the node layouts, the layout of the surface and the retained layers up to date immediately.
		/// </summary>
		public void FlushRendering()
		{
			this._renderQueued = false;
			this._scene.EnsureLayouts();

			Rect bounds = this._scene.GetContentBounds();
			if (!bounds.Equals(this._lastBounds))
			{
				this._lastBounds = bounds;
				this.InvalidateMeasure();
			}

			Point origin = this.Origin;
			if (origin != this._appliedOrigin || Double.IsNaN(this._appliedZoom) || Math.Abs(this._appliedZoom - this._zoom) > 1e-12)
			{
				MatrixTransform transform = new MatrixTransform(this._zoom, 0, 0, this._zoom, -origin.X * this._zoom, -origin.Y * this._zoom);
				transform.Freeze();
				this._lowerLayer.Transform = transform;
				this._linkLayer.Transform = transform;
				this._upperLayer.Transform = transform;
				this._overlayLayer.Transform = transform;
				if (origin != this._appliedOrigin)
				{
					this.InvalidateVisual();
				}

				this._appliedOrigin = origin;
				this._appliedZoom = this._zoom;
			}

			EdgeMode edgeMode = this._scene.Context.HighQuality ? EdgeMode.Unspecified : EdgeMode.Aliased;
			SurfaceLayers dirty = this._dirty;
			this._dirty = SurfaceLayers.None;

			if ((dirty & SurfaceLayers.Lower) != 0)
			{
				RenderOptions.SetEdgeMode(this._lowerLayer, edgeMode);
				using (DrawingContext dc = this._lowerLayer.RenderOpen())
				{
					this._renderer.DrawItems(dc, false, true);
				}
			}

			if ((dirty & SurfaceLayers.Links) != 0)
			{
				RenderOptions.SetEdgeMode(this._linkLayer, edgeMode);
				using (DrawingContext dc = this._linkLayer.RenderOpen())
				{
					this._renderer.DrawLinks(dc, this._zoom, this._linksOnlyForSelection, true);
				}
			}

			if ((dirty & SurfaceLayers.Upper) != 0)
			{
				RenderOptions.SetEdgeMode(this._upperLayer, edgeMode);
				using (DrawingContext dc = this._upperLayer.RenderOpen())
				{
					this._renderer.DrawItems(dc, true, true);
				}
			}

			if ((dirty & SurfaceLayers.Overlay) != 0)
			{
				RenderOptions.SetEdgeMode(this._overlayLayer, edgeMode);
				using (DrawingContext dc = this._overlayLayer.RenderOpen())
				{
					this._renderer.DrawOverlay(dc, this._zoom, this._rubberBand);
				}
			}

			this.Flushed?.Invoke(this, EventArgs.Empty);
		}

		protected override Visual GetVisualChild(Int32 index)
		{
			return this._visuals[index];
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			this._scene.EnsureLayouts();
			Rect bounds = this._scene.GetContentBounds();
			Double width = 0.0;
			Double height = 0.0;
			if (!bounds.IsEmpty)
			{
				Point origin = this.Origin;
				width = (bounds.Right - origin.X + ExtentMargin) * this._zoom;
				height = (bounds.Bottom - origin.Y + ExtentMargin) * this._zoom;
			}

			width = Math.Max(width, this._viewportSize.Width);
			height = Math.Max(height, this._viewportSize.Height);
			return new Size(
				Double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width),
				Double.IsInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			Rect area = new Rect(this.RenderSize);
			drawingContext.DrawRectangle(palette.Background, null, area);
			if (!this._showGrid)
			{
				return;
			}

			Double spacing = GridSpacing * this._zoom;
			while (spacing < 10.0)
			{
				spacing *= 5.0;
			}

			Point origin = this.Origin;
			Double worldSpacing = spacing / this._zoom;
			Double firstX = Math.Ceiling(origin.X / worldSpacing) * worldSpacing;
			Double firstY = Math.Ceiling(origin.Y / worldSpacing) * worldSpacing;
			Double offsetX = (firstX - origin.X) * this._zoom;
			Double offsetY = (firstY - origin.Y) * this._zoom;

			GeometryDrawing dot = new GeometryDrawing(palette.GridDot, null, new EllipseGeometry(new Point(spacing / 2.0, spacing / 2.0), 1.0, 1.0));
			DrawingBrush brush = new DrawingBrush(dot)
			{
				Viewbox = new Rect(0, 0, spacing, spacing),
				ViewboxUnits = BrushMappingMode.Absolute,
				Viewport = new Rect(offsetX - (spacing / 2.0), offsetY - (spacing / 2.0), spacing, spacing),
				ViewportUnits = BrushMappingMode.Absolute,
				TileMode = TileMode.Tile
			};
			brush.Freeze();
			drawingContext.DrawRectangle(brush, null, area);
		}

		private static SurfaceLayers LayerOf(DiagramItem item)
		{
			return item.IsSelected ? SurfaceLayers.Upper : SurfaceLayers.Lower;
		}

		private void OnSceneChanged(Object? sender, SceneChangedEventArgs e)
		{
			SceneChange change = e.Change;
			DiagramItem? item = e.Item;
			if ((change & (SceneChange.Geometry | SceneChange.Layout | SceneChange.Structure | SceneChange.Context)) != 0)
			{
				this.InvalidateMeasure();
			}

			if ((change & (SceneChange.Structure | SceneChange.Selection | SceneChange.Context)) != 0 || item == null)
			{
				this.InvalidateLayers(SurfaceLayers.All);
				return;
			}

			SurfaceLayers layers = LayerOf(item);
			if ((change & (SceneChange.Geometry | SceneChange.Layout)) != 0)
			{
				layers |= SurfaceLayers.Links;
				if (item.IsSelected)
				{
					layers |= SurfaceLayers.Overlay;
				}
			}

			this.InvalidateLayers(layers);
		}
	}
}
