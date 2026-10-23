namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>What the target of an option or a call turned out to be.</summary>
	internal enum TargetKind
	{
		/// <summary>A dialogue node of the script.</summary>
		Node,

		/// <summary>A procedure that is not a node (the dialogue leaves the graph).</summary>
		Procedure,

		/// <summary>Something that cannot be worked out (a macro with arguments, an expression).</summary>
		Unknown,

		/// <summary>A name that is neither a procedure, nor a macro, nor a variable.</summary>
		Undefined,
	}
}