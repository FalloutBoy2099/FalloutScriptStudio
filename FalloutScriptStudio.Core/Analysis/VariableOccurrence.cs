namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>A place where a declared variable is named in a script (other than its declaration).</summary>
	/// <param name="TokenIndex">The index of the token.</param>
	/// <param name="Declaration">The variable the name refers to.</param>
	/// <param name="IsWrite">Whether the name is only the target of an assignment (<c>name := value;</c>); everything else reads the variable.</param>
	public readonly record struct VariableOccurrence(Int32 TokenIndex, VariableDeclaration Declaration, Boolean IsWrite);
}