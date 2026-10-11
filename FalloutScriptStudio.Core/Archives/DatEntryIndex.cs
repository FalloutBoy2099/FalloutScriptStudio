namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The entries of an archive with the case insensitive lookups the readers need.
	/// </summary>
	internal sealed class DatEntryIndex
	{
		private readonly List<DatEntry> _entries = new List<DatEntry>();
		private readonly Dictionary<String, DatEntry> _byPath = new Dictionary<String, DatEntry>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<String, List<DatEntry>> _byDirectory = new Dictionary<String, List<DatEntry>>(StringComparer.OrdinalIgnoreCase);

		public IReadOnlyList<DatEntry> Entries => this._entries;

		/// <summary>Adds an entry. When two entries have the same path the first one is the one found by <see cref="Find"/>.</summary>
		public void Add(DatEntry entry)
		{
			this._entries.Add(entry);
			this._byPath.TryAdd(entry.FullPath, entry);

			if (!this._byDirectory.TryGetValue(entry.Directory, out List<DatEntry>? list))
			{
				list = new List<DatEntry>();
				this._byDirectory.Add(entry.Directory, list);
			}

			list.Add(entry);
		}

		public DatEntry? Find(String path)
		{
			this._byPath.TryGetValue(DatPaths.NormalizePath(path), out DatEntry? entry);
			return entry;
		}

		public IReadOnlyList<DatEntry> GetEntries(String directory)
		{
			return this._byDirectory.TryGetValue(DatPaths.NormalizeDirectory(directory), out List<DatEntry>? list)
				? list
				: Array.Empty<DatEntry>();
		}
	}
}
