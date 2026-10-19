namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Loads and saves flowchart files.
	/// </summary>
	public interface IFlowchartDocumentStore
	{
		/// <summary>Finds the flowchart file of a script: next to the script, or in its <c>fcd</c> sub folder.</summary>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <returns>The path of an existing file, or <c>null</c>.</returns>
		String? FindFile(String scriptPath);

		/// <summary>Reads a flowchart file.</summary>
		/// <param name="path">The file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The flowchart.</returns>
		/// <exception cref="FormatException">The file is not a flowchart.</exception>
		Task<FlowchartDocument> LoadAsync(String path, CancellationToken cancellationToken = default);

		/// <summary>Writes a flowchart file atomically.</summary>
		/// <param name="path">The file.</param>
		/// <param name="document">The flowchart.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the file is written.</returns>
		Task SaveAsync(String path, FlowchartDocument document, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes a flowchart file atomically and synchronously. For the few places that cannot wait for a task, such as the
		/// closing of a window; everything else uses <see cref="SaveAsync"/>.
		/// </summary>
		/// <param name="path">The file.</param>
		/// <param name="document">The flowchart.</param>
		void Save(String path, FlowchartDocument document);
	}
}