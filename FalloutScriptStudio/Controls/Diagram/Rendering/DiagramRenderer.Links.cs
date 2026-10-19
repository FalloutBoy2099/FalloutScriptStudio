namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using Internal;
	using Model;
	using Routing;

	/// <summary>Drawing of the connector lines: routed polylines with rounded corners and the markers at the ends.</summary>
	internal sealed partial class DiagramRenderer
	{
		private const Double LinkCornerRadius = 8.0;

		/// <summary>
		/// Draws the connector lines.
		/// </summary>
		public void DrawLinks(DrawingContext dc, Double zoom, Boolean onlyForSelection, Boolean interactive)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			Boolean anySelectedNode = false;
			if (interactive)
			{
				foreach (DiagramItem item in this._scene.Selection)
				{
					if (item is DiagramNode)
					{
						anySelectedNode = true;
						break;
					}
				}
			}

			List<RoutedLink> normal = [];
			List<RoutedLink> incoming = [];
			List<RoutedLink> outgoing = [];
			foreach (RoutedLink routed in this._scene.Routes)
			{
				if (interactive && routed.Link.Source.IsSelected)
				{
					outgoing.Add(routed);
				}
				else if (interactive && routed.Link.Target.IsSelected)
				{
					incoming.Add(routed);
				}
				else if (!(onlyForSelection && anySelectedNode))
				{
					normal.Add(routed);
				}
			}

			Double thickness = Math.Clamp(1.5 / zoom, 1.0, 4.0);
			Double scale = Math.Clamp(1.0 / Math.Sqrt(zoom), 0.5, 2.0);
			this.DrawLinkGroup(dc, normal, palette.LinkNormal, thickness, scale, drawStartMarkers: true);
			this.DrawLinkGroup(dc, incoming, palette.LinkIncoming, thickness + 0.5, scale, drawStartMarkers: true);
			this.DrawLinkGroup(dc, outgoing, palette.LinkOutgoing, thickness + 0.5, scale, drawStartMarkers: false);
		}

		private static StreamGeometry BuildRoundedPolylines(IEnumerable<RoutedLink> links)
		{
			StreamGeometry geometry = new();
			using (StreamGeometryContext ctx = geometry.Open())
			{
				foreach (RoutedLink routed in links)
				{
					IReadOnlyList<Point> pts = routed.Route.Points;
					ctx.BeginFigure(pts[0], false, false);
					for (Int32 i = 1; i < pts.Count - 1; i++)
					{
						Point prev = pts[i - 1];
						Point corner = pts[i];
						Point next = pts[i + 1];
						Double inLength = (corner - prev).Length;
						Double outLength = (next - corner).Length;
						Double r = Math.Min(LinkCornerRadius, Math.Min(inLength, outLength) / 2.0);
						Vector inDir = (corner - prev) / inLength;
						Vector outDir = (next - corner) / outLength;
						ctx.LineTo(corner - (inDir * r), true, false);
						ctx.QuadraticBezierTo(corner, corner + (outDir * r), true, false);
					}

					ctx.LineTo(pts[pts.Count - 1], true, false);
				}
			}

			geometry.Freeze();
			return geometry;
		}

		private static Vector DirectionVector(RouteDirection direction)
		{
			return direction switch
			{
				RouteDirection.Up => new Vector(0, -1),
				RouteDirection.Down => new Vector(0, 1),
				RouteDirection.Left => new Vector(-1, 0),
				_ => new Vector(1, 0)
			};
		}

		private void DrawLinkGroup(DrawingContext dc, List<RoutedLink> links, Brush brush, Double thickness, Double scale, Boolean drawStartMarkers)
		{
			if (links.Count == 0)
			{
				return;
			}

			DiagramPalette palette = this._scene.Context.Palette;
			Pen pen = DiagramPalette.MakeLinkPen(brush, thickness);
			dc.DrawGeometry(null, pen, BuildRoundedPolylines(links));

			StreamGeometry arrows = new();
			Double length = 9.0 * scale;
			Double half = 4.5 * scale;
			using (StreamGeometryContext ctx = arrows.Open())
			{
				foreach (RoutedLink routed in links)
				{
					Point tip = routed.Route.End;
					Vector dir = DirectionVector(routed.Route.EndDirection);
					Vector perp = new(-dir.Y, dir.X);
					Point back = tip - (dir * length);
					ctx.BeginFigure(tip, true, true);
					ctx.LineTo(back + (perp * half), true, false);
					ctx.LineTo(back - (perp * half), true, false);
				}
			}

			arrows.Freeze();
			dc.DrawGeometry(brush, null, arrows);

			if (drawStartMarkers)
			{
				foreach (RoutedLink routed in links)
				{
					if (routed.DrawStartMarker)
					{
						dc.DrawEllipse(brush, null, routed.Route.Start, 3.0 * scale, 3.0 * scale);
					}
				}
			}
		}
	}
}