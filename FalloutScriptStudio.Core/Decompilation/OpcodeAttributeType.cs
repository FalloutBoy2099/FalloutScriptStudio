namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>What an opcode is in the source of a script.</summary>
	internal enum OpcodeAttributeType
	{
		/// <summary>A line of its own (<c>critter_heal(self_obj, 5);</c>).</summary>
		Statement,

		/// <summary>A value, used inside another expression (<c>obj_name(who)</c>).</summary>
		Expression,

		/// <summary>A call that is a value when something uses it, and a line when nothing does.</summary>
		ExpressionStatement,
	}
}