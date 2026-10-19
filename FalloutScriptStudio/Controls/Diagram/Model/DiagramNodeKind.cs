namespace FalloutScriptStudio.Controls.Diagram.Model
{
	/// <summary>
	/// Semantic category of a <see cref="DiagramNode"/>. It only drives the colour scheme of the node header and border.
	/// </summary>
	public enum DiagramNodeKind
	{
		/// <summary>An ordinary node.</summary>
		Default,

		/// <summary>The entry point of the diagram (the dialogue start procedure).</summary>
		Start,

		/// <summary>A terminal node: it is referenced by other nodes but does not link anywhere.</summary>
		End,

		/// <summary>A node that links to others but is not referenced by any node.</summary>
		NoIncomingLinks,

		/// <summary>A node that neither links nor is linked; drawn with a dashed border.</summary>
		Unused
	}
}