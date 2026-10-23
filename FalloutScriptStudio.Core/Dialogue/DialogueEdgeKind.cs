namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>How an edge of the dialogue graph was found.</summary>
	internal enum DialogueEdgeKind
	{
		/// <summary>An option.</summary>
		Option,

		/// <summary>A <c>call</c> or a macro that jumps to a node.</summary>
		Call,

		/// <summary>The name of the node appears in the body in some other way.</summary>
		Mention,
	}
}