namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;
	using static FalloutScriptStudio.Tests.Diagram.DiagramCanvasTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Diagram.DiagramCanvasTests")]
	public sealed class DiagramCanvasLayoutTests
	{
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
	}
}