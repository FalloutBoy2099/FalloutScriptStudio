namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// The outcome of a decompilation.
	/// </summary>
	/// <param name="Success">Whether a decompiled script was produced.</param>
	/// <param name="OutputFilePath">The decompiled <c>.ssl</c> file in the scratch folder, or <see langword="null"/> on failure.</param>
	/// <param name="Output">The collected output of the decompiler(s).</param>
	public sealed record DecompilationResult(Boolean Success, String? OutputFilePath, String Output);
}
