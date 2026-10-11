namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// A procedure name typed by the user, checked and normalized by <see cref="ProcedureNameParser"/>.
	/// </summary>
	/// <param name="IsValid">Whether the input is acceptable.</param>
	/// <param name="Name">The bare procedure name (without arguments).</param>
	/// <param name="Declaration">The name with its argument list in script syntax, for example <c>Foo(variable a, variable b)</c>.</param>
	/// <param name="Error">Why the input is not acceptable, or <c>null</c>.</param>
	public sealed record ParsedProcedureName(Boolean IsValid, String Name, String Declaration, String? Error);
}
