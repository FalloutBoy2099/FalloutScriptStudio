namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;
	using static FalloutScriptStudio.Tests.Diagram.DiagramCanvasTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Diagram.DiagramCanvasTests")]
	public sealed class DiagramCanvasViewportTests
	{
		[Fact]
		public void Zoom_IsClampedAndRaisesChangedEvent()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				Int32 changed = 0;
				canvas.ZoomChanged += (s, e) => changed++;

				canvas.Zoom = 10.0;
				Assert.Equal(4.0, canvas.Zoom);
				canvas.Zoom = 0.001;
				Assert.Equal(0.1, canvas.Zoom);
				canvas.ResetZoom();
				canvas.ZoomIn();

				Assert.Equal(4, changed);
				Assert.Equal(1.1, canvas.Zoom, 6);
			});
		}

		[Fact]
		public void ViewportConversions_RoundTripAtAnyZoom()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("a", 200, 300));
				canvas.Zoom = 0.5;

				Point world = new Point(321, 123);
				Point back = canvas.ViewportToWorld(canvas.WorldToViewport(world));

				Assert.Equal(world.X, back.X, 6);
				Assert.Equal(world.Y, back.Y, 6);
			});
		}

		[Fact]
		public void LayoutChanged_IsRaisedOnceAfterRefresh()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.Refresh();
				Int32 raised = 0;
				canvas.LayoutChanged += (s, e) => raised++;

				DiagramNode node = CreateNode("a");
				canvas.AddItem(node);
				node.X = 50;
				node.Y = 60;
				node.IsCollapsed = true;
				canvas.Refresh();

				Assert.Equal(1, raised);
			});
		}

		[Fact]
		public void ScrollToItem_ScrollsFarItemIntoView()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode far = CreateNode("far", 3000, 2000);
				canvas.AddItem(CreateNode("near", 20, 20));
				canvas.AddItem(far);
				canvas.Measure(new Size(800, 600));
				canvas.Arrange(new Rect(0, 0, 800, 600));
				canvas.UpdateLayout();

				canvas.ScrollToItem(far);

				Assert.True(canvas.HorizontalOffset > 2000.0);
				Assert.True(canvas.VerticalOffset > 1000.0);
				Point center = canvas.WorldToViewport(new Point(far.X + (far.Width / 2.0), far.Y + (far.Height / 2.0)));
				Assert.InRange(center.X, 0.0, 800.0);
				Assert.InRange(center.Y, 0.0, 600.0);
			});
		}

		[Fact]
		public void ZoomAt_KeepsAnchorWorldPointFixed()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("near", 20, 20));
				canvas.AddItem(CreateNode("far", 3000, 2000));
				canvas.Measure(new Size(800, 600));
				canvas.Arrange(new Rect(0, 0, 800, 600));
				canvas.UpdateLayout();
				canvas.ScrollToOffset(500, 300);
				Point anchor = new Point(400, 300);
				Point before = canvas.ViewportToWorld(anchor);

				canvas.ZoomAt(anchor, 2.0);
				Point after = canvas.ViewportToWorld(anchor);

				Assert.Equal(before.X, after.X, 3);
				Assert.Equal(before.Y, after.Y, 3);
			});
		}

		[Fact]
		public void RenderedControl_FollowsZoom()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas { ShowGrid = false };
				DiagramNode node = CreateNode("node001", 20, 20);
				canvas.AddItem(node);
				canvas.Measure(new Size(900, 500));
				canvas.Arrange(new Rect(0, 0, 900, 500));
				canvas.UpdateLayout();

				Int32 right1 = RightmostNonBackgroundColumn(canvas);
				canvas.Zoom = 2.0;
				canvas.ScrollToOffset(0, 0);
				Int32 right2 = RightmostNonBackgroundColumn(canvas);

				Double expected1 = node.X + node.Width;
				Assert.InRange(right1, expected1 - 3, expected1 + 12);
				Assert.InRange(right2, (expected1 * 2) - 6, (expected1 * 2) + 24);
			});
		}

		private static Int32 RightmostNonBackgroundColumn(DiagramCanvas canvas)
		{
			canvas.Refresh();
			RenderTargetBitmap bitmap = new RenderTargetBitmap(900, 500, 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(canvas);
			Byte[] pixels = new Byte[900 * 500 * 4];
			bitmap.CopyPixels(pixels, 900 * 4, 0);
			Int32 right = 0;
			for (Int32 y = 0; y < 440; y++)
			{
				for (Int32 x = 0; x < 850; x++)
				{
					Int32 i = ((y * 900) + x) * 4;
					if (pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] || pixels[i + 2] != pixels[2])
					{
						right = Math.Max(right, x);
					}
				}
			}

			return right;
		}
	}
}
