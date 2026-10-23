namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Tests.Support;
	using Xunit;

	/// <summary>The badge and the ring of a node with a problem, and the faded node: what is drawn follows the properties of the node, and only them.</summary>
	public sealed class DiagramIssueMarkTests
	{
		private const Int32 Width = 700;
		private const Int32 Height = 400;

		private static DiagramNode CreateNode(String name, Double x)
		{
			DiagramNode node = new(name) { X = x, Y = 40 };
			node.HeaderLines.Add("from talk_p_proc");
			node.AddRow(DiagramRowKind.Reply, "\"Hello there, stranger.\"", "Reply(100);");
			node.AddRow(DiagramRowKind.Option, "Tell me more.", "NOption(101, Node002, 4);", "Node002");
			return node;
		}

		private static Byte[] Render(DiagramCanvas canvas)
		{
			canvas.Refresh();
			RenderTargetBitmap bitmap = new(Width, Height, 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(canvas);
			Byte[] pixels = new Byte[Width * Height * 4];
			bitmap.CopyPixels(pixels, Width * 4, 0);
			return pixels;
		}

		private static void Run(Action<DiagramCanvas, DiagramNode, DiagramNode> body)
		{
			StaTestRunner.Run(() =>
			{
				DiagramCanvas canvas = new();
				Window window = OffscreenWindow.Show(canvas, Width, Height);
				try
				{
					canvas.UpdateLayout();
					DiagramNode first = CreateNode("Node001", 30);
					DiagramNode second = CreateNode("Node002", 360);
					canvas.AddItem(first);
					canvas.AddItem(second);
					body(canvas, first, second);
				}
				finally
				{
					canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
					window.Content = null;
					window.Close();
				}
			});
		}

		[Fact]
		public void A_node_without_a_problem_is_drawn_as_before()
		{
			Run((canvas, first, _) =>
			{
				Byte[] plain = Render(canvas);

				first.IssueText = "text without a severity";
				Byte[] withText = Render(canvas);

				Assert.True(plain.Any(b => b != 0), "nothing was drawn");
				Assert.True(plain.AsSpan().SequenceEqual(withText), "a tool tip text alone changes the drawing");
				Assert.Equal(DiagramIssueSeverity.None, first.IssueSeverity);
				Assert.False(first.IsDimmed);
			});
		}

		[Fact]
		public void Each_severity_has_its_own_badge()
		{
			Run((canvas, first, _) =>
			{
				Byte[] plain = Render(canvas);
				Byte[][] marked = new Byte[3][];
				DiagramIssueSeverity[] severities = { DiagramIssueSeverity.Info, DiagramIssueSeverity.Warning, DiagramIssueSeverity.Error };
				for (Int32 i = 0; i < severities.Length; i++)
				{
					first.IssueSeverity = severities[i];
					marked[i] = Render(canvas);
					Assert.False(marked[i].AsSpan().SequenceEqual(plain), severities[i] + " is not drawn");
				}

				Assert.False(marked[0].AsSpan().SequenceEqual(marked[1]), "the badges of a note and of a warning look alike");
				Assert.False(marked[1].AsSpan().SequenceEqual(marked[2]), "the badges of a warning and of an error look alike");
				Assert.False(marked[0].AsSpan().SequenceEqual(marked[2]), "the badges of a note and of an error look alike");

				first.IssueSeverity = DiagramIssueSeverity.None;
				Assert.True(Render(canvas).AsSpan().SequenceEqual(plain), "taking the mark away does not restore the drawing");
			});
		}

		[Fact]
		public void Only_the_marked_node_changes()
		{
			Run((canvas, first, second) =>
			{
				Byte[] plain = Render(canvas);
				first.IssueSeverity = DiagramIssueSeverity.Error;
				Byte[] marked = Render(canvas);

				Int32 changedColumns = Enumerable.Range(0, Width)
					.Count(x => Enumerable.Range(0, Height).Any(y => Differs(plain, marked, (y * Width * 4) + (x * 4))));
				Assert.InRange(changedColumns, 1, (Int32)second.X);
			});
		}

		[Fact]
		public void A_dimmed_node_is_fainter_and_a_node_that_is_both_dimmed_and_marked_keeps_its_badge()
		{
			Run((canvas, first, _) =>
			{
				Byte[] plain = Render(canvas);
				first.IsDimmed = true;
				Byte[] dimmed = Render(canvas);
				first.IssueSeverity = DiagramIssueSeverity.Warning;
				Byte[] both = Render(canvas);
				first.IsDimmed = false;
				Byte[] badgeOnly = Render(canvas);

				Assert.False(dimmed.AsSpan().SequenceEqual(plain), "dimming does not change the drawing");
				Assert.False(both.AsSpan().SequenceEqual(dimmed), "the badge of a dimmed node is not drawn");
				Assert.False(both.AsSpan().SequenceEqual(badgeOnly), "dimming a marked node does not change the drawing");
				first.IssueSeverity = DiagramIssueSeverity.None;
				Assert.True(Render(canvas).AsSpan().SequenceEqual(plain), "the canvas does not come back to the plain drawing");
			});
		}

		private static Boolean Differs(Byte[] first, Byte[] second, Int32 offset)
		{
			return first[offset] != second[offset] || first[offset + 1] != second[offset + 1] || first[offset + 2] != second[offset + 2] || first[offset + 3] != second[offset + 3];
		}
	}
}