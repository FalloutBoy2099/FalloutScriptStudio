namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// Describes the decompilation of a compiled script.
	/// </summary>
	public sealed record DecompilationRequest
	{
		/// <summary>Gets the compiled <c>.int</c> script.</summary>
		public required String ScriptPath { get; init; }

		/// <summary>Gets the compiler options to use.</summary>
		public required CompilerOptions Options { get; init; }

		/// <summary>Gets a value indicating whether the decompilation belongs to a round-trip check.</summary>
		public Boolean TemporaryOutput { get; init; }
	}
}