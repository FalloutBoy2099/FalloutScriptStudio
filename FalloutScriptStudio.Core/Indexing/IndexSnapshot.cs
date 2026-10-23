namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Globals;

	/// <summary>An immutable view of the index: the indexed files, all the symbols and the lookup by name.</summary>
	internal sealed class IndexSnapshot
	{
		private readonly Lazy<IndexedSymbol[]> _sorted;
		private readonly Lazy<GlobalVariableIndex> _globals;

		internal IndexSnapshot(Dictionary<String, IndexedFile> files, Boolean truncated, Boolean globalsTruncated = false)
		{
			this.Files = files;
			this.IsTruncated = truncated;
			this.GlobalsTruncated = globalsTruncated;

			Int32 count = 0;
			Int32 uses = 0;
			foreach (IndexedFile file in files.Values)
			{
				count += file.Symbols.Length;
				uses += file.Globals?.Uses.Length ?? 0;
			}

			this.GlobalUseCount = uses;

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
			this._globals = new Lazy<GlobalVariableIndex>(this.BuildGlobals, true);
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

		/// <summary>Gets a value indicating whether files were left out of the global variable uses because the limit was reached.</summary>
		internal Boolean GlobalsTruncated { get; }

		/// <summary>Gets the number of uses of global variables kept for all the files.</summary>
		internal Int32 GlobalUseCount { get; }

		/// <summary>Gets the global variables of the files (computed on first use, dropped with the snapshot).</summary>
		internal GlobalVariableIndex Globals => this._globals.Value;

		/// <summary>Gets every symbol ordered by name (computed on first use).</summary>
		internal IndexedSymbol[] Sorted => this._sorted.Value;

		private GlobalVariableIndex BuildGlobals()
		{
			if (this.GlobalUseCount == 0 && !this.GlobalsTruncated)
			{
				return GlobalVariableIndex.Empty;
			}

			return GlobalVariableIndex.Build(
				this.Files.Values.Where(static f => f.Globals is not null).Select(static f => new KeyValuePair<String, FileGlobalScan>(f.Path, f.Globals!)),
				this.GlobalsTruncated);
		}

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