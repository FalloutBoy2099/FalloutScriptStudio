namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using Xunit;

	public sealed class DiagramModelTests
	{
		[Fact]
		public void AddRow_SetsOwnerAndRaisesRowsChanged()
		{
			DiagramNode node = new DiagramNode("node001");
			List<String?> changed = new List<String?>();
			node.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

			DiagramRow row = node.AddRow(DiagramRowKind.Reply, "Hello", "Reply(1);");

			Assert.Same(node, row.Owner);
			Assert.Contains(nameof(DiagramNode.Rows), changed);
		}

		[Fact]
		public void RemovedRow_LosesOwnerAndNoLongerNotifiesNode()
		{
			DiagramNode node = new DiagramNode("node001");
			DiagramRow row = node.AddRow(DiagramRowKind.Text, "x");
			node.Rows.Remove(row);
			Int32 count = 0;
			node.PropertyChanged += (s, e) => count++;

			row.Text = "changed";

			Assert.Null(row.Owner);
			Assert.Equal(0, count);
		}

		[Fact]
		public void RowPropertyChange_IsForwardedByOwnerNode()
		{
			DiagramNode node = new DiagramNode("node001");
			DiagramRow row = node.AddRow(DiagramRowKind.Option, "x", "detail");
			Int32 count = 0;
			node.PropertyChanged += (s, e) => count++;

			row.IsExpanded = true;

			Assert.Equal(1, count);
		}

		[Fact]
		public void Row_IsExpandable_OnlyWithDetail()
		{
			DiagramRow row = new DiagramRow(DiagramRowKind.Reply, "x");
			Assert.False(row.IsExpandable);

			row.Detail = "Reply(1);";

			Assert.True(row.IsExpandable);
			Assert.Equal(DiagramRowIcon.Reply, row.Icon);
		}

		[Fact]
		public void Node_Width_IsRaisedToMinimum_AndZeroMeansAutomatic()
		{
			DiagramNode node = new DiagramNode("n");

			node.Width = 40;
			Assert.Equal(100.0, node.Width);

			node.Width = 0;
			Assert.Equal(0.0, node.Width);
		}

		[Fact]
		public void Node_Height_CannotBeAssigned()
		{
			DiagramNode node = new DiagramNode("n");

			node.Height = 500;

			Assert.Equal(0.0, node.Height);
		}

		[Fact]
		public void Note_HasDefaultSizeAndMinimums()
		{
			DiagramNote note = new DiagramNote();
			Assert.Equal(200.0, note.Width);
			Assert.Equal(50.0, note.Height);

			note.Width = 10;
			note.Height = 5;

			Assert.Equal(80.0, note.Width);
			Assert.Equal(30.0, note.Height);
			Assert.True(note.CanResizeVertically);
		}

		[Fact]
		public void Coordinates_IgnoreNonFiniteValues()
		{
			DiagramNote note = new DiagramNote { X = Double.NaN, Y = Double.PositiveInfinity };

			Assert.Equal(0.0, note.X);
			Assert.Equal(0.0, note.Y);
		}
	}
}
