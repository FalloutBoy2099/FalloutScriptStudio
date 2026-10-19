namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Concurrent;
	using System.IO;

	/// <summary>
	/// Remembers the symbols of the header files the scripts include, so that a header is read and analysed once, not at every pause in the
	/// typing. An entry is used while the size and the modification time of the file are the same; a file that was written less than two
	/// seconds ago is always read again (its time stamp may not tell two saves apart yet). The cache is safe to use from several threads.
	/// </summary>
	public sealed class IncludeSymbolCache
	{
		/// <summary>The number of files the cache holds before it is emptied.</summary>
		public const Int32 MaximumEntries = 2000;

		private static readonly TimeSpan _recentlyModified = TimeSpan.FromSeconds(2);

		private readonly ConcurrentDictionary<String, Entry> _entries = new ConcurrentDictionary<String, Entry>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the number of files in the cache.</summary>
		public Int32 Count => this._entries.Count;

		/// <summary>Gets the symbols of a file, reading and analysing it only when the cache does not have the current version.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <param name="read">Reads the text of the file; returns <see langword="null"/> when it cannot be read.</param>
		/// <returns>The symbols, or <see langword="null"/> when the file does not exist or cannot be read.</returns>
		public ScriptSymbolTable? GetOrRead(String path, Func<String, String?> read)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(read);

			FileInfo info = new FileInfo(path);
			if (!info.Exists)
			{
				this._entries.TryRemove(path, out _);
				return null;
			}

			Int64 ticks = info.LastWriteTimeUtc.Ticks;
			Int64 length = info.Length;
			if (this._entries.TryGetValue(path, out Entry? known) && known.Ticks == ticks && known.Length == length)
			{
				return known.Table;
			}

			String? text = read(path);
			if (text is null)
			{
				return null;
			}

			ScriptSymbolTable table = ScriptSymbolTable.Extract(text);
			if (DateTime.UtcNow - info.LastWriteTimeUtc > _recentlyModified)
			{
				if (this._entries.Count >= MaximumEntries)
				{
					this._entries.Clear();
				}

				this._entries[path] = new Entry(ticks, length, table);
			}

			return table;
		}

		/// <summary>Forgets everything.</summary>
		public void Clear()
		{
			this._entries.Clear();
		}

		private sealed record Entry(Int64 Ticks, Int64 Length, ScriptSymbolTable Table);
	}
}