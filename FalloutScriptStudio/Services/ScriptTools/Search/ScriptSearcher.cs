namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// Searches open documents and the files of a folder (the "search in files" of the legacy editor), cancellable and with progress.
	/// Stateless: every run works on its own request.
	/// </summary>
	public sealed class ScriptSearcher
	{
		private const Int64 MaxFileBytes = 32L * 1024 * 1024;
		private const Int32 BinaryProbeBytes = 8000;
		private const Int32 ProgressIntervalMilliseconds = 60;

		/// <summary>
		/// Runs a search on a worker thread.
		/// </summary>
		/// <param name="request">What to search.</param>
		/// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The matching lines.</returns>
		/// <exception cref="OperationCanceledException">The search was cancelled.</exception>
		public Task<SearchResult> SearchAsync(SearchRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			return Task.Run(() => this.Search(request, progress, cancellationToken), cancellationToken);
		}

		/// <summary>
		/// Runs a search on the calling thread.
		/// </summary>
		/// <param name="request">What to search.</param>
		/// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The matching lines.</returns>
		/// <exception cref="OperationCanceledException">The search was cancelled.</exception>
		public SearchResult Search(SearchRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			List<SearchHit> hits = new List<SearchHit>();
			Int32 searched = 0;
			Int32 withMatches = 0;
			Int32 skipped = 0;
			Boolean truncated = false;
			Stopwatch clock = Stopwatch.StartNew();
			Int64 lastReport = -ProgressIntervalMilliseconds;

			IReadOnlyList<String> files = request.Folder is null ? Array.Empty<String>() : EnumerateFiles(request.Folder, cancellationToken);
			Int32 total = request.Sources.Count + files.Count;
			progress?.Report(new SearchProgress(0, total, 0));

			void Visit(String path, String? text)
			{
				searched++;
				if (text is null)
				{
					skipped++;
				}
				else
				{
					Int32 before = hits.Count;
					truncated |= !SearchText(path, text, request.Matcher, hits, request.MaxHits - hits.Count, cancellationToken);
					if (hits.Count > before)
					{
						withMatches++;
					}
				}

				if (progress is not null && (clock.ElapsedMilliseconds - lastReport >= ProgressIntervalMilliseconds || searched == total))
				{
					lastReport = clock.ElapsedMilliseconds;
					progress.Report(new SearchProgress(searched, total, hits.Count));
				}
			}

			foreach (SearchTextSource source in request.Sources)
			{
				cancellationToken.ThrowIfCancellationRequested();
				Visit(source.Path, source.Text);
				if (truncated)
				{
					return new SearchResult(hits, searched, withMatches, skipped, true);
				}
			}

			foreach (String file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				String? text = request.Folder!.OpenTexts.TryGetValue(file, out String? openText) ? openText : ReadText(file, request.FallbackEncoding);
				Visit(file, text);
				if (truncated)
				{
					break;
				}
			}

			progress?.Report(new SearchProgress(searched, total, hits.Count));
			return new SearchResult(hits, searched, withMatches, skipped, truncated);
		}

		/// <summary>
		/// Searches one text and adds a hit for every line that contains a match.
		/// </summary>
		/// <param name="path">The file the text belongs to.</param>
		/// <param name="text">The text.</param>
		/// <param name="matcher">What to look for.</param>
		/// <param name="hits">Receives the hits.</param>
		/// <param name="maxHits">The number of hits that may still be added.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns><see langword="false"/> when the search stopped because <paramref name="maxHits"/> was reached.</returns>
		public static Boolean SearchText(String path, String text, SearchMatcher matcher, IList<SearchHit> hits, Int32 maxHits, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(matcher);
			ArgumentNullException.ThrowIfNull(hits);

			if (!matcher.Options.UseRegex && text.IndexOf(matcher.Options.Text, matcher.Options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) < 0)
			{
				return true;
			}

			TextLineMap map = new TextLineMap(text);
			Int32 added = 0;
			for (Int32 line = 1; line <= map.LineCount; line++)
			{
				if ((line & 0x7FF) == 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}

				if (map.GetLineLength(line) == 0)
				{
					continue;
				}

				String lineText = map.GetLineText(line);
				LineMatch? match = matcher.FindFirst(lineText, 0);
				if (match is null)
				{
					continue;
				}

				if (added >= maxHits)
				{
					return false;
				}

				hits.Add(new SearchHit(path, line, match.Index + 1, match.Length, lineText));
				added++;
			}

			return true;
		}

		/// <summary>
		/// Lists the files of a folder that match the masks, in path order.
		/// </summary>
		/// <param name="folder">The folder.</param>
		/// <param name="cancellationToken">Cancels the listing.</param>
		/// <returns>The full paths.</returns>
		public static IReadOnlyList<String> EnumerateFiles(SearchFolder folder, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(folder);

			EnumerationOptions options = new EnumerationOptions
			{
				RecurseSubdirectories = folder.IncludeSubfolders,
				IgnoreInaccessible = true,
				MatchType = MatchType.Simple,
				MatchCasing = MatchCasing.CaseInsensitive,
			};

			HashSet<String> seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			List<String> files = new List<String>();
			if (!Directory.Exists(folder.Directory))
			{
				return files;
			}

			foreach (String mask in folder.FileMasks)
			{
				foreach (String file in Directory.EnumerateFiles(folder.Directory, mask, options))
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (seen.Add(file))
					{
						files.Add(file);
					}
				}
			}

			files.Sort(StringComparer.OrdinalIgnoreCase);
			return files;
		}

		private static String? ReadText(String path, Encoding fallback)
		{
			try
			{
				FileInfo info = new FileInfo(path);
				if (info.Length > MaxFileBytes)
				{
					return null;
				}

				Byte[] bytes = File.ReadAllBytes(path);
				return LooksBinary(bytes) ? null : ScriptTextFile.Decode(bytes, fallback).Text;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}

		private static Boolean LooksBinary(Byte[] bytes)
		{
			if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
			{
				return false;
			}

			Int32 count = Math.Min(bytes.Length, BinaryProbeBytes);
			for (Int32 i = 0; i < count; i++)
			{
				if (bytes[i] == 0)
				{
					return true;
				}
			}

			return false;
		}
	}
}
