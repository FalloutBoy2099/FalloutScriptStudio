namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The nodes of a script's dialogue as the flowchart shows them.
	/// </summary>
	public sealed class FlowchartModel
	{
		private readonly Dictionary<String, FlowchartNodeData> _byName;

		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartModel"/> class.
		/// </summary>
		/// <param name="nodes">The nodes in script order.</param>
		/// <param name="parsedLines">The parsed lines of each node by node name.</param>
		public FlowchartModel(IReadOnlyList<FlowchartNodeData> nodes, IReadOnlyDictionary<String, IReadOnlyList<DialogueLine>> parsedLines)
		{
			ArgumentNullException.ThrowIfNull(nodes);
			ArgumentNullException.ThrowIfNull(parsedLines);
			this.Nodes = nodes;
			this.ParsedLines = parsedLines;
			this._byName = new(StringComparer.OrdinalIgnoreCase);
			foreach (FlowchartNodeData node in nodes)
			{
				this._byName.TryAdd(node.Name, node);
			}
		}

		/// <summary>Gets an empty model.</summary>
		public static FlowchartModel Empty { get; } = new FlowchartModel(Array.Empty<FlowchartNodeData>(), new Dictionary<String, IReadOnlyList<DialogueLine>>());

		/// <summary>Gets the nodes in script order.</summary>
		public IReadOnlyList<FlowchartNodeData> Nodes { get; }

		/// <summary>Gets the parsed lines of each node by node name (case-insensitive lookup is not applied; use the names of <see cref="Nodes"/>).</summary>
		public IReadOnlyDictionary<String, IReadOnlyList<DialogueLine>> ParsedLines { get; }

		/// <summary>Gets the names of the nodes in script order.</summary>
		public IEnumerable<String> NodeNames => this.Nodes.Select(n => n.Name);

		/// <summary>Finds a node by name.</summary>
		/// <param name="name">The node name (case-insensitive).</param>
		/// <returns>The node, or <c>null</c>.</returns>
		public FlowchartNodeData? FindNode(String name)
		{
			ArgumentNullException.ThrowIfNull(name);
			return this._byName.TryGetValue(name, out FlowchartNodeData? node) ? node : null;
		}
	}
}