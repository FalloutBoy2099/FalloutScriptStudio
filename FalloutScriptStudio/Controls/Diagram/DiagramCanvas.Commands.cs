namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Linq;
	using Model;
	using Rendering;
	using Internal;
	using Layout;

	// Bulk commands on all nodes: collapse, expand, widths and automatic arrangement.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Collapses every node so that only its header is visible.
		/// </summary>
		public void CollapseAllNodes()
		{
			this.SetNodeCollapsed(true);
		}

		/// <summary>
		/// Expands every node.
		/// </summary>
		public void ExpandAllNodes()
		{
			this.SetNodeCollapsed(false);
		}

		/// <summary>
		/// Collapses the detail line of every row of every node (the legacy "collapse all contents").
		/// </summary>
		public void CollapseAllRows()
		{
			this.SetRowsExpanded(false);
		}

		/// <summary>
		/// Expands the detail line of every row of every node (the legacy "expand all contents").
		/// </summary>
		public void ExpandAllRows()
		{
			this.SetRowsExpanded(true);
		}

		/// <summary>
		/// Gives every node the width its content wants (the legacy "match all widths").
		/// </summary>
		public void MatchAllWidths()
		{
			this.ApplyToNodeWidths(Double.MaxValue);
		}

		/// <summary>
		/// Gives every node the width its content wants, limited to <paramref name="maxWidth"/> (the legacy "shrink all widths").
		/// </summary>
		/// <param name="maxWidth">Upper limit; defaults to 350 units like the legacy editor.</param>
		public void ShrinkAllWidths(Double maxWidth = DiagramMetrics.AutoWidthLimit)
		{
			this.ApplyToNodeWidths(maxWidth);
		}

		/// <summary>
		/// Arranges the visible nodes in left-to-right layers following their links (see <see cref="LayeredGraphLayout"/>).
		/// Notes are left where they are.
		/// </summary>
		/// <param name="options">Spacing and origin, or <c>null</c> for the defaults.</param>
		public void AutoArrange(DiagramArrangeOptions? options = null)
		{
			DiagramAutoArranger.Arrange(this._scene, options);
		}

		private void SetNodeCollapsed(Boolean collapsed)
		{
			foreach (DiagramNode node in this.Nodes.ToList())
			{
				node.IsCollapsed = collapsed;
			}
		}

		private void SetRowsExpanded(Boolean expanded)
		{
			foreach (DiagramNode node in this.Nodes.ToList())
			{
				foreach (DiagramRow row in node.Rows)
				{
					if (row.IsExpandable)
					{
						row.IsExpanded = expanded;
					}
				}
			}
		}

		private void ApplyToNodeWidths(Double maxWidth)
		{
			this._scene.EnsureLayouts();
			foreach (DiagramNode node in this.Nodes.ToList())
			{
				node.Width = Math.Min(this._scene.GetLayout(node).ContentWidth, maxWidth);
			}
		}
	}
}