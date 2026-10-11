namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// The content of a flowchart file (<c>.fcd</c>): where the nodes and notes are on the canvas and the view state.
	/// It is independent of any UI; <see cref="FlowchartDocumentSerializer"/> reads and writes the legacy XML format.
	/// </summary>
	/// <param name="ScriptName">The file name of the script the flowchart belongs to (see <see cref="CreateScriptName"/>).</param>
	public sealed record FlowchartDocument(String ScriptName)
	{
		/// <summary>Gets the zoom factor of the canvas (1 = 100%).</summary>
		public Single Zoom { get; init; } = 1f;

		/// <summary>Gets the horizontal scroll position of the canvas.</summary>
		public Int32 PositionX { get; init; }

		/// <summary>Gets the vertical scroll position of the canvas.</summary>
		public Int32 PositionY { get; init; }

		/// <summary>Gets the nodes. The legacy editor wrote the hidden nodes first, then the visible ones in z-order.</summary>
		public IReadOnlyList<FlowchartNodeState> Nodes { get; init; } = Array.Empty<FlowchartNodeState>();

		/// <summary>Gets the notes.</summary>
		public IReadOnlyList<FlowchartNoteState> Notes { get; init; } = Array.Empty<FlowchartNoteState>();

		/// <summary>Creates the value stored in <see cref="ScriptName"/>: the lower-cased file name of the script.</summary>
		/// <param name="scriptPath">The path or file name of the script.</param>
		/// <returns>The script name.</returns>
		public static String CreateScriptName(String scriptPath)
		{
			ArgumentNullException.ThrowIfNull(scriptPath);
			return Path.GetFileName(scriptPath).ToLowerInvariant();
		}

		/// <summary>Determines whether the flowchart was saved for a script (the legacy "saved for another script file" check).</summary>
		/// <param name="scriptPath">The path or file name of the script.</param>
		/// <returns><c>true</c> when the script file names match, ignoring case.</returns>
		public Boolean IsForScript(String scriptPath)
		{
			ArgumentNullException.ThrowIfNull(scriptPath);
			return String.Equals(this.ScriptName, Path.GetFileName(scriptPath), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Finds the saved state of a node.</summary>
		/// <param name="name">The node name (case-insensitive).</param>
		/// <returns>The state, or <c>null</c>.</returns>
		public FlowchartNodeState? FindNode(String name)
		{
			ArgumentNullException.ThrowIfNull(name);
			return this.Nodes.FirstOrDefault(n => String.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Compares the saved nodes with the nodes of the script.
		/// </summary>
		/// <param name="scriptNodeNames">The names of the node procedures that exist in the script.</param>
		/// <returns>The flowchart without obsolete nodes, and the names that were added or removed.</returns>
		public FlowchartReconciliation Reconcile(IEnumerable<String> scriptNodeNames)
		{
			ArgumentNullException.ThrowIfNull(scriptNodeNames);

			List<String> names = scriptNodeNames.ToList();
			var known = new HashSet<String>(names, StringComparer.OrdinalIgnoreCase);
			var saved = new HashSet<String>(this.Nodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);

			List<FlowchartNodeState> kept = this.Nodes.Where(n => known.Contains(n.Name)).ToList();
			List<String> removed = this.Nodes.Where(n => !known.Contains(n.Name)).Select(n => n.Name).ToList();
			List<String> added = names.Where(n => !saved.Contains(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			return new FlowchartReconciliation(this with { Nodes = kept }, added, removed);
		}
	}
}
