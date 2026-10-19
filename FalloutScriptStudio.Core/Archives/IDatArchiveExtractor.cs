namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Extracts compiled scripts from Fallout <c>.dat</c> archives (or from a folder that replaces one). No external tool is needed.
	/// </summary>
	public interface IDatArchiveExtractor
	{
		/// <summary>
		/// Extracts a script from an archive. Failures are reported in the result, not thrown.
		/// </summary>
		/// <param name="request">What to extract.</param>
		/// <param name="cancellationToken">Cancels the extraction.</param>
		/// <returns>The result of the extraction.</returns>
		/// <exception cref="System.OperationCanceledException">The operation was cancelled.</exception>
		Task<DatExtractionResult> ExtractScriptAsync(DatExtractionRequest request, CancellationToken cancellationToken = default);
	}
}