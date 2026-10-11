namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Routing;
	using Xunit;

	public sealed class ConnectorRouterTests
	{
		private const Double Offset = 32.0;
		private readonly ConnectorRouter _router = new ConnectorRouter();

		[Fact]
		public void ChooseTargetSide_TargetFarToTheRight_EntersThroughWestEdge()
		{
			Rect target = new Rect(300, 200, 100, 100);

			ConnectionSide side = ConnectorRouter.ChooseTargetSide(new Point(100, 50), target, Offset);

			Assert.Equal(ConnectionSide.West, side);
		}

		[Fact]
		public void ChooseTargetSide_TargetBelowAndSlightlyRight_EntersThroughNorthEdge()
		{
			Rect target = new Rect(120, 400, 200, 100);

			ConnectionSide side = ConnectorRouter.ChooseTargetSide(new Point(100, 50), target, Offset);

			Assert.Equal(ConnectionSide.North, side);
		}

		[Fact]
		public void ChooseTargetSide_StartInsideTargetEast_PrefersEastPoint()
		{
			Rect target = new Rect(0, 0, 100, 100);

			ConnectionSide side = ConnectorRouter.ChooseTargetSide(new Point(100, Offset), target, Offset);

			Assert.Equal(ConnectionSide.East, side);
		}

		[Fact]
		public void Route_TargetToTheRight_UsesThreeSegmentsLikeLegacy()
		{
			Rect source = new Rect(0, 0, 100, 100);
			Rect target = new Rect(300, 200, 100, 100);

			ConnectorRoute route = this._router.Route(new Point(100, 50), source, target, Offset);

			Assert.Equal(ConnectionSide.West, route.TargetSide);
			Assert.Equal(3, route.Segments.Count);
			Assert.Equal(new RouteSegment(RouteDirection.Right, 100.0), route.Segments[0]);
			Assert.Equal(new RouteSegment(RouteDirection.Down, 182.0), route.Segments[1]);
			Assert.Equal(new RouteSegment(RouteDirection.Right, 100.0), route.Segments[2]);
			Assert.Equal(new Point(300, 232), route.End);
		}

		[Fact]
		public void Route_SelfLink_LeavesAndReentersThroughEastEdge()
		{
			Rect node = new Rect(0, 0, 200, 150);

			ConnectorRoute route = this._router.Route(new Point(200, 90), node, node, Offset);

			Assert.Equal(ConnectionSide.East, route.TargetSide);
			Assert.Equal(RouteDirection.Right, route.StartDirection);
			Assert.Equal(RouteDirection.Left, route.EndDirection);
			Assert.Equal(new Point(200, Offset), route.End);
		}

		[Fact]
		public void Route_TargetAboveLeft_EndsAtBottomEdgeTravellingUp()
		{
			Rect source = new Rect(500, 400, 200, 200);
			Rect target = new Rect(0, 0, 1000, 100);

			ConnectorRoute route = this._router.Route(new Point(700, 450), source, target, Offset);

			Assert.Equal(ConnectionSide.South, route.TargetSide);
			Assert.Equal(RouteDirection.Up, route.EndDirection);
			Assert.Equal(new Point(500, 100), route.End);
			AssertOrthogonal(route);
		}

		[Fact]
		public void ConnectorRoute_GetBounds_ContainsAllVertices()
		{
			ConnectorRoute route = new ConnectorRoute(
				new Point(10, 10),
				new[] { new RouteSegment(RouteDirection.Right, 20), new RouteSegment(RouteDirection.Down, 30), new RouteSegment(RouteDirection.Left, 5) },
				ConnectionSide.East);

			Assert.Equal(new Rect(10, 10, 20, 30), route.GetBounds());
			Assert.Equal(new Point(25, 40), route.End);
		}

		[Fact]
		public void ConnectorRoute_WithoutSegments_Throws()
		{
			Assert.Throws<ArgumentException>(() => new ConnectorRoute(new Point(0, 0), new List<RouteSegment>(), ConnectionSide.East));
		}

		[Fact]
		public void Route_RandomLayouts_AlwaysEndOnTargetSideWithMatchingDirection()
		{
			Random random = new Random(12345);
			for (Int32 i = 0; i < 5000; i++)
			{
				Rect source = RandomRect(random);
				Rect target = random.Next(10) == 0 ? source : RandomRect(random);
				Double offset = 10.0 + random.NextDouble() * Math.Min(60.0, target.Height - 10.0);
				Point start = new Point(source.Right, source.Top + random.NextDouble() * source.Height);

				ConnectorRoute route = this._router.Route(start, source, target, offset);

				Point expected = ConnectorRouter.GetConnectionPoint(target, route.TargetSide, offset);
				Assert.True(Math.Abs(route.End.X - expected.X) < 0.01 && Math.Abs(route.End.Y - expected.Y) < 0.01, $"case {i}: end {route.End} expected {expected}");
				Assert.Equal(RouteDirection.Right, route.StartDirection);
				RouteDirection arrival = route.TargetSide switch
				{
					ConnectionSide.North => RouteDirection.Down,
					ConnectionSide.South => RouteDirection.Up,
					ConnectionSide.East => RouteDirection.Left,
					_ => RouteDirection.Right
				};
				Assert.Equal(arrival, route.EndDirection);
				AssertOrthogonal(route);
			}
		}

		private static Rect RandomRect(Random random)
		{
			return new Rect(random.Next(0, 1500), random.Next(0, 1500), random.Next(100, 400), random.Next(40, 500));
		}

		private static void AssertOrthogonal(ConnectorRoute route)
		{
			for (Int32 i = 1; i < route.Points.Count; i++)
			{
				Point a = route.Points[i - 1];
				Point b = route.Points[i];
				Assert.True(Math.Abs(a.X - b.X) < 0.0001 || Math.Abs(a.Y - b.Y) < 0.0001, $"segment {i} is diagonal: {a} -> {b}");
			}
		}
	}
}
