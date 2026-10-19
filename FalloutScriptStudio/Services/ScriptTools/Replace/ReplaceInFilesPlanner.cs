namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.TextTools;
	using Search;
	using Workspace;

	/// <summary>
	/// The first phase of "Replace in Files": finds every replacement in the open documents and in the files of a folder, without changing
	/// anything. The files are read one at a time and only the matches are kept (never the text of a file), so a run over thousands of files
	/// stays small on a 32-bit process; the hash of what was read lets the second phase refuse a file that changed meanwhile.
	/// </summary>
	public sealed class ReplaceInFilesPlanner
	{
		/// <summary>The largest file that is read (the same limit as the search).</summary>
		public const Int64 MaxFileBytes = 32L * 1024 * 1024;

		/// <summary>The longest part of a line that is kept before and after a match.</summary>
		public const Int32 ContextLength = 80;

		private const Int32 BinaryProbeBytes = 8000;
		private const Int32 ProgressIntervalMilliseconds = 60;
		private const Int32 CancellationCheckMask = 0x7FF;

		/// <summary>Runs the first phase on a worker thread.</summary>
		/// <param name="request">What to replace.</param>
		/// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>The plan.</returns>
		/// <exception cref="OperationCanceledException">The run was cancelled.</exception>
		/// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The regular expression took too long on a line.</exception>
		public Task<ReplacePlan> PlanAsync(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);
			return Task.Run(() => this.Plan(request, progress, cancellationToken), cancellationToken);
		}

		/// <summary>Runs the first phase on the calling thread.</summary>
		/// <param name="request">What to replace.</param>
		/// <param name="progress">Receives progress reports; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>The plan.</returns>
		public ReplacePlan Plan(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			List<ReplaceFilePlan> files = [];
			List<ReplaceSkip> skipped = [];
			Int32 matchCount = 0;
			Int32 searched = 0;
			Boolean truncated = false;
			Stopwatch clock = Stopwatch.StartNew();
			Int64 lastReport = -ProgressIntervalMilliseconds;

			IReadOnlyList<String> paths = request.Folder is null ? Array.Empty<String>() : ScriptSearcher.EnumerateFiles(request.Folder, cancellationToken);
			Int32 total = request.Sources.Count + paths.Count;
			progress?.Report(new SearchProgress(0, total, 0));

			void Report()
			{
				if (progress is not null && (clock.ElapsedMilliseconds - lastReport >= ProgressIntervalMilliseconds || searched == total))
				{
					lastReport = clock.ElapsedMilliseconds;
					progress.Report(new SearchProgress(searched, total, matchCount));
				}
			}

			void AddFile(ReplaceFilePlan? plan)
			{
				if (plan is not null)
				{
					files.Add(plan);
					matchCount += plan.Matches.Count;
				}
			}

			foreach (ReplaceTextSource source in request.Sources)
			{
				cancellationToken.ThrowIfCancellationRequested();
				searched++;
				Report();
				AddFile(PlanText(source.Path, source.Text, Hash(source.Text), source.Owner, request, request.MaxMatches - matchCount, cancellationToken, ref truncated));
				if (truncated)
				{
					return Finish(files, skipped, searched, truncated, request);
				}
			}

			foreach (String path in paths)
			{
				cancellationToken.ThrowIfCancellationRequested();
				searched++;
				Report();
				ReplaceFilePlan? plan = request.OpenDocuments.TryGetValue(path, out ReplaceTextSource? open)
					? PlanText(open.Path, open.Text, Hash(open.Text), open.Owner, request, request.MaxMatches - matchCount, cancellationToken, ref truncated)
					: PlanFile(path, request, request.MaxMatches - matchCount, skipped, cancellationToken, ref truncated);
				AddFile(plan);
				if (truncated)
				{
					break;
				}
			}

			progress?.Report(new SearchProgress(searched, total, matchCount));
			return Finish(files, skipped, searched, truncated, request);
		}

		/// <summary>Gets the SHA-256 of the characters of a text (what the plan of an open document is compared with).</summary>
		/// <param name="text">The text.</param>
		/// <returns>The hash.</returns>
		public static Byte[] Hash(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return SHA256.HashData(MemoryMarshal.AsBytes(text.AsSpan()));
		}

		/// <summary>Finds the replacements of one text.</summary>
		/// <param name="text">The text.</param>
		/// <param name="matcher">What to look for.</param>
		/// <param name="replacement">The replacement.</param>
		/// <param name="limit">The most matches to return.</param>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <param name="truncated">Set when the text has more matches than <paramref name="limit"/>.</param>
		/// <returns>The matches in text order.</returns>
		public static IReadOnlyList<ReplaceMatch> FindMatches(String text, SearchMatcher matcher, String replacement, Int32 limit, CancellationToken cancellationToken, out Boolean truncated)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(matcher);
			ArgumentNullException.ThrowIfNull(replacement);

			truncated = false;
			List<ReplaceMatch> matches = [];
			if (!matcher.Options.UseRegex && text.IndexOf(matcher.Options.Text, matcher.Options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) < 0)
			{
				return matches;
			}

			TextLineMap map = new(text);
			for (Int32 line = 1; line <= map.LineCount; line++)
			{
				if ((line & CancellationCheckMask) == 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}

				if (map.GetLineLength(line) == 0)
				{
					continue;
				}

				String lineText = map.GetLineText(line);
				Int32 lineStart = map.GetLineStart(line);
				foreach (LineMatch match in matcher.FindAll(lineText))
				{
					if (matches.Count >= limit)
					{
						truncated = true;
						return matches;
					}

					String matched = lineText.Substring(match.Index, match.Length);
					matches.Add(new ReplaceMatch(
						matches.Count,
						line,
						match.Index + 1,
						lineStart + match.Index,
						match.Length,
						matched,
						matcher.Expand(match, replacement),
						Cut(lineText, Math.Max(0, match.Index - ContextLength), match.Index, true),
						Cut(lineText, match.Index + match.Length, Math.Min(lineText.Length, match.Index + match.Length + ContextLength), false)));
				}
			}

			return matches;
		}

		/// <summary>Reads a file and decides whether it can be replaced: not too large, not binary.</summary>
		/// <param name="path">The file.</param>
		/// <param name="bytes">The content.</param>
		/// <returns><see langword="true"/> when the file was read and looks like text.</returns>
		internal static Boolean TryRead(String path, out Byte[] bytes)
		{
			bytes = Array.Empty<Byte>();
			FileInfo info = new(path);
			if (!info.Exists || info.Length > MaxFileBytes)
			{
				return false;
			}

			bytes = SharedFile.ReadAllBytes(path);
			return !LooksBinary(bytes);
		}

		private static ReplacePlan Finish(List<ReplaceFilePlan> files, List<ReplaceSkip> skipped, Int32 searched, Boolean truncated, ReplaceInFilesRequest request)
		{
			return new ReplacePlan(files, skipped, searched, truncated, request.FallbackEncoding);
		}

		private static String Cut(String line, Int32 start, Int32 end, Boolean leading)
		{
			String part = line.Substring(start, end - start);
			Boolean cut = leading ? start > 0 : end < line.Length;
			if (leading)
			{
				part = part.TrimStart();
				return cut ? "…" + part : part;
			}

			return cut ? part + "…" : part;
		}

		private static ReplaceFilePlan? PlanText(String path, String text, Byte[] hash, Object? owner, ReplaceInFilesRequest request, Int32 limit, CancellationToken cancellationToken, ref Boolean truncated)
		{
			IReadOnlyList<ReplaceMatch> matches = FindMatches(text, request.Matcher, request.Replacement, limit, cancellationToken, out Boolean cut);
			truncated |= cut;
			return matches.Count == 0 ? null : new ReplaceFilePlan(path, hash, matches, owner);
		}

		private static ReplaceFilePlan? PlanFile(String path, ReplaceInFilesRequest request, Int32 limit, List<ReplaceSkip> skipped, CancellationToken cancellationToken, ref Boolean truncated)
		{
			Byte[] bytes;
			try
			{
				if (!TryRead(path, out bytes))
				{
					return null;
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A locked file cannot be searched: the user is told once the run ended, when it may have matches.
				skipped.Add(new ReplaceSkip(path, ReplaceSkipReason.Unreadable, 0));
				return null;
			}

			Byte[] hash = SHA256.HashData(bytes);
			String text = DocumentTextCodec.Decode(bytes, request.FallbackEncoding).Text;
			bytes = Array.Empty<Byte>();
			ReplaceFilePlan? plan = PlanText(path, text, hash, null, request, limit, cancellationToken, ref truncated);
			if (plan is not null && IsReadOnly(path))
			{
				skipped.Add(new ReplaceSkip(path, ReplaceSkipReason.ReadOnly, plan.Matches.Count));
				return null;
			}

			return plan;
		}

		private static Boolean IsReadOnly(String path)
		{
			try
			{
				return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return false;
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