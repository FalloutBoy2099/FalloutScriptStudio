namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>One parameter of a function in the description files.</summary>
	/// <param name="Type">The type as written (<c>int</c>, <c>string</c>, <c>ObjectPtr</c>...), or an empty text when the description states none.</param>
	/// <param name="Name">The name of the parameter, or an empty text.</param>
	/// <param name="IsOptional">Whether the parameter is written in square brackets, <c>[int flags]</c>.</param>
	/// <param name="IsVariadic">Whether the parameter is the <c>...</c> that stands for any number of further arguments.</param>
	public sealed record FunctionParameter(String Type, String Name, Boolean IsOptional, Boolean IsVariadic);
}