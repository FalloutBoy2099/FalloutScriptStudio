namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Layout;
	using Xunit;

	public sealed class LayeredGraphLayoutTests
	{
		private readonly LayeredGraphLayout _layout = new LayeredGraphLayout();

		[Fact]
		public void Arrange_NoBoxes_ReturnsEmpty()
		{
			IReadOnlyList<Point> result = this._layout.Arrange(new List<Size>(), new List<ArrangeEdge>());

			Assert.Empty(result);
		}

		[Fact]
		public void Arrange_Chain_PlacesEachBoxRightOfItsPredecessor()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(200, 100), 4).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(0, 1), new ArrangeEdge(1, 2), new ArrangeEdge(2, 3) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.True(p[0].X < p[1].X && p[1].X < p[2].X && p[2].X < p[3].X);
			Assert.True(p[1].X - p[0].X >= 200.0 + 90.0 - 0.001);
			Assert.All(p, point => Assert.Equal(40.0, point.Y));
		}

		[Fact]
		public void Arrange_Cycle_TerminatesAndKeepsStartAtLeft()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(150, 80), 3).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(0, 1), new ArrangeEdge(1, 2), new ArrangeEdge(2, 0), new ArrangeEdge(1, 1) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.True(p[0].X < p[1].X);
			Assert.True(p[1].X < p[2].X);
		}

		[Fact]
		public void Arrange_PureCycleWithoutRoot_StillProducesLayers()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(100, 50), 2).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(0, 1), new ArrangeEdge(1, 0) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.NotEqual(p[0].X, p[1].X);
		}

		[Fact]
		public void Arrange_Branching_StacksSiblingsInTheSameColumn()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(100, 60), 4).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(0, 1), new ArrangeEdge(0, 2), new ArrangeEdge(0, 3) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.Equal(p[1].X, p[2].X);
			Assert.Equal(p[2].X, p[3].X);
			Assert.True(p[2].Y >= p[1].Y + 60.0 + 30.0 - 0.001);
			Assert.True(p[3].Y >= p[2].Y + 60.0 + 30.0 - 0.001);
		}

		[Fact]
		public void Arrange_UnlinkedBoxes_AreParkedBelowTheGraph()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(100, 60), 4).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(0, 1) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.True(p[2].Y > p[0].Y + 60.0);
			Assert.True(p[3].Y > p[0].Y + 60.0);
			Assert.NotEqual(p[2].X, p[3].X);
		}

		[Fact]
		public void Arrange_IgnoresInvalidEdges()
		{
			List<Size> sizes = Enumerable.Repeat(new Size(100, 60), 2).ToList();
			ArrangeEdge[] edges = { new ArrangeEdge(-1, 0), new ArrangeEdge(0, 7), new ArrangeEdge(1, 1) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			Assert.Equal(2, p.Count);
		}

		[Fact]
		public void Arrange_IsDeterministic()
		{
			Random random = new Random(7);
			List<Size> sizes = Enumerable.Range(0, 60).Select(_ => new Size(random.Next(100, 350), random.Next(40, 600))).ToList();
			List<ArrangeEdge> edges = Enumerable.Range(0, 90).Select(_ => new ArrangeEdge(random.Next(60), random.Next(60))).ToList();

			IReadOnlyList<Point> a = this._layout.Arrange(sizes, edges);
			IReadOnlyList<Point> b = this._layout.Arrange(sizes, edges);

			Assert.Equal(a, b);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		public void Arrange_RandomGraphs_NeverOverlap(Int32 seed)
		{
			Random random = new Random(seed);
			Int32 n = 80;
			List<Size> sizes = Enumerable.Range(0, n).Select(_ => new Size(random.Next(100, 350), random.Next(40, 600))).ToList();
			List<ArrangeEdge> edges = Enumerable.Range(0, 110).Select(_ => new ArrangeEdge(random.Next(n), random.Next(n))).ToList();

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, edges);

			for (Int32 i = 0; i < n; i++)
			{
				Rect a = new Rect(p[i], sizes[i]);
				for (Int32 j = i + 1; j < n; j++)
				{
					Rect b = new Rect(p[j], sizes[j]);
					Assert.False(a.IntersectsWith(b), $"boxes {i} and {j} overlap: {a} / {b}");
				}
			}
		}

		[Fact]
		public void Arrange_HonoursOrigin()
		{
			List<Size> sizes = new List<Size> { new Size(100, 60) };

			IReadOnlyList<Point> p = this._layout.Arrange(sizes, new List<ArrangeEdge>(), new DiagramArrangeOptions { OriginX = 5, OriginY = 7 });

			Assert.Equal(new Point(5, 7), p[0]);
		}
	}
}
