namespace FalloutScriptStudio.Controls.Diagram.Routing
{
	using System;
	using System.Collections.Generic;
	using System.Windows;

	/// <summary>
	/// The computed path of a connector line: a start point followed by axis-aligned segments.
	/// </summary>
	public sealed class ConnectorRoute
	{
		private readonly List<RouteSegment> _segments;
		private readonly List<Point> _points;

		/// <summary>
		/// Initializes a route.
		/// </summary>
		/// <param name="start">Start of the line.</param>
		/// <param name="segments">Axis-aligned pieces; at least one is required.</param>
		/// <param name="targetSide">The side of the target rectangle the line ends at.</param>
		public ConnectorRoute(Point start, IEnumerable<RouteSegment> segments, ConnectionSide targetSide)
		{
			ArgumentNullException.ThrowIfNull(segments);
			this._segments = new(segments);
			if (this._segments.Count == 0)
			{
				throw new ArgumentException("A route needs at least one segment.", nameof(segments));
			}

			this.Start = start;
			this.TargetSide = targetSide;
			this._points = new(this._segments.Count + 1) { start };
			Point current = start;
			foreach (RouteSegment segment in this._segments)
			{
				current = segment.GetDestination(current);
				this._points.Add(current);
			}
		}

		/// <summary>Gets the first point of the line.</summary>
		public Point Start { get; }

		/// <summary>Gets the last point of the line (where the arrow head is drawn).</summary>
		public Point End => this._points[this._points.Count - 1];

		/// <summary>Gets the side of the target rectangle the line ends at.</summary>
		public ConnectionSide TargetSide { get; }

		/// <summary>Gets the pieces of the line.</summary>
		public IReadOnlyList<RouteSegment> Segments => this._segments;

		/// <summary>Gets the vertices of the polyline: the start point followed by the end of each segment.</summary>
		public IReadOnlyList<Point> Points => this._points;

		/// <summary>Gets the direction of the first segment.</summary>
		public RouteDirection StartDirection => this._segments[0].Direction;

		/// <summary>Gets the direction of the last segment, i.e. the direction the arrow head points to.</summary>
		public RouteDirection EndDirection => this._segments[this._segments.Count - 1].Direction;

		/// <summary>
		/// Gets the smallest rectangle containing every vertex of the line.
		/// </summary>
		/// <returns>The bounding box.</returns>
		public Rect GetBounds()
		{
			Rect bounds = new(this._points[0], this._points[0]);
			for (Int32 i = 1; i < this._points.Count; i++)
			{
				bounds.Union(this._points[i]);
			}

			return bounds;
		}
	}
}