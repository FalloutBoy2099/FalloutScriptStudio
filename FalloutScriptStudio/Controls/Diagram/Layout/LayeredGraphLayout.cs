namespace FalloutScriptStudio.Controls.Diagram.Layout
{
	using System;
	using System.Collections.Generic;
	using System.Windows;

	/// <summary>
	/// Arranges boxes of known size in left-to-right layers following the direction of their links: a box is placed to the
	/// right of every box that links to it. Cycles are broken with a depth-first search, the order inside a layer is
	/// improved with a few barycenter sweeps, and boxes without any link are packed in rows below the graph.
	/// The algorithm is pure (no WPF visuals involved) and deterministic.
	/// </summary>
	/// <remarks>
	/// This replaces the legacy <c>DiagramRouter.RecalcPositions</c> dependency-tree arrangement, which put every box in a
	/// single parent row and could not handle cycles (very common in dialogue graphs).
	/// </remarks>
	public sealed class LayeredGraphLayout
	{
		private const Int32 SweepCount = 4;

		/// <summary>
		/// Computes the top-left position of every box.
		/// </summary>
		/// <param name="sizes">Size of each box; the result is indexed the same way.</param>
		/// <param name="edges">Links between boxes. Invalid indexes, self loops and duplicates are ignored.</param>
		/// <param name="options">Spacing and origin; <c>null</c> selects the defaults.</param>
		/// <returns>The position of each box.</returns>
		public IReadOnlyList<Point> Arrange(IReadOnlyList<Size> sizes, IEnumerable<ArrangeEdge> edges, DiagramArrangeOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(sizes);
			ArgumentNullException.ThrowIfNull(edges);
			DiagramArrangeOptions o = options ?? new DiagramArrangeOptions();
			Int32 n = sizes.Count;
			Point[] result = new Point[n];
			if (n == 0)
			{
				return result;
			}

			List<Int32>[] outs = new List<Int32>[n];
			List<Int32>[] ins = new List<Int32>[n];
			for (Int32 i = 0; i < n; i++)
			{
				outs[i] = [];
				ins[i] = [];
			}

			HashSet<Int64> seen = [];
			foreach (ArrangeEdge edge in edges)
			{
				if (edge.From < 0 || edge.From >= n || edge.To < 0 || edge.To >= n || edge.From == edge.To)
				{
					continue;
				}

				if (seen.Add(EdgeKey(edge.From, edge.To, n)))
				{
					outs[edge.From].Add(edge.To);
					ins[edge.To].Add(edge.From);
				}
			}

			List<Int32> connected = [];
			List<Int32> unlinked = [];
			for (Int32 i = 0; i < n; i++)
			{
				if (outs[i].Count + ins[i].Count > 0)
				{
					connected.Add(i);
				}
				else
				{
					unlinked.Add(i);
				}
			}

			Double graphWidth = 0.0;
			Double graphHeight = 0.0;
			if (connected.Count > 0)
			{
				ArrangeLayers(sizes, outs, ins, connected, o, result, out graphWidth, out graphHeight);
			}

			PlaceUnlinked(sizes, unlinked, o, result, connected.Count > 0, graphWidth, graphHeight);
			return result;
		}

		private static Int64 EdgeKey(Int32 from, Int32 to, Int32 n)
		{
			return ((Int64)from * n) + to;
		}

		private static void ArrangeLayers(
			IReadOnlyList<Size> sizes,
			List<Int32>[] outs,
			List<Int32>[] ins,
			List<Int32> connected,
			DiagramArrangeOptions o,
			Point[] result,
			out Double graphWidth,
			out Double graphHeight)
		{
			Int32 n = sizes.Count;
			Int32[] discovery = new Int32[n];
			List<Int32> postOrder = new(connected.Count);
			HashSet<Int64> backEdges = [];
			Int32[] state = new Int32[n];

			List<Int32> starts = [];
			foreach (Int32 v in connected)
			{
				if (ins[v].Count == 0)
				{
					starts.Add(v);
				}
			}

			starts.AddRange(connected);
			Int32 discoveryCounter = 0;
			foreach (Int32 start in starts)
			{
				if (state[start] != 0)
				{
					continue;
				}

				Stack<(Int32 Node, Int32 NextChild)> stack = new();
				state[start] = 1;
				discovery[start] = discoveryCounter++;
				stack.Push((start, 0));
				while (stack.Count > 0)
				{
					(Int32 node, Int32 next) = stack.Pop();
					if (next < outs[node].Count)
					{
						stack.Push((node, next + 1));
						Int32 child = outs[node][next];
						if (state[child] == 0)
						{
							state[child] = 1;
							discovery[child] = discoveryCounter++;
							stack.Push((child, 0));
						}
						else if (state[child] == 1)
						{
							backEdges.Add(EdgeKey(node, child, n));
						}
					}
					else
					{
						state[node] = 2;
						postOrder.Add(node);
					}
				}
			}

			Int32[] layerOf = new Int32[n];
			Int32 maxLayer = 0;
			for (Int32 i = postOrder.Count - 1; i >= 0; i--)
			{
				Int32 v = postOrder[i];
				foreach (Int32 w in outs[v])
				{
					if (backEdges.Contains(EdgeKey(v, w, n)))
					{
						continue;
					}

					if (layerOf[w] < layerOf[v] + 1)
					{
						layerOf[w] = layerOf[v] + 1;
					}
				}
			}

			foreach (Int32 v in connected)
			{
				maxLayer = Math.Max(maxLayer, layerOf[v]);
			}

			List<Int32>[] layers = new List<Int32>[maxLayer + 1];
			for (Int32 i = 0; i <= maxLayer; i++)
			{
				layers[i] = [];
			}

			foreach (Int32 v in connected)
			{
				layers[layerOf[v]].Add(v);
			}

			foreach (List<Int32> layer in layers)
			{
				layer.Sort((a, b) => discovery[a].CompareTo(discovery[b]));
			}

			Int32[] position = new Int32[n];
			UpdatePositions(layers, position);
			for (Int32 sweep = 0; sweep < SweepCount; sweep++)
			{
				if (sweep % 2 == 0)
				{
					for (Int32 l = 1; l <= maxLayer; l++)
					{
						ReorderLayer(layers, l, position, layerOf, ins, l - 1);
					}
				}
				else
				{
					for (Int32 l = maxLayer - 1; l >= 0; l--)
					{
						ReorderLayer(layers, l, position, layerOf, outs, l + 1);
					}
				}
			}

			Double[] columnWidth = new Double[maxLayer + 1];
			Double[] columnHeight = new Double[maxLayer + 1];
			graphHeight = 0.0;
			for (Int32 l = 0; l <= maxLayer; l++)
			{
				foreach (Int32 v in layers[l])
				{
					columnWidth[l] = Math.Max(columnWidth[l], sizes[v].Width);
					columnHeight[l] += sizes[v].Height;
				}

				columnHeight[l] += Math.Max(0, layers[l].Count - 1) * o.VerticalSpacing;
				graphHeight = Math.Max(graphHeight, columnHeight[l]);
			}

			Double x = o.OriginX;
			for (Int32 l = 0; l <= maxLayer; l++)
			{
				Double y = o.OriginY + ((graphHeight - columnHeight[l]) / 2.0);
				foreach (Int32 v in layers[l])
				{
					result[v] = new Point(x, y);
					y += sizes[v].Height + o.VerticalSpacing;
				}

				x += columnWidth[l] + o.HorizontalSpacing;
			}

			graphWidth = Math.Max(0.0, x - o.HorizontalSpacing - o.OriginX);
		}

		private static void UpdatePositions(List<Int32>[] layers, Int32[] position)
		{
			foreach (List<Int32> layer in layers)
			{
				for (Int32 i = 0; i < layer.Count; i++)
				{
					position[layer[i]] = i;
				}
			}
		}

		private static void ReorderLayer(List<Int32>[] layers, Int32 layerIndex, Int32[] position, Int32[] layerOf, List<Int32>[] neighbours, Int32 adjacentLayer)
		{
			List<Int32> layer = layers[layerIndex];
			if (layer.Count < 2)
			{
				return;
			}

			Double[] barycenter = new Double[position.Length];
			foreach (Int32 v in layer)
			{
				Double sum = 0.0;
				Int32 count = 0;
				foreach (Int32 u in neighbours[v])
				{
					if (layerOf[u] == adjacentLayer)
					{
						Int32 adjacentCount = layers[adjacentLayer].Count;
						sum += adjacentCount > 1 ? (Double)position[u] / (adjacentCount - 1) : 0.5;
						count++;
					}
				}

				barycenter[v] = count > 0 ? sum / count : (Double)position[v] / (layer.Count - 1);
			}

			List<Int32> ordered = new(layer);
			ordered.Sort((a, b) =>
			{
				Int32 c = barycenter[a].CompareTo(barycenter[b]);
				return c != 0 ? c : position[a].CompareTo(position[b]);
			});

			layer.Clear();
			layer.AddRange(ordered);
			for (Int32 i = 0; i < layer.Count; i++)
			{
				position[layer[i]] = i;
			}
		}

		private static void PlaceUnlinked(
			IReadOnlyList<Size> sizes,
			List<Int32> unlinked,
			DiagramArrangeOptions o,
			Point[] result,
			Boolean hasGraph,
			Double graphWidth,
			Double graphHeight)
		{
			if (unlinked.Count == 0)
			{
				return;
			}

			Double rowLimit = Math.Max(o.UnlinkedRowWidth, graphWidth);
			Double y = hasGraph ? o.OriginY + graphHeight + (2.0 * o.VerticalSpacing) : o.OriginY;
			Double x = o.OriginX;
			Double rowHeight = 0.0;
			foreach (Int32 v in unlinked)
			{
				Size size = sizes[v];
				if (x > o.OriginX && x + size.Width - o.OriginX > rowLimit)
				{
					x = o.OriginX;
					y += rowHeight + o.VerticalSpacing;
					rowHeight = 0.0;
				}

				result[v] = new Point(x, y);
				x += size.Width + o.HorizontalSpacing / 2.0;
				rowHeight = Math.Max(rowHeight, size.Height);
			}
		}
	}
}