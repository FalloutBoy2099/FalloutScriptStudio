namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Analysis;
	using Globals;
	using Parsing;

	/// <summary>
	/// The symbols (procedures, macros and script level variables) of every script and header of the project, found without opening the
	/// files in the editor. It is built in the background, only re-reads the files whose time stamp or size changed, and never keeps the
	/// text of a file. Queries return at once from the last complete snapshot.
	/// </summary>
	public interface IProjectSymbolIndex
	{
		/// <summary>Raised (on a background thread) while a run progresses, and once more when it ends (the phase is then <see cref="ProjectIndexPhase.Idle"/>).</summary>
		event EventHandler<ProjectIndexProgress>? ProgressChanged;

		/// <summary>Raised (on a background thread) after the snapshot was replaced because files were added, changed or removed.</summary>
		event EventHandler? Updated;

		/// <summary>Gets the progress of the current run.</summary>
		ProjectIndexProgress Progress { get; }

		/// <summary>Gets the number of files in the index.</summary>
		Int32 FileCount { get; }

		/// <summary>Gets the number of symbols in the index.</summary>
		Int32 SymbolCount { get; }

		/// <summary>Gets a value indicating whether some files were left out because the symbol limit was reached.</summary>
		Boolean IsTruncated { get; }

		/// <summary>Gets a value indicating whether some files were left out of the global variables because the limit of uses was reached.</summary>
		Boolean AreGlobalsTruncated => false;

		/// <summary>Gets the full paths of the indexed files.</summary>
		IReadOnlyCollection<String> Files { get; }

		/// <summary>
		/// Brings the index up to date with the files of the roots: new and changed files are read, files that are gone or outside the roots
		/// are dropped, the others are kept. Runs off the calling thread; runs one after the other.
		/// </summary>
		/// <param name="roots">What to index.</param>
		/// <param name="cancellationToken">Cancels the run; the previous snapshot stays in place.</param>
		/// <returns>A task that completes when the run ended.</returns>
		/// <exception cref="OperationCanceledException">The run was cancelled.</exception>
		Task UpdateAsync(ProjectIndexRoots roots, CancellationToken cancellationToken = default);

		/// <summary>Reads one file again (after it was saved) and replaces its symbols. A file that does not exist any more is removed.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <param name="text">The text that was saved, or <see langword="null"/> to read the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the index holds the new symbols.</returns>
		Task UpdateFileAsync(String path, String? text = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets the reads and writes of global variables (sfall globals, game, map and local variables) of the indexed files, with the macros of
		/// the project resolved. The index of the last complete snapshot is built on first use and kept until the snapshot is replaced.
		/// </summary>
		/// <returns>The global variables; empty for an implementation that does not track them.</returns>
		GlobalVariableIndex GetGlobalVariables() => GlobalVariableIndex.Empty;

		/// <summary>Forgets everything.</summary>
		void Clear();

		/// <summary>Finds the symbols with a name. The case is ignored, except for macros, whose name is case sensitive.</summary>
		/// <param name="name">The name.</param>
		/// <param name="kinds">The kinds to return.</param>
		/// <returns>The symbols, in no particular order (see <see cref="SymbolCloseness.Order"/>).</returns>
		IReadOnlyList<IndexedSymbol> Find(String name, IndexedSymbolKind kinds = IndexedSymbolKind.All);

		/// <summary>Finds the symbols that match a filter and ranks them (see <see cref="SymbolMatcher"/>).</summary>
		/// <param name="filter">The filter text; the first symbols by name when it is blank.</param>
		/// <param name="maxResults">The most symbols to return.</param>
		/// <param name="kinds">The kinds to return.</param>
		/// <returns>The best matches first.</returns>
		IReadOnlyList<IndexedSymbol> Search(String? filter, Int32 maxResults, IndexedSymbolKind kinds = IndexedSymbolKind.All);

		/// <summary>Finds the places where a name is written in the files of the project.</summary>
		/// <param name="request">What to look for and where.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The occurrences, ordered by file and position.</returns>
		Task<IReadOnlyList<SymbolOccurrence>> FindOccurrencesAsync(SymbolOccurrenceRequest request, CancellationToken cancellationToken = default);

		/// <summary>Gets the files that include a file, directly or through other files, and the file itself.</summary>
		/// <param name="file">The full path of the included file.</param>
		/// <param name="resolver">Finds the files of the <c>#include</c> directives.</param>
		/// <returns>The full paths.</returns>
		IReadOnlySet<String> GetFilesSeeing(String file, IncludeResolver resolver);

		/// <summary>Gets the files that some files include, directly or through other files, and the files themselves.</summary>
		/// <param name="seeds">The full paths of the files (already resolved) to start from.</param>
		/// <param name="resolver">Finds the files of the <c>#include</c> directives.</param>
		/// <returns>The full paths.</returns>
		IReadOnlySet<String> GetIncludeClosure(IEnumerable<String> seeds, IncludeResolver resolver);
	}
}