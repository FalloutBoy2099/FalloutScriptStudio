namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The outcome of a compilation or preprocessing run.
	/// </summary>
	public sealed record CompilationResult
	{
		/// <summary>Gets a value indicating whether the tools reported success.</summary>
		public required Boolean Success { get; init; }

		/// <summary>Gets the complete text output of the run, ready to be shown in the build log.</summary>
		public required String Output { get; init; }

		/// <summary>Gets the errors, warnings and messages extracted from <see cref="Output"/>.</summary>
		public IReadOnlyList<BuildError> Errors { get; init; } = Array.Empty<BuildError>();

		/// <summary>Gets the path of the produced <c>.int</c> file when a compilation succeeded.</summary>
		public String? OutputFilePath { get; init; }

		/// <summary>Gets the path of the preprocessed script when a preprocessing run succeeded.</summary>
		public String? PreprocessedFilePath { get; init; }

		/// <summary>Gets the external tools that were missing or could not be started (their messages are also part of <see cref="Output"/>).</summary>
		public IReadOnlyList<ToolFailure> ToolFailures { get; init; } = Array.Empty<ToolFailure>();
	}
}