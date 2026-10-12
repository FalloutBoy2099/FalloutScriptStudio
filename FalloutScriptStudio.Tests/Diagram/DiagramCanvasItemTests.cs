namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;
	using static FalloutScriptStudio.Tests.Diagram.DiagramCanvasTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Diagram.DiagramCanvasTests")]
	public sealed class DiagramCanvasItemTests
	{
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
	}
}
