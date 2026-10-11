namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram.Layout;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Rendering;

	/// <summary>Positions the visible nodes of a scene in left-to-right layers following their links.</summary>
	internal static class DiagramAutoArranger
	{
		/// <summary>Moves the visible nodes (see <see cref="LayeredGraphLayout"/>); notes are left where they are.</summary>
		/// <param name="scene">The scene.</param>
		/// <param name="options">Spacing and origin, or <c>null</c> for the defaults.</param>
		public static void Arrange(DiagramScene scene, DiagramArrangeOptions? options)
		{
			scene.EnsureLayouts();
			List<DiagramNode> nodes = scene.Items.OfType<DiagramNode>().Where(n => !n.IsHidden).ToList();
			Dictionary<DiagramNode, Int32> index = new Dictionary<DiagramNode, Int32>();
			List<Size> sizes = new List<Size>(nodes.Count);
			foreach (DiagramNode node in nodes)
			{
				index[node] = sizes.Count;
				NodeLayout layout = scene.GetLayout(node);
				sizes.Add(new Size(layout.Width, layout.Height));
			}

			List<ArrangeEdge> edges = new List<ArrangeEdge>();
			foreach (DiagramLink link in scene.Links)
			{
				if (index.TryGetValue(link.Source, out Int32 from) && index.TryGetValue(link.Target, out Int32 to))
				{
					edges.Add(new ArrangeEdge(from, to));
				}
			}

			IReadOnlyList<Point> positions = new LayeredGraphLayout().Arrange(sizes, edges, options);
			for (Int32 i = 0; i < nodes.Count; i++)
			{
				nodes[i].X = positions[i].X;
				nodes[i].Y = positions[i].Y;
			}
		}
	}
}
