namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// A script that failed to compile in a mass compilation.
	/// </summary>
	/// <param name="ScriptPath">The script.</param>
	/// <param name="Output">The output of the compilation.</param>
	public sealed record BatchCompileFailure(String ScriptPath, String Output);
}
