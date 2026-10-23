namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>The dialogue nodes of a script and the edges between them.</summary>
	internal sealed class DialogueGraph
	{
		/// <summary>Initializes a new instance of the <see cref="DialogueGraph"/> class.</summary>
		/// <param name="nodes">The nodes in script order.</param>
		/// <param name="entries">The nodes the dialogue can start from: the entry node and the nodes that code outside the nodes refers to.</param>
		/// <param name="entryNode">The name of the entry node, or <see langword="null"/>.</param>
		public DialogueGraph(IReadOnlyList<DialogueGraphNode> nodes, IReadOnlySet<String> entries, String? entryNode)
		{
			this.Nodes = nodes;
			this.Entries = entries;
			this.EntryNode = entryNode;
			this.ByName = new Dictionary<String, DialogueGraphNode>(StringComparer.OrdinalIgnoreCase);
			foreach (DialogueGraphNode node in nodes)
			{
				this.ByName.TryAdd(node.Name, node);
			}
		}

		/// <summary>Gets the nodes in script order.</summary>
		public IReadOnlyList<DialogueGraphNode> Nodes { get; }

		/// <summary>Gets the nodes by name (the case is ignored).</summary>
		public Dictionary<String, DialogueGraphNode> ByName { get; }

		/// <summary>Gets the names of the nodes the dialogue can start from.</summary>
		public IReadOnlySet<String> Entries { get; }

		/// <summary>Gets the name of the entry node (<c>talk_p_proc</c>), or <see langword="null"/>.</summary>
		public String? EntryNode { get; }
	}
}