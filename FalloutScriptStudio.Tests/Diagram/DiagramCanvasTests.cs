namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;

	public sealed class DiagramCanvasTests
	{
		private static DiagramNode CreateNode(String name, Double x = 0, Double y = 0)
		{
			DiagramNode node = new DiagramNode(name) { X = x, Y = y };
			node.HeaderLines.Add("talk_p_proc");
			node.AddRow(DiagramRowKind.Code, "start_gdialog(NPC_MSG, self_obj, 4, -1, -1);");
			node.AddRow(DiagramRowKind.Reply, "\"Hello there, stranger.\"", "Reply(100);");
			return node;
		}

		[Fact]
		public void AddItem_NodeGetsAutomaticWidthAndMeasuredHeight()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");

				canvas.AddItem(node);

				Assert.InRange(node.Width, 100.0, 350.0);
				Assert.True(node.Height > 32.0);
				Assert.Single(canvas.Items);
			});
		}

		[Fact]
		public void AddItem_SameItemTwice_Throws()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");
				canvas.AddItem(node);

				Assert.Throws<InvalidOperationException>(() => canvas.AddItem(node));
			});
		}

		[Fact]
		public void Links_AreResolvedByNameIgnoringCase_AndFollowRenameAndRemoval()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("node001");
				a.AddRow(DiagramRowKind.Option, "Go", "NOption(1, Node002, 4);", "NODE002");
				a.AddRow(DiagramRowKind.Option, "Nowhere", null, "node404");
				DiagramNode b = CreateNode("node002");
				canvas.AddItem(a);
				canvas.AddItem(b);

				Assert.Single(canvas.Links);
				Assert.Same(a, canvas.Links[0].Source);
				Assert.Same(b, canvas.Links[0].Target);

				b.Name = "node003";
				Assert.Empty(canvas.Links);

				b.Name = "Node002";
				Assert.Single(canvas.Links);

				canvas.RemoveItem(b);
				Assert.Empty(canvas.Links);
			});
		}

		[Fact]
		public void FindNode_IsCaseInsensitive_AndIncludesHiddenNodes()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("Node001");
				canvas.AddItem(node);
				node.IsHidden = true;

				Assert.Same(node, canvas.FindNode("node001"));
				Assert.True(canvas.ContainsNode("NODE001"));
				Assert.False(canvas.ContainsNode("other"));
			});
		}

		[Fact]
		public void HitTestWorld_ReportsTitleRowButtonsAndEmptySpace()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001", 100, 100);
				canvas.AddItem(node);
				canvas.Refresh();

				DiagramHitTestResult title = canvas.HitTestWorld(new Point(node.X + (node.Width / 2.0), node.Y + 16));
				DiagramHitTestResult collapse = canvas.HitTestWorld(new Point(node.X + node.Width - 18, node.Y + 16));
				DiagramHitTestResult nothing = canvas.HitTestWorld(new Point(5, 5));

				Assert.Equal(DiagramHitPart.Title, title.Part);
				Assert.Same(node, title.Item);
				Assert.Equal(DiagramHitPart.CollapseButton, collapse.Part);
				Assert.Equal(DiagramHitPart.None, nothing.Part);
				Assert.Null(nothing.Item);

				Rect replyBounds = canvas.GetRowViewportBounds(node.Rows[1]);
				Assert.False(replyBounds.IsEmpty);
				Point inside = canvas.ViewportToWorld(new Point(replyBounds.X + (replyBounds.Width / 2.0), replyBounds.Y + (replyBounds.Height / 2.0)));
				DiagramHitTestResult row = canvas.HitTestWorld(inside);
				Assert.Equal(DiagramHitPart.Row, row.Part);
				Assert.Same(node.Rows[1], row.Row);

				Point toggle = canvas.ViewportToWorld(new Point(replyBounds.X + 8, replyBounds.Y + (replyBounds.Height / 2.0)));
				Assert.Equal(DiagramHitPart.RowToggle, canvas.HitTestWorld(toggle).Part);
			});
		}

		[Fact]
		public void HiddenItems_AreNotHitTested()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001", 100, 100);
				canvas.AddItem(node);
				Point inside = new Point(node.X + 20, node.Y + 20);
				Assert.Same(node, canvas.HitTestWorld(inside).Item);

				node.IsHidden = true;

				Assert.Null(canvas.HitTestWorld(inside).Item);
				Assert.True(canvas.GetContentBounds().IsEmpty);
			});
		}

		[Fact]
		public void Select_RaisesEventsInOrder_AndBringsItemToFront()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				DiagramNode b = CreateNode("b");
				canvas.AddItem(a);
				canvas.AddItem(b);
				List<String> log = new List<String>();
				canvas.ItemSelected += (s, e) => log.Add("sel:" + e.Node?.Name);
				canvas.ItemUnselected += (s, e) => log.Add("unsel:" + e.Node?.Name);
				canvas.SelectionChanged += (s, e) => log.Add("changed");

				canvas.Select(a);
				canvas.Select(b);
				canvas.ClearSelection();

				Assert.Equal(new[] { "sel:a", "changed", "unsel:a", "sel:b", "changed", "unsel:b", "changed" }, log);
				Assert.Same(b, canvas.Items[canvas.Items.Count - 1]);
				canvas.Select(a);
				Assert.Same(a, canvas.Items[canvas.Items.Count - 1]);
				Assert.Same(a, canvas.SelectedItem);
				Assert.True(a.IsSelected);
				Assert.False(b.IsSelected);
			});
		}

		[Fact]
		public void SelectAdditive_KeepsPreviousSelection_AndSelectAllSkipsHidden()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				DiagramNode b = CreateNode("b");
				DiagramNode c = CreateNode("c") ;
				canvas.AddItem(a);
				canvas.AddItem(b);
				canvas.AddItem(c);
				c.IsHidden = true;

				canvas.Select(a);
				canvas.Select(b, additive: true);
				Assert.Equal(2, canvas.SelectedItems.Count);

				canvas.ClearSelection();
				canvas.SelectAll();
				Assert.Equal(2, canvas.SelectedItems.Count);
				Assert.False(c.IsSelected);
			});
		}

		[Fact]
		public void HidingSelectedItem_DeselectsIt()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				canvas.AddItem(a);
				canvas.Select(a);
				Int32 unselected = 0;
				canvas.ItemUnselected += (s, e) => unselected++;

				a.IsHidden = true;

				Assert.Equal(1, unselected);
				Assert.Empty(canvas.SelectedItems);
			});
		}

		[Fact]
		public void RemovingUnselectedItem_DoesNotRaiseUnselected()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				canvas.AddItem(a);
				Int32 unselected = 0;
				canvas.ItemUnselected += (s, e) => unselected++;

				canvas.RemoveItem(a);

				Assert.Equal(0, unselected);
			});
		}

		[Fact]
		public void IsRowTextTrimmed_ReflectsNodeWidth()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = new DiagramNode("n") { Width = 120 };
				DiagramRow longRow = node.AddRow(DiagramRowKind.Option, new String('w', 300));
				DiagramRow shortRow = node.AddRow(DiagramRowKind.Option, "ok");
				DiagramRow longCode = node.AddRow(DiagramRowKind.Code, new String('x', 300));
				canvas.AddItem(node);

				Assert.True(canvas.IsRowTextTrimmed(longRow));
				Assert.False(canvas.IsRowTextTrimmed(shortRow));
				Assert.True(canvas.IsRowTextTrimmed(longCode));

				node.Width = 100000;
				Assert.False(canvas.IsRowTextTrimmed(longRow));
			});
		}

		[Fact]
		public void GetItemBounds_ForForeignItem_ReturnsItsOwnBounds()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNote note = new DiagramNote { X = 5, Y = 6 };

				Assert.Equal(new Rect(5, 6, 200, 50), canvas.GetItemBounds(note));
			});
		}

		[Fact]
		public void RemovingSelectedItem_RaisesUnselected()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				canvas.AddItem(a);
				canvas.Select(a);
				Int32 unselected = 0;
				canvas.ItemUnselected += (s, e) => unselected++;

				canvas.RemoveItem(a);

				Assert.Equal(1, unselected);
				Assert.Empty(canvas.SelectedItems);
				Assert.False(a.IsSelected);
			});
		}

		[Fact]
		public void CollapseAndExpand_ChangeNodeHeights()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");
				canvas.AddItem(node);
				canvas.Refresh();
				Double expanded = node.Height;

				canvas.CollapseAllNodes();
				canvas.Refresh();
				Double collapsed = node.Height;
				canvas.ExpandAllNodes();
				canvas.Refresh();

				Assert.True(collapsed < expanded);
				Assert.Equal(expanded, node.Height, 3);
			});
		}

		[Fact]
		public void ExpandAllRows_GrowsNode_AndCollapseAllRowsRestoresIt()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");
				canvas.AddItem(node);
				canvas.Refresh();
				Double before = node.Height;

				canvas.ExpandAllRows();
				canvas.Refresh();
				Double expanded = node.Height;
				canvas.CollapseAllRows();
				canvas.Refresh();

				Assert.True(expanded > before);
				Assert.Equal(before, node.Height, 3);
			});
		}

		[Fact]
		public void ShowCodeRowsFalse_HidesCodeRows()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");
				canvas.AddItem(node);
				canvas.Refresh();
				Double withCode = node.Height;

				node.ShowCodeRows = false;
				canvas.Refresh();

				Assert.True(node.Height < withCode);
				Assert.True(canvas.GetRowViewportBounds(node.Rows[0]).IsEmpty);
				Assert.False(canvas.GetRowViewportBounds(node.Rows[1]).IsEmpty);
			});
		}

		[Fact]
		public void GetRowViewportBounds_IsEmptyForCollapsedNode()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = CreateNode("node001");
				canvas.AddItem(node);

				node.IsCollapsed = true;

				Assert.True(canvas.GetRowViewportBounds(node.Rows[1]).IsEmpty);
			});
		}

		[Fact]
		public void MatchAndShrinkAllWidths_UseContentWidth()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode node = new DiagramNode("n");
				node.AddRow(DiagramRowKind.Code, new String('x', 200));
				canvas.AddItem(node);
				Assert.Equal(350.0, node.Width, 3);

				canvas.MatchAllWidths();
				Double matched = node.Width;
				canvas.ShrinkAllWidths();

				Assert.True(matched > 350.0);
				Assert.Equal(350.0, node.Width, 3);
			});
		}

		[Fact]
		public void AutoArrange_PlacesLinkedNodesLeftToRight()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				a.AddRow(DiagramRowKind.Option, "x", null, "b");
				DiagramNode b = CreateNode("b");
				b.AddRow(DiagramRowKind.Option, "y", null, "c");
				DiagramNode c = CreateNode("c");
				DiagramNode lonely = CreateNode("lonely");
				foreach (DiagramNode n in new[] { c, b, a, lonely })
				{
					canvas.AddItem(n);
				}

				canvas.AutoArrange();

				Assert.True(a.X < b.X && b.X < c.X);
				Assert.True(lonely.Y > a.Y);
				foreach (DiagramNode n1 in canvas.Nodes)
				{
					foreach (DiagramNode n2 in canvas.Nodes.Where(n => n != n1))
					{
						Assert.False(canvas.GetItemBounds(n1).IntersectsWith(canvas.GetItemBounds(n2)));
					}
				}
			});
		}

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
		public void ReplaceItem_KeepsZOrderAndSelection()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				DiagramNode a = CreateNode("a");
				DiagramNode b = CreateNode("b");
				DiagramNode c = CreateNode("c");
				canvas.AddItem(a);
				canvas.AddItem(b);
				canvas.AddItem(c);
				canvas.Select(b);
				canvas.BringToFront(a);
				DiagramNode b2 = CreateNode("b");

				canvas.ReplaceItem(b, b2);

				Assert.Equal(3, canvas.Items.Count);
				Assert.Same(c, canvas.Items[0]);
				Assert.Same(b2, canvas.Items[1]);
				Assert.Same(a, canvas.Items[2]);
				Assert.True(b2.IsSelected);
				Assert.DoesNotContain(b, canvas.Items);
			});
		}

		[Fact]
		public void Clear_RemovesEverything()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("a"));
				canvas.AddItem(new DiagramNote());
				canvas.Select(canvas.Items[0]);

				canvas.Clear();

				Assert.Empty(canvas.Items);
				Assert.Empty(canvas.SelectedItems);
				Assert.True(canvas.GetContentBounds().IsEmpty);
			});
		}

		[Fact]
		public void RenderToBitmap_ProducesDrawnImage()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("node001", 20, 20));
				canvas.AddItem(new DiagramNote { Text = "note", X = 400, Y = 40 });

				BitmapSource bitmap = canvas.RenderToBitmap(1.0, 10.0);

				Assert.True(bitmap.PixelWidth > 400);
				Assert.True(bitmap.PixelHeight > 80);
				Byte[] pixels = new Byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
				bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
				Boolean differs = false;
				for (Int32 i = 4; i < pixels.Length && !differs; i += 4)
				{
					differs = pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] || pixels[i + 2] != pixels[2];
				}

				Assert.True(differs);
			});
		}

		[Fact]
		public void SaveToPng_WritesPngSignature()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();
				canvas.AddItem(CreateNode("node001", 20, 20));
				using (MemoryStream stream = new MemoryStream())
				{
					canvas.SaveToPng(stream);

					Byte[] data = stream.ToArray();
					Assert.True(data.Length > 100);
					Assert.Equal(new Byte[] { 0x89, 0x50, 0x4E, 0x47 }, data.Take(4).ToArray());
				}
			});
		}

		[Fact]
		public void RenderToBitmap_EmptyCanvas_StillReturnsImage()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas();

				BitmapSource bitmap = canvas.RenderToBitmap();

				Assert.True(bitmap.PixelWidth > 0);
				Assert.True(bitmap.PixelHeight > 0);
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

		[Fact]
		public void ThemeFallback_WorksWithoutFluentResources()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new DiagramCanvas { LowDetail = true, HighQuality = false };
				canvas.AddItem(CreateNode("node001", 20, 20));

				BitmapSource bitmap = canvas.RenderToBitmap();

				Assert.True(bitmap.PixelWidth > 0);
			});
		}
	}
}
