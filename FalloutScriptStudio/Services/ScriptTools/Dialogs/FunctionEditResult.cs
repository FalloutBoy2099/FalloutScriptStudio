namespace FalloutScriptStudio.Services.ScriptTools.Dialogs
{
	using System;
	using Core.Functions;

	/// <summary>
	/// The outcome of the add / edit user function dialog.
	/// </summary>
	/// <param name="Definition">The function the user entered.</param>
	/// <param name="AddToHighlight">Whether the keyword of the code should be added to the syntax highlighting (only offered when adding).</param>
	public sealed record FunctionEditResult(FunctionDefinition Definition, Boolean AddToHighlight);
}