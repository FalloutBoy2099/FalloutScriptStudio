namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using Documents;

	/// <summary>
	/// Describes a change of the node code editors of a script.
	/// </summary>
	public sealed class NodeEditorEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NodeEditorEventArgs"/> class.
		/// </summary>
		/// <param name="script">The script whose node is edited.</param>
		/// <param name="nodeName">The name of the node.</param>
		public NodeEditorEventArgs(DocumentViewModel script, String nodeName)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);
			this.Script = script;
			this.NodeName = nodeName;
		}

		/// <summary>Gets the script whose node is edited.</summary>
		public DocumentViewModel Script { get; }

		/// <summary>Gets the name of the node.</summary>
		public String NodeName { get; }
	}
}