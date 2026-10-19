namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Location of a procedure inside the text of a script.
	/// </summary>
	/// <remarks>All line numbers are 1-based; -1 means "not present / not found".</remarks>
	/// <param name="Name">Procedure name (case preserved).</param>
	/// <param name="DeclarationLine">Line of the forward declaration (<c>procedure Foo;</c>), or -1.</param>
	/// <param name="BeginLine">Line where the body starts (the <c>procedure</c> line), or -1 for a declaration without a body.</param>
	/// <param name="EndLine">Line of the closing <c>end</c>, or -1 when it was not found.</param>
	/// <param name="Flags">Modifiers found in front of the <c>procedure</c> keyword.</param>
	public sealed record ProcedureBlock(String Name, Int32 DeclarationLine, Int32 BeginLine, Int32 EndLine, ProcedureFlags Flags = ProcedureFlags.None)
	{
		/// <summary>Gets a value indicating whether the procedure has a body in the text.</summary>
		public Boolean HasBody => this.BeginLine > 0;
	}
}