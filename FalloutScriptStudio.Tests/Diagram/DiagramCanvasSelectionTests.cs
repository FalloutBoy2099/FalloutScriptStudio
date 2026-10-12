namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;
	using static FalloutScriptStudio.Tests.Diagram.DiagramCanvasTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Diagram.DiagramCanvasTests")]
	public sealed class DiagramCanvasSelectionTests
	{
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
	}
}
