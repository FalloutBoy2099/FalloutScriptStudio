namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;

	/// <summary>What the build cache remembers about the last successful compilation of one script into one output file.</summary>
	/// <param name="Script">The full path of the script.</param>
	/// <param name="Output">The full path of the compiled file.</param>
	/// <param name="Fingerprint">The option and tool digests the script was built with.</param>
	/// <param name="ShortCircuit">Whether short-circuit evaluation was in effect for the script (the option, or the script itself asking for it).</param>
	/// <param name="Source">The stamp of the script when the compilation started.</param>
	/// <param name="OutputStamp">The stamp of the compiled file when the compilation ended.</param>
	/// <param name="Dependencies">Every file the script includes, directly or not, with its stamp when the compilation started.</param>
	public sealed record BuildRecord(
		String Script,
		String Output,
		BuildFingerprint Fingerprint,
		Boolean ShortCircuit,
		FileStamp Source,
		FileStamp OutputStamp,
		IReadOnlyList<DependencyStamp> Dependencies);
}