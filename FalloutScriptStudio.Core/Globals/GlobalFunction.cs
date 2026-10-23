namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>A function or macro of the description files that reads or writes a global variable.</summary>
	/// <param name="Name">The name as the description files write it (lower case).</param>
	/// <param name="Kind">The family of variables.</param>
	/// <param name="Access">What the call does with the variable.</param>
	/// <param name="ValueArgument">The zero-based index of the argument that holds the value written, or -1 when the call writes no stated value.</param>
	/// <param name="ValueType">The type of the value the call fixes (<c>get_sfall_global_int</c>), or <see cref="GlobalValueType.Unknown"/>.</param>
	public sealed record GlobalFunction(String Name, GlobalVariableKind Kind, GlobalAccess Access, Int32 ValueArgument, GlobalValueType ValueType);
}