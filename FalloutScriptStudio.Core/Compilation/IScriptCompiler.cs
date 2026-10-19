namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Compiles, preprocesses and decompiles Fallout scripts (the compiler and the preprocessors are external tools).
	/// </summary>
	public interface IScriptCompiler
	{
		/// <summary>
		/// Compiles a script. Nothing is thrown for tool failures; they are reported in the result.
		/// </summary>
		/// <param name="request">What to compile.</param>
		/// <param name="cancellationToken">Cancels the run; a previous <c>.int</c> file is restored.</param>
		/// <returns>The result, including the parsed build errors.</returns>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Preprocesses a script. On success <see cref="CompilationResult.PreprocessedFilePath"/> holds the preprocessed copy.
		/// </summary>
		/// <param name="request">What to preprocess.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>The result.</returns>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// Decompiles a compiled script.
		/// </summary>
		/// <param name="request">What to decompile.</param>
		/// <param name="outputProgress">Receives every output line of the decompiler as it is produced.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>The result.</returns>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets the path where the <c>.int</c> file of a script is (or would be) written.
		/// </summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="options">The compiler options.</param>
		/// <param name="temporaryOutput">Whether the round-trip location in the scratch folder is wanted.</param>
		/// <returns>The full path of the compiled file.</returns>
		String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false);
	}
}