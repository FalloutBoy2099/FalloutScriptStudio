namespace FalloutScriptStudio.Controls.Diagram.Routing
{
	using System;
	using System.Collections.Generic;
	using System.Windows;

	/// <summary>
	/// Computes orthogonal (horizontal / vertical only) connector routes between two rectangles. The line always leaves the
	/// source towards the right, at an arbitrary start point (typically the right edge of the source at the height of the row
	/// that owns the link), and enters the target through the side nearest to the start point. This is a port of the legacy
	/// <c>Route.Recalc</c> algorithm; degenerate layouts that produced lines missing the target in the legacy code now get a
	/// detour instead, so the last point of the route is always exactly on the chosen target side.
	/// </summary>
	public sealed class ConnectorRouter
	{
		private const Double Epsilon = 0.01;

		private readonly ConnectorRouterOptions _options;

		/// <summary>
		/// Initializes a router with default options.
		/// </summary>
		public ConnectorRouter()
			: this(new ConnectorRouterOptions())
		{
		}

		/// <summary>
		/// Initializes a router.
		/// </summary>
		/// <param name="options">Tuning values.</param>
		public ConnectorRouter(ConnectorRouterOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			this._options = options;
		}

		/// <summary>
		/// Gets the connection point of <paramref name="rect"/> on the given side.
		/// </summary>
		/// <param name="rect">The rectangle.</param>
		/// <param name="side">The side.</param>
		/// <param name="anchorOffset">Vertical offset from the top of the rectangle used by the east and west sides.</param>
		/// <returns>The connection point.</returns>
		public static Point GetConnectionPoint(Rect rect, ConnectionSide side, Double anchorOffset)
		{
			return side switch
			{
				ConnectionSide.North => new Point(rect.Left + (rect.Width / 2.0), rect.Top),
				ConnectionSide.East => new Point(rect.Right, rect.Top + anchorOffset),
				ConnectionSide.South => new Point(rect.Left + (rect.Width / 2.0), rect.Bottom),
				_ => new Point(rect.Left, rect.Top + anchorOffset)
			};
		}

		/// <summary>
		/// Chooses the side of <paramref name="target"/> that is nearest (squared Euclidean distance) to <paramref name="start"/>.
		/// Ties are resolved in the order North, East, South, West.
		/// </summary>
		/// <param name="start">Start of the line.</param>
		/// <param name="target">Target rectangle.</param>
		/// <param name="anchorOffset">Vertical offset of the east and west connection points from the top of the target.</param>
		/// <returns>The nearest side.</returns>
		public static ConnectionSide ChooseTargetSide(Point start, Rect target, Double anchorOffset)
		{
			ConnectionSide best = ConnectionSide.North;
			Double bestDistance = Double.MaxValue;
			for (Int32 i = 0; i < 4; i++)
			{
				ConnectionSide side = (ConnectionSide)i;
				Point p = GetConnectionPoint(target, side, anchorOffset);
				Double dx = start.X - p.X;
				Double dy = start.Y - p.Y;
				Double distance = (dx * dx) + (dy * dy);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = side;
				}
			}

			return best;
		}

		/// <summary>
		/// Computes a route.
		/// </summary>
		/// <param name="start">Start point of the line; the first segment always goes to the right.</param>
		/// <param name="source">Rectangle of the source node (the line detours around it when needed).</param>
		/// <param name="target">Rectangle of the target node.</param>
		/// <param name="targetAnchorOffset">Vertical offset of the target's east / west connection point from its top.</param>
		/// <returns>The route.</returns>
		public ConnectorRoute Route(Point start, Rect source, Rect target, Double targetAnchorOffset)
		{
			ConnectionSide side = ChooseTargetSide(start, target, targetAnchorOffset);
			Point end = GetConnectionPoint(target, side, targetAnchorOffset);
			List<RouteSegment> segments = new(5);

			switch (side)
			{
				case ConnectionSide.East:
					this.RouteToEast(segments, start, end);
					break;
				case ConnectionSide.West:
					this.RouteToWest(segments, start, end, source, target);
					break;
				case ConnectionSide.North:
					this.RouteToVerticalSide(segments, start, end, source, true);
					break;
				default:
					this.RouteToVerticalSide(segments, start, end, source, false);
					break;
			}

			if (segments.Count == 0)
			{
				segments.Add(new RouteSegment(RouteDirection.Right, 0.0));
			}

			return new ConnectorRoute(start, segments, side);
		}

		private static void Add(List<RouteSegment> segments, RouteDirection direction, Double length)
		{
			if (length < Epsilon)
			{
				return;
			}

			if (segments.Count > 0 && segments[segments.Count - 1].Direction == direction)
			{
				RouteSegment last = segments[segments.Count - 1];
				segments[segments.Count - 1] = new RouteSegment(direction, last.Length + length);
				return;
			}

			segments.Add(new RouteSegment(direction, length));
		}

		private static void AddVertical(List<RouteSegment> segments, Double fromY, Double toY)
		{
			Add(segments, toY >= fromY ? RouteDirection.Down : RouteDirection.Up, Math.Abs(toY - fromY));
		}

		private static void AddHorizontal(List<RouteSegment> segments, Double fromX, Double toX)
		{
			Add(segments, toX >= fromX ? RouteDirection.Right : RouteDirection.Left, Math.Abs(toX - fromX));
		}

		// The line arrives at the right edge of the target travelling left: right, vertical, left.
		private void RouteToEast(List<RouteSegment> segments, Point start, Point end)
		{
			Double stub = this._options.TurnStubLength;
			Double diff = start.X - end.X;
			Double first = stub;
			Double last = stub;
			if (diff < 0.0)
			{
				first += -diff;
			}
			else
			{
				last += diff;
			}

			Add(segments, RouteDirection.Right, first);
			AddVertical(segments, start.Y, end.Y);
			Add(segments, RouteDirection.Left, last);
		}

		// The line arrives at the left edge of the target travelling right.
		private void RouteToWest(List<RouteSegment> segments, Point start, Point end, Rect source, Rect target)
		{
			if (end.X - start.X >= 1.0)
			{
				Double half = (end.X - start.X) / 2.0;
				Add(segments, RouteDirection.Right, half);
				AddVertical(segments, start.Y, end.Y);
				Add(segments, RouteDirection.Right, half);
				return;
			}

			// The target lies to the left of the start: detour through a horizontal channel clear of both rectangles.
			Double exit = this._options.ExitStubLength;
			Double clearance = this._options.Clearance;
			Double channelY;
			if (end.Y >= start.Y)
			{
				channelY = target.Top - source.Bottom >= 2.0 * clearance
					? (source.Bottom + target.Top) / 2.0
					: Math.Max(source.Bottom, target.Bottom) + clearance;
			}
			else
			{
				channelY = source.Top - target.Bottom >= 2.0 * clearance
					? (target.Bottom + source.Top) / 2.0
					: Math.Min(source.Top, target.Top) - clearance;
			}

			Double entryX = end.X - exit;
			Add(segments, RouteDirection.Right, exit);
			AddVertical(segments, start.Y, channelY);
			AddHorizontal(segments, start.X + exit, entryX);
			AddVertical(segments, channelY, end.Y);
			Add(segments, RouteDirection.Right, exit);
		}

		// The line arrives at the top (travelling down) or bottom (travelling up) edge of the target.
		private void RouteToVerticalSide(List<RouteSegment> segments, Point start, Point end, Rect source, Boolean arriveDown)
		{
			Boolean directlyReachable = end.X > start.X + Epsilon && (arriveDown ? end.Y > start.Y + Epsilon : end.Y < start.Y - Epsilon);
			if (directlyReachable)
			{
				Add(segments, RouteDirection.Right, end.X - start.X);
				AddVertical(segments, start.Y, end.Y);
				return;
			}

			Double exit = this._options.ExitStubLength;
			Double clearance = this._options.Clearance;
			Double channelY;
			if (arriveDown)
			{
				channelY = end.Y - source.Bottom >= clearance
					? (source.Bottom + end.Y) / 2.0
					: Math.Min(end.Y, source.Top) - clearance;
			}
			else
			{
				channelY = source.Top - end.Y >= clearance
					? (end.Y + source.Top) / 2.0
					: Math.Max(end.Y, source.Bottom) + clearance;
			}

			Add(segments, RouteDirection.Right, exit);
			AddVertical(segments, start.Y, channelY);
			AddHorizontal(segments, start.X + exit, end.X);
			AddVertical(segments, channelY, end.Y);
		}
	}
}