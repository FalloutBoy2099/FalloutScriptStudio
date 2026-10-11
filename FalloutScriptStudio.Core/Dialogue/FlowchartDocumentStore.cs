namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// <see cref="IFlowchartDocumentStore"/> working on the file system.
	/// </summary>
	public sealed class FlowchartDocumentStore : IFlowchartDocumentStore
	{
		/// <summary>The name of the sub folder searched for flowchart files.</summary>
		public const String SubFolderName = "fcd";

		/// <summary>
		/// Gets the places a flowchart file of a script is looked for, in order: next to the script, then the <c>fcd</c> sub folder.
		/// </summary>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <returns>The candidate paths.</returns>
		public static IReadOnlyList<String> GetSearchPaths(String scriptPath)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);

			String directory = Path.GetDirectoryName(scriptPath) ?? String.Empty;
			return new[]
			{
				Path.ChangeExtension(scriptPath, FlowchartDocumentSerializer.FileExtension),
				Path.Combine(directory, SubFolderName, Path.GetFileNameWithoutExtension(scriptPath) + FlowchartDocumentSerializer.FileExtension),
			};
		}

		/// <summary>Gets the path a new flowchart file of a script is saved to by default: next to the script.</summary>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <returns>The path.</returns>
		public static String GetDefaultSavePath(String scriptPath)
		{
			return GetSearchPaths(scriptPath)[0];
		}

		/// <inheritdoc />
		public String? FindFile(String scriptPath)
		{
			foreach (String candidate in GetSearchPaths(scriptPath))
			{
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}

			return null;
		}

		/// <inheritdoc />
		public async Task<FlowchartDocument> LoadAsync(String path, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			String xml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
			return FlowchartDocumentSerializer.Parse(xml);
		}

		/// <inheritdoc />
		public Task SaveAsync(String path, FlowchartDocument document, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(document);

			return AtomicFile.WriteAllBytesAsync(path, FlowchartDocumentSerializer.ToFileBytes(document), cancellationToken);
		}

		/// <inheritdoc />
		public void Save(String path, FlowchartDocument document)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(document);

			AtomicFile.WriteAllBytes(path, FlowchartDocumentSerializer.ToFileBytes(document));
		}
	}
}
