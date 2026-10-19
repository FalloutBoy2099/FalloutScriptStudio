namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// Describes one compilation (or preprocessing) of a script.
	/// </summary>
	public sealed record CompilationRequest
	{
		/// <summary>Gets the script to compile.</summary>
		public required String ScriptPath { get; init; }

		/// <summary>Gets the compiler options to use.</summary>
		public required CompilerOptions Options { get; init; }

		/// <summary>Gets a value indicating whether the script itself requires short-circuit evaluation.</summary>
		public Boolean ShortCircuit { get; init; }

		/// <summary>Gets a value indicating whether the <c>.int</c> goes to the scratch folder (round-trip check).</summary>
		public Boolean TemporaryOutput { get; init; }

		/// <summary>Gets a value indicating whether the compilation is part of a batch (no log header or external preprocessing banner).</summary>
		public Boolean Batch { get; init; }

		/// <summary>Gets a value indicating whether the script is only preprocessed.</summary>
		public Boolean PreprocessOnly { get; init; }
	}
}