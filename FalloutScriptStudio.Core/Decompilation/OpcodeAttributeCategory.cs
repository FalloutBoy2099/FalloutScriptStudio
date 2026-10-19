namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>Where the operator is written relative to its operands.</summary>
	internal enum OpcodeAttributeCategory
	{
		/// <summary>Before them, as a call: <c>name(a, b)</c>.</summary>
		Prefix,

		/// <summary>Between them: <c>a + b</c>.</summary>
		Infix,
	}
}