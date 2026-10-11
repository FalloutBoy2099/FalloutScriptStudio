namespace FalloutScriptStudio.Services.ScriptTools.Procedures
{
	using System;

	/// <summary>
	/// The outcome of the procedure name dialog.
	/// </summary>
	/// <param name="Name">The plain procedure name without arguments (legacy <c>CheckName</c>).</param>
	/// <param name="Declaration">The text to put between <c>procedure</c> and <c>begin</c>: the name, followed by the
	/// argument list with a <c>variable</c> keyword before each argument (legacy <c>ProcedureName</c>).</param>
	/// <param name="Placement">Where the procedure is inserted; <see cref="ProcedurePlacement.AtCaret"/> when the dialog offered no choice.</param>
	/// <param name="CopyBody">Whether the body of the current procedure is copied.</param>
	public sealed record ProcedureNameResult(String Name, String Declaration, ProcedurePlacement Placement, Boolean CopyBody);
}
