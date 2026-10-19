namespace FalloutScriptStudio.Core.Building
{
	using System;

	/// <summary>A script whose staleness is asked for.</summary>
	/// <param name="ScriptPath">The script.</param>
	/// <param name="ShortCircuit">Whether the script asks for short-circuit evaluation itself (in addition to the option).</param>
	public sealed record BuildCandidate(String ScriptPath, Boolean ShortCircuit = false);
}