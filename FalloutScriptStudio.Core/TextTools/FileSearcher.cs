namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Default <see cref="IFileSearcher"/>, built on <see cref="ITextSearcher"/>.
	/// </summary>
	public sealed class FileSearcher : IFileSearcher
	{
		private readonly ITextSearcher _searcher;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileSearcher"/> class.
		/// </summary>
		/// <param name="searcher">The text searcher.</param>
		public FileSearcher(ITextSearcher searcher)
		{
			ArgumentNullException.ThrowIfNull(searcher);
			this._searcher = searcher;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<String>> FindFilesAsync(IEnumerable<String> files, SearchQuery query, Encoding? encoding, IProgress<Int32>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(query);

			var result = new List<String>();
			Int32 done = 0;
			foreach (String file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String? text = await ReadAsync(file, encoding, cancellationToken).ConfigureAwait(false);
				if (text is not null && this._searcher.IsMatch(text, query))
				{
					result.Add(file);
				}

				progress?.Report(++done);
			}

			return result;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<SearchResultRow>> FindRowsAsync(IEnumerable<String> files, SearchQuery query, Encoding? encoding, IProgress<Int32>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(query);

			var result = new List<SearchResultRow>();
			Int32 done = 0;
			foreach (String file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String? text = await ReadAsync(file, encoding, cancellationToken).ConfigureAwait(false);
				if (text is not null)
				{
					result.AddRange(this._searcher.FindAllLines(text, Path.GetFullPath(file), query));
				}

				progress?.Report(++done);
			}

			return result;
		}

		private static async Task<String?> ReadAsync(String file, Encoding? encoding, CancellationToken cancellationToken)
		{
			try
			{
				return await File.ReadAllTextAsync(file, encoding ?? Encoding.UTF8, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}
	}
}
