namespace FalloutScriptStudio.Tests.Diagram
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;

	/// <summary>
	/// The canvas keeps the drawing of the content of every node and the measurements of its texts, and draws only the frame again when a
	/// node is selected or unselected. What is seen must not depend on what happened before.
	/// </summary>
	public sealed class DiagramCachedRenderingTests
	{
		private static List<DiagramNode> CreateNodes()
		{
			List<DiagramNode> nodes = new List<DiagramNode>();
			for (Int32 i = 0; i < 12; i++)
			{
				DiagramNode node = new DiagramNode("node" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)) { X = 30 + ((i % 4) * 330), Y = 30 + ((i / 4) * 250) };
				node.HeaderLines.Add(i == 0 ? "talk_p_proc" : "from node000");
				node.AddRow(DiagramRowKind.Code, "start_gdialog(NPC_MSG, self_obj, 4, -1, -1);");
				node.AddRow(DiagramRowKind.Reply, "\"Hello there, stranger. This is a reply that is long enough to wrap onto a second line of the node.\"", "Reply(" + (100 + i) + ");");
				node.AddRow(DiagramRowKind.Option, "Tell me more about " + i, "NOption(" + (200 + i) + ", node" + (((i + 1) % 12).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)) + ", 4);", "node" + (((i + 1) % 12).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)));
				node.AddRow(DiagramRowKind.Call, "call node" + (((i + 5) % 12).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)) + ";", null, "node" + (((i + 5) % 12).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)));
				nodes.Add(node);
			}

			return nodes;
		}

		private static Byte[] Render(DiagramCanvas canvas)
		{
			canvas.Refresh();
			Int32 width = 1400;
			Int32 height = 800;
			RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(canvas);
			Byte[] pixels = new Byte[width * height * 4];
			bitmap.CopyPixels(pixels, width * 4, 0);
			return pixels;
		}

		private static Window Host(DiagramCanvas canvas)
		{
			Window window = OffscreenWindow.Show(canvas, 1400, 800);
			canvas.UpdateLayout();
			return window;
		}

		[Fact]
		public void TheDrawingDoesNotDependOnTheSelectionsAndTheCollapsingThatCameBefore()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas worked = new DiagramCanvas();
				Window workedWindow = Host(worked);
				List<DiagramNode> nodes = CreateNodes();
				foreach (DiagramNode node in nodes)
				{
					worked.AddItem(node);
				}

				worked.Refresh();
				worked.SelectAll();
				worked.Refresh();
				worked.ClearSelection();
				worked.Refresh();
				worked.CollapseAllNodes();
				worked.Refresh();
				worked.Select(nodes[3]);
				worked.Refresh();
				worked.ExpandAllNodes();
				worked.Refresh();
				worked.CollapseAllRows();
				worked.Refresh();
				worked.ExpandAllRows();
				worked.Refresh();
				nodes[5].X += 40;
				worked.Refresh();
				nodes[5].X -= 40;
				worked.ClearSelection();
				Byte[] afterHistory = Render(worked);

				DiagramCanvas fresh = new DiagramCanvas();
				Window freshWindow = Host(fresh);
				foreach (DiagramNode node in CreateNodes())
				{
					fresh.AddItem(node);
				}

				fresh.ExpandAllRows();
				Byte[] direct = Render(fresh);

				Assert.True(afterHistory.Any(b => b != 0), "nothing was drawn");
				Assert.True(afterHistory.AsSpan().SequenceEqual(direct), "the canvas that went through selections and collapsing looks different from a new one");

				worked.Select(nodes[2]);
				Byte[] selectedWorked = Render(worked);
				fresh.Select(fresh.Nodes.ElementAt(2));
				Byte[] selectedFresh = Render(fresh);
				Assert.True(selectedWorked.AsSpan().SequenceEqual(selectedFresh), "a selected node looks different after the history");
				Assert.False(selectedWorked.AsSpan().SequenceEqual(afterHistory), "selecting a node changes the drawing");

				worked.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				workedWindow.Content = null;
				workedWindow.Close();
				fresh.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				freshWindow.Content = null;
				freshWindow.Close();
			});
		}

		[Fact]
		public void ResizingAndChangingTheTextOfANode_ShowsTheNewTexts()
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas worked = new DiagramCanvas();
				Window workedWindow = Host(worked);
				List<DiagramNode> nodes = CreateNodes();
				foreach (DiagramNode node in nodes)
				{
					worked.AddItem(node);
				}

				Byte[] before = Render(worked);
				nodes[1].Width = 220;
				nodes[1].Rows[1].Text = "Changed text of the reply.";
				nodes[2].HeaderLines[0] = "from somewhere else";
				Byte[] changed = Render(worked);

				DiagramCanvas fresh = new DiagramCanvas();
				Window freshWindow = Host(fresh);
				List<DiagramNode> same = CreateNodes();
				same[1].Width = 220;
				same[1].Rows[1].Text = "Changed text of the reply.";
				same[2].HeaderLines[0] = "from somewhere else";
				foreach (DiagramNode node in same)
				{
					fresh.AddItem(node);
				}

				Byte[] expected = Render(fresh);

				Assert.False(before.AsSpan().SequenceEqual(changed));
				Assert.True(changed.AsSpan().SequenceEqual(expected), "the changed nodes are not drawn like new nodes with the same content");
				worked.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				workedWindow.Content = null;
				workedWindow.Close();
				fresh.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				freshWindow.Content = null;
				freshWindow.Close();
			});
		}
	}
}
