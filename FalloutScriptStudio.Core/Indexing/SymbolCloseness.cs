namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>Orders the candidates of a symbol by how close they are to the file the user is working in.</summary>
	public static class SymbolCloseness
	{
		/// <summary>
		/// Orders candidates: the ones in the current file, then the ones in the files it includes, then the others; inside each group the
		/// ones that have a body first, then the ones in the nearest folder, then by path and line.
		/// </summary>
		/// <param name="candidates">The candidates.</param>
		/// <param name="currentFile">The full path of the file the user is working in, or <see langword="null"/>.</param>
		/// <param name="includedFiles">The files the current file includes (directly or not), or <see langword="null"/> when unknown.</param>
		/// <returns>The ordered candidates.</returns>
		public static IReadOnlyList<IndexedSymbol> Order(IEnumerable<IndexedSymbol> candidates, String? currentFile, IReadOnlySet<String>? includedFiles = null)
		{
			ArgumentNullException.ThrowIfNull(candidates);

			String? currentDirectory = String.IsNullOrEmpty(currentFile) ? null : Path.GetDirectoryName(currentFile);
			return candidates
				.Select(symbol => (Symbol: symbol, Group: GetGroup(symbol, currentFile, includedFiles), Distance: GetDistance(currentDirectory, Path.GetDirectoryName(symbol.File))))
				.OrderBy(item => item.Group)
				.ThenBy(item => item.Symbol.HasBody ? 0 : 1)
				.ThenBy(item => item.Distance)
				.ThenBy(item => item.Symbol.File, StringComparer.OrdinalIgnoreCase)
				.ThenBy(item => item.Symbol.Line)
				.Select(item => item.Symbol)
				.ToList();
		}

		/// <summary>Counts the folder steps between two folders (up to the common parent and down again).</summary>
		/// <param name="from">The first folder, or <see langword="null"/>.</param>
		/// <param name="to">The second folder, or <see langword="null"/>.</param>
		/// <returns>The number of steps; a large number when one of them is unknown.</returns>
		public static Int32 GetDistance(String? from, String? to)
		{
			if (String.IsNullOrEmpty(from) || String.IsNullOrEmpty(to))
			{
				return Int32.MaxValue / 2;
			}

			String[] a = Split(from);
			String[] b = Split(to);
			Int32 common = 0;
			while (common < a.Length && common < b.Length && String.Equals(a[common], b[common], StringComparison.OrdinalIgnoreCase))
			{
				common++;
			}

			return (a.Length - common) + (b.Length - common);
		}

		private static Int32 GetGroup(IndexedSymbol symbol, String? currentFile, IReadOnlySet<String>? includedFiles)
		{
			if (currentFile is not null && String.Equals(symbol.File, currentFile, StringComparison.OrdinalIgnoreCase))
			{
				return 0;
			}

			return includedFiles is not null && includedFiles.Contains(symbol.File) ? 1 : 2;
		}

		private static String[] Split(String path)
		{
			return path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
		}
	}
}
