namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A symbol index with a fixed list of symbols and a progress that the test controls.</summary>
	internal sealed class FakeSymbolIndex : IProjectSymbolIndex
	{
		public event EventHandler<ProjectIndexProgress>? ProgressChanged;

		public event EventHandler? Updated;

		public List<IndexedSymbol> Symbols { get; } = new List<IndexedSymbol>();

		public ProjectIndexProgress Progress { get; private set; } = ProjectIndexProgress.Idle;

		public Int32 FileCount => this.Symbols.Select(s => s.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();

		public Int32 SymbolCount => this.Symbols.Count;

		public Boolean IsTruncated => false;

		public IReadOnlyCollection<String> Files => this.Symbols.Select(s => s.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

		/// <summary>Gets the filters and limits of the searches.</summary>
		public List<(String? Filter, Int32 Max)> Searches { get; } = new List<(String? Filter, Int32 Max)>();

		public void SetProgress(ProjectIndexProgress progress)
		{
			this.Progress = progress;
			this.ProgressChanged?.Invoke(this, progress);
		}

		public void RaiseUpdated()
		{
			this.Updated?.Invoke(this, EventArgs.Empty);
		}

		public Task UpdateAsync(ProjectIndexRoots roots, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task UpdateFileAsync(String path, String? text = null, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public void Clear()
		{
			this.Symbols.Clear();
		}

		public IReadOnlyList<IndexedSymbol> Find(String name, IndexedSymbolKind kinds = IndexedSymbolKind.All)
		{
			return this.Symbols.Where(s => String.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) && (s.Kind & kinds) != 0).ToList();
		}

		public IReadOnlyList<IndexedSymbol> Search(String? filter, Int32 maxResults, IndexedSymbolKind kinds = IndexedSymbolKind.All)
		{
			this.Searches.Add((filter, maxResults));
			IReadOnlyList<String> terms = SymbolMatcher.SplitTerms(filter);
			return this.Symbols
				.Select(symbol => (Symbol: symbol, Tier: SymbolMatcher.GetTier(symbol, terms)))
				.Where(item => item.Tier != SymbolMatcher.NoMatch && (item.Symbol.Kind & kinds) != 0)
				.OrderBy(item => item.Tier)
				.ThenBy(item => item.Symbol.Name.Length)
				.ThenBy(item => item.Symbol.Name, StringComparer.OrdinalIgnoreCase)
				.Select(item => item.Symbol)
				.Take(maxResults)
				.ToList();
		}

		public Task<IReadOnlyList<SymbolOccurrence>> FindOccurrencesAsync(SymbolOccurrenceRequest request, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<SymbolOccurrence>>(Array.Empty<SymbolOccurrence>());
		}

		public IReadOnlySet<String> GetFilesSeeing(String file, IncludeResolver resolver)
		{
			return new HashSet<String>(StringComparer.OrdinalIgnoreCase) { file };
		}

		public IReadOnlySet<String> GetIncludeClosure(IEnumerable<String> seeds, IncludeResolver resolver)
		{
			return new HashSet<String>(seeds, StringComparer.OrdinalIgnoreCase);
		}
	}
}
