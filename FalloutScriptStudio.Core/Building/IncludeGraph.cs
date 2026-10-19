namespace FalloutScriptStudio.Core.Building
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Parsing;

	/// <summary>
	/// The include graph of the scripts of a project: for a script it computes the transitive set of files it includes. The directives of a
	/// file are cached (keyed by the path and validated by the size and time stamp of the file), so a second computation reads no file
	/// that did not change. The graph is thread safe; one instance is shared by all runs.
	/// </summary>
	public sealed class IncludeGraph
	{
		private readonly ConcurrentDictionary<String, DirectiveEntry> _directives = new ConcurrentDictionary<String, DirectiveEntry>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the number of files whose directives are cached.</summary>
		public Int32 CachedFileCount => this._directives.Count;

		/// <summary>Starts a computation with one include resolver. The scan memorises the time stamps it reads, so it describes one moment: use a new one for every run.</summary>
		/// <param name="resolver">Finds the files of the directives (the rule of <see cref="IncludeSearch"/> decides whether the headers folder is searched).</param>
		/// <returns>The scan.</returns>
		public IncludeScan BeginScan(IncludeResolver resolver)
		{
			ArgumentNullException.ThrowIfNull(resolver);
			return new IncludeScan(this, resolver);
		}

		/// <summary>Forgets everything that was cached.</summary>
		public void Clear()
		{
			this._directives.Clear();
		}

		/// <summary>Gets the directives of a file, reading the file only when the cached ones are not valid for <paramref name="stamp"/>.</summary>
		/// <param name="file">The full path of the file.</param>
		/// <param name="stamp">The current stamp of the file.</param>
		/// <returns>The directives, or <see langword="null"/> when the file cannot be read.</returns>
		internal IReadOnlyList<IncludeDirective>? GetDirectives(String file, FileStamp stamp)
		{
			if (this._directives.TryGetValue(file, out DirectiveEntry? entry) && entry.Stamp == stamp)
			{
				return entry.Directives;
			}

			try
			{
				// Latin-1 maps every byte to one character: the paths of the directives are ASCII and no encoding can make the read fail.
				String text = SharedFile.ReadAllText(file, Encoding.Latin1);
				IReadOnlyList<IncludeDirective> directives = IncludeScanner.Scan(text);
				this._directives[file] = new DirectiveEntry(stamp, directives);
				return directives;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._directives.TryRemove(file, out _);
				return null;
			}
		}

		private sealed record DirectiveEntry(FileStamp Stamp, IReadOnlyList<IncludeDirective> Directives);
	}
}