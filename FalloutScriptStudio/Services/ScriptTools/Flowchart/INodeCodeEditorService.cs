namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;

	/// <summary>
	/// Opens and tracks the node code editor windows. There is at most one editor per node of a script.
	/// </summary>
	public interface INodeCodeEditorService
	{
		/// <summary>Occurs after an editor was opened, closed, or switched to another node.</summary>
		event EventHandler<NodeEditorEventArgs>? EditorsChanged;

		/// <summary>Occurs after an editor applied the code of its node to the script.</summary>
		event EventHandler<NodeEditorEventArgs>? CodeApplied;

		/// <summary>Determines whether a node is being edited in an editor window.</summary>
		/// <param name="script">The script.</param>
		/// <param name="nodeName">The node name (case-insensitive).</param>
		/// <returns><see langword="true"/> when an editor shows the node.</returns>
		Boolean IsEditing(DocumentViewModel script, String nodeName);

		/// <summary>Gets the names of the nodes of a script that are being edited.</summary>
		/// <param name="script">The script.</param>
		/// <returns>The names.</returns>
		IReadOnlyList<String> GetEditedNodes(DocumentViewModel script);

		/// <summary>Opens the editor of a node, or activates it when it is open already.</summary>
		/// <param name="script">The script.</param>
		/// <param name="nodeName">The node name.</param>
		/// <param name="fromFlowchart">Whether the editor belongs to a flowchart window and closes with it.</param>
		/// <returns><see langword="true"/> when an editor is shown.</returns>
		Boolean Open(DocumentViewModel script, String nodeName, Boolean fromFlowchart);

		/// <summary>
		/// Closes the editors that were opened from the flowchart of a script. The user is still asked about unsaved changes
		/// (Yes / No only).
		/// </summary>
		/// <param name="script">The script.</param>
		void CloseFlowchartEditors(DocumentViewModel script);

		/// <summary>
		/// Closes every editor of a script, whatever opened it (legacy: closing a tab closes its node editors). The user is still asked
		/// about unsaved changes (Yes / No only), and the answer is awaited, so the code is applied to the script before the tab goes on closing.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <returns>A task that completes when all editors are closed.</returns>
		Task CloseEditorsAsync(DocumentViewModel script);
	}
}
