namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>An immutable view of the index: the indexed files, all the symbols and the lookup by name.</summary>
	internal sealed class IndexSnapshot
	{
		private readonly Lazy<IndexedSymbol[]> _sorted;

		internal IndexSnapshot(Dictionary<String, IndexedFile> files, Boolean truncated)
		{
			this.Files = files;
			this.IsTruncated = truncated;

			Int32 count = 0;
			foreach (IndexedFile file in files.Values)
			{
				count += file.Symbols.Length;
			}

			IndexedSymbol[] all = new IndexedSymbol[count];
			Dictionary<String, List<IndexedSymbol>> byName = new Dictionary<String, List<IndexedSymbol>>(Math.Max(count / 2, 4), StringComparer.OrdinalIgnoreCase);
			Int32 index = 0;
			foreach (IndexedFile file in files.Values)
			{
				foreach (IndexedSymbol symbol in file.Symbols)
				{
					all[index++] = symbol;
					if (!byName.TryGetValue(symbol.Name, out List<IndexedSymbol>? list))
					{
						list = new(1);
						byName.Add(symbol.Name, list);
					}

					list.Add(symbol);
				}
			}

			this.Symbols = all;
			this.ByName = new Dictionary<String, IndexedSymbol[]>(byName.Count, StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<String, List<IndexedSymbol>> pair in byName)
			{
				this.ByName.Add(pair.Key, pair.Value.ToArray());
			}

			this._sorted = new Lazy<IndexedSymbol[]>(this.Sort, true);
		}

		internal static IndexSnapshot Empty { get; } = new IndexSnapshot(new(StringComparer.OrdinalIgnoreCase), false);

		/// <summary>Gets the indexed files by full path.</summary>
		internal Dictionary<String, IndexedFile> Files { get; }

		/// <summary>Gets every symbol, in no particular order.</summary>
		internal IndexedSymbol[] Symbols { get; }

		/// <summary>Gets the symbols by name (the case is ignored).</summary>
		internal Dictionary<String, IndexedSymbol[]> ByName { get; }

		/// <summary>Gets a value indicating whether files were left out because the symbol limit was reached.</summary>
		internal Boolean IsTruncated { get; }

		/// <summary>Gets every symbol ordered by name (computed on first use).</summary>
		internal IndexedSymbol[] Sorted => this._sorted.Value;

		private IndexedSymbol[] Sort()
		{
			IndexedSymbol[] copy = (IndexedSymbol[])this.Symbols.Clone();
			Array.Sort(copy, static (a, b) =>
			{
				Int32 result = String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
				if (result != 0)
				{
					return result;
				}

				result = a.Kind.CompareTo(b.Kind);
				if (result != 0)
				{
					return result;
				}

				result = String.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
				return result != 0 ? result : a.Line.CompareTo(b.Line);
			});
			return copy;
		}
	}
}