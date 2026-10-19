namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Searches the content of files on disk (the "search in folder" mode of the legacy search form).
	/// </summary>
	public interface IFileSearcher
	{
		/// <summary>Finds the files that contain a match.</summary>
		/// <param name="files">The files to read.</param>
		/// <param name="query">What to find.</param>
		/// <param name="encoding">The encoding of the files; UTF-8 (with byte order mark detection) when <c>null</c>.</param>
		/// <param name="progress">Receives the number of files processed so far; may be <c>null</c>.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The paths of the files with a match, in the order given. Files that cannot be read are skipped.</returns>
		Task<IReadOnlyList<String>> FindFilesAsync(IEnumerable<String> files, SearchQuery query, Encoding? encoding, IProgress<Int32>? progress, CancellationToken cancellationToken);

		/// <summary>Finds the matching lines of the files ("find all").</summary>
		/// <param name="files">The files to read.</param>
		/// <param name="query">What to find.</param>
		/// <param name="encoding">The encoding of the files; UTF-8 (with byte order mark detection) when <c>null</c>.</param>
		/// <param name="progress">Receives the number of files processed so far; may be <c>null</c>.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>One row per matching line, with the full path of the file in <see cref="SearchResultRow.File"/>.</returns>
		Task<IReadOnlyList<SearchResultRow>> FindRowsAsync(IEnumerable<String> files, SearchQuery query, Encoding? encoding, IProgress<Int32>? progress, CancellationToken cancellationToken);
	}
}