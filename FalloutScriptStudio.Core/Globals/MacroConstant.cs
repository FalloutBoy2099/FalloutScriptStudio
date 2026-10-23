namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>How a <c>#define</c> without parameters gives a constant.</summary>
	internal enum MacroConstantKind
	{
		/// <summary>A number.</summary>
		Number,

		/// <summary>A string.</summary>
		String,

		/// <summary>Another macro.</summary>
		Alias,
	}

	/// <summary>An object-like macro whose body is a number, a string or the name of another macro (<c>#define GVAR_FOO (27)</c>).</summary>
	/// <param name="Name">The macro name.</param>
	/// <param name="Kind">What the body is.</param>
	/// <param name="Value">The number as written, the text of the string without its quotes, or the other macro name.</param>
	internal readonly record struct MacroConstant(String Name, MacroConstantKind Kind, String Value);
}