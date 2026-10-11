namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// The outcome of an external process.
	/// </summary>
	/// <param name="ExitCode">The exit code of the process.</param>
	/// <param name="Output">The captured standard output (empty when output was not captured).</param>
	public sealed record ProcessRunResult(Int32 ExitCode, String Output);
}
