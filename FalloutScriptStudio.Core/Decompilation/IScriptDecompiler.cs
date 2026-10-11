namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// Turns a compiled <c>.int</c> script back into <c>.ssl</c> source.
	/// </summary>
	/// <remarks>
	/// The decompiler writes <c>decomp.ssl</c> into the scratch folder of the request's options and then renames it to
	/// <c>&lt;name&gt;_[decomp].ssl</c> (or <c>&lt;name&gt;_[temp].ssl</c> for a round-trip check); that final path is the result's
	/// <see cref="DecompilationResult.OutputFilePath"/>. Failures are reported in the result, not thrown.
	/// </remarks>
	public interface IScriptDecompiler
	{
		/// <summary>
		/// Decompiles a script.
		/// </summary>
		/// <param name="request">What to decompile and how.</param>
		/// <param name="outputProgress">Receives every output line of the decompiler as it is produced.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>The result.</returns>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default);
	}
}
