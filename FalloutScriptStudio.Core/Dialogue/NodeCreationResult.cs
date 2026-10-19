namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using TextTools;

	/// <summary>
	/// Result of creating a dialogue node.
	/// </summary>
	/// <param name="Error">The problem, or <see cref="NodeCreationError.None"/>.</param>
	/// <param name="NodeName">The final name of the node (with the <c>Node</c> prefix added when it was missing).</param>
	/// <param name="Edit">The changes to the script that add the node procedure and its declaration; <c>null</c> on failure.</param>
	public sealed record NodeCreationResult(NodeCreationError Error, String NodeName, ProcedureEditResult? Edit)
	{
		/// <summary>Gets a value indicating whether the node can be created.</summary>
		public Boolean Succeeded => this.Error == NodeCreationError.None;
	}
}