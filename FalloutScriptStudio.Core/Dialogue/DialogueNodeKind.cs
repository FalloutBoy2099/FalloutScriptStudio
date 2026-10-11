namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>
	/// The role of a node in the dialogue graph (legacy <c>NodesType</c>), used to colour it in the flowchart.
	/// </summary>
	public enum DialogueNodeKind
	{
		/// <summary>A node with incoming and outgoing links.</summary>
		Default = 0,

		/// <summary>The start of the dialogue: <c>talk_p_proc</c>.</summary>
		DialogStart,

		/// <summary>A node that other nodes link to but that links nowhere (the dialogue ends there).</summary>
		DialogEnd,

		/// <summary>A node that links to others but that no node links to.</summary>
		NoFromLink,

		/// <summary>A node without any link in either direction.</summary>
		Unused,
	}
}
