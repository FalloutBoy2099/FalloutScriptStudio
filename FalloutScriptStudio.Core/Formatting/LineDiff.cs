namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using TextTools;

	/// <summary>
	/// Finds the lines that differ between a text and its formatted version, as a list of edits, so that the editor changes only those
	/// lines (folds, markers and the caret on the other lines stay where they are) and so that a selection can be formatted by applying
	/// only the changes that touch it.
	/// </summary>
	public static class LineDiff
	{
		/// <summary>The largest number of differing lines the exact algorithm follows; beyond it the whole changed region is one edit.</summary>
		private const Int32 MaxDifferences = 1500;

		/// <summary>Computes the edits that turn one text into another.</summary>
		/// <param name="oldText">The original text.</param>
		/// <param name="newText">The new text.</param>
		/// <returns>The edits in the coordinates of the original text, in order, without overlap; empty when the texts are equal.</returns>
		public static IReadOnlyList<TextEdit> Compute(String oldText, String newText)
		{
			ArgumentNullException.ThrowIfNull(oldText);
			ArgumentNullException.ThrowIfNull(newText);

			if (String.Equals(oldText, newText, StringComparison.Ordinal))
			{
				return Array.Empty<TextEdit>();
			}

			List<(Int32 Start, Int32 Length)> oldLines = Split(oldText);
			List<(Int32 Start, Int32 Length)> newLines = Split(newText);
			List<TextEdit> edits = [];
			foreach (Hunk hunk in FindHunks(oldText, newText, oldLines, newLines, true))
			{
				edits.Add(CreateEdit(oldText, newText, oldLines, newLines, hunk));
			}

			return edits;
		}

		/// <summary>Finds the groups of lines that differ between two texts.</summary>
		/// <param name="oldText">The original text.</param>
		/// <param name="newText">The new text.</param>
		/// <returns>The changes in order, without overlap; empty when the texts are equal.</returns>
		public static IReadOnlyList<LineChange> GetChanges(String oldText, String newText)
		{
			ArgumentNullException.ThrowIfNull(oldText);
			ArgumentNullException.ThrowIfNull(newText);

			if (String.Equals(oldText, newText, StringComparison.Ordinal))
			{
				return Array.Empty<LineChange>();
			}

			List<Hunk> hunks = FindHunks(oldText, newText, Split(oldText), Split(newText), false);
			List<LineChange> changes = new(hunks.Count);
			foreach (Hunk hunk in hunks)
			{
				changes.Add(new LineChange(hunk.OldStart, hunk.OldCount, hunk.NewStart, hunk.NewCount));
			}

			return changes;
		}

		/// <summary>Counts the lines that differ: for each change the larger of the removed and the added lines.</summary>
		/// <param name="oldText">The original text.</param>
		/// <param name="newText">The new text.</param>
		/// <returns>The number of changed lines.</returns>
		public static Int32 CountChangedLines(String oldText, String newText)
		{
			Int32 count = 0;
			foreach (LineChange change in GetChanges(oldText, newText))
			{
				count += Math.Max(change.OldCount, change.NewCount);
			}

			return count;
		}

		private static List<Hunk> FindHunks(String oldText, String newText, List<(Int32 Start, Int32 Length)> oldLines, List<(Int32 Start, Int32 Length)> newLines, Boolean splitLines)
		{
			Int32 prefix = 0;
			while (prefix < oldLines.Count && prefix < newLines.Count && Same(oldText, oldLines[prefix], newText, newLines[prefix]))
			{
				prefix++;
			}

			Int32 suffix = 0;
			while (suffix < oldLines.Count - prefix && suffix < newLines.Count - prefix
				&& Same(oldText, oldLines[oldLines.Count - 1 - suffix], newText, newLines[newLines.Count - 1 - suffix]))
			{
				suffix++;
			}

			Int32 oldCount = oldLines.Count - prefix - suffix;
			Int32 newCount = newLines.Count - prefix - suffix;
			if (oldCount == newCount)
			{
				return FindPairwiseHunks(oldText, newText, oldLines, newLines, prefix, oldCount, splitLines);
			}

			return Myers(oldText, newText, oldLines, newLines, prefix, oldCount, newCount) ?? [new Hunk(prefix, oldCount, prefix, newCount)];
		}

		/// <summary>Keeps the edits that touch a range of lines of the original text.</summary>
		/// <param name="text">The original text.</param>
		/// <param name="edits">The edits that turn it into the formatted text.</param>
		/// <param name="selection">The range; an edit that overlaps it, touches it or lies in a line it touches is kept.</param>
		/// <returns>The edits to apply.</returns>
		public static IReadOnlyList<TextEdit> Restrict(String text, IReadOnlyList<TextEdit> edits, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(edits);

			TextLineMap map = new TextLineMap(text);
			Int32 first = map.GetLineFromOffset(selection.Offset);
			Int32 last = map.GetLineFromOffset(Math.Max(selection.End, selection.Offset));
			if (selection.Length > 0 && selection.End > selection.Offset && selection.End == map.GetLineStart(last) && last > first)
			{
				last--;
			}

			Int32 from = map.GetLineStart(first);
			Int32 to = map.GetNextLineStart(last);
			List<TextEdit> kept = [];
			foreach (TextEdit edit in edits)
			{
				Boolean insertion = edit.Length == 0;
				if (insertion ? edit.Offset >= from && edit.Offset < to || (edit.Offset == to && last == map.LineCount) : edit.Offset < to && edit.End > from)
				{
					kept.Add(edit);
				}
			}

			return kept;
		}

		private static List<Hunk> FindPairwiseHunks(String oldText, String newText, List<(Int32 Start, Int32 Length)> oldLines, List<(Int32 Start, Int32 Length)> newLines, Int32 prefix, Int32 count, Boolean splitLines)
		{
			List<Hunk> hunks = [];
			Int32 i = 0;
			while (i < count)
			{
				if (Same(oldText, oldLines[prefix + i], newText, newLines[prefix + i]))
				{
					i++;
					continue;
				}

				Int32 start = i;
				while (i < count && !Same(oldText, oldLines[prefix + i], newText, newLines[prefix + i]))
				{
					i++;
				}

				if (splitLines)
				{
					// Every differing line is its own edit, so that an edit never reaches into lines that did not change.
					for (Int32 line = start; line < i; line++)
					{
						hunks.Add(new Hunk(prefix + line, 1, prefix + line, 1));
					}
				}
				else
				{
					hunks.Add(new Hunk(prefix + start, i - start, prefix + start, i - start));
				}
			}

			return hunks;
		}

		private static TextEdit CreateEdit(String oldText, String newText, List<(Int32 Start, Int32 Length)> oldLines, List<(Int32 Start, Int32 Length)> newLines, Hunk hunk)
		{
			Int32 oldStart = hunk.OldStart < oldLines.Count ? oldLines[hunk.OldStart].Start : oldText.Length;
			Int32 oldEnd = hunk.OldCount == 0 ? oldStart : oldLines[hunk.OldStart + hunk.OldCount - 1].Start + oldLines[hunk.OldStart + hunk.OldCount - 1].Length;
			Int32 newStart = hunk.NewStart < newLines.Count ? newLines[hunk.NewStart].Start : newText.Length;
			Int32 newEnd = hunk.NewCount == 0 ? newStart : newLines[hunk.NewStart + hunk.NewCount - 1].Start + newLines[hunk.NewStart + hunk.NewCount - 1].Length;
			return new TextEdit(oldStart, oldEnd - oldStart, newText.Substring(newStart, newEnd - newStart));
		}

		/// <summary>Splits a text into lines that keep their line break.</summary>
		private static List<(Int32 Start, Int32 Length)> Split(String text)
		{
			List<(Int32, Int32)> lines = new((text.Length / 24) + 4);
			Int32 start = 0;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
				{
					lines.Add((start, i + 1 - start));
					start = i + 1;
				}
			}

			if (start < text.Length)
			{
				lines.Add((start, text.Length - start));
			}

			return lines;
		}

		private static Boolean Same(String a, (Int32 Start, Int32 Length) lineA, String b, (Int32 Start, Int32 Length) lineB)
		{
			return lineA.Length == lineB.Length && String.CompareOrdinal(a, lineA.Start, b, lineB.Start, lineA.Length) == 0;
		}

		/// <summary>The O(ND) difference algorithm of Myers on the lines between the common prefix and suffix; <see langword="null"/> when the texts differ too much.</summary>
		private static List<Hunk>? Myers(String oldText, String newText, List<(Int32 Start, Int32 Length)> oldLines, List<(Int32 Start, Int32 Length)> newLines, Int32 offset, Int32 n, Int32 m)
		{
			if (n == 0 || m == 0)
			{
				return [new Hunk(offset, n, offset, m)];
			}

			Int32 max = Math.Min(n + m, MaxDifferences);
			Int32 size = (2 * max) + 3;
			Int32[] v = new Int32[size];
			List<Int32[]> trace = [];
			Int32 middle = max + 1;
			Int32 found = -1;
			for (Int32 d = 0; d <= max && found < 0; d++)
			{
				trace.Add((Int32[])v.Clone());
				for (Int32 k = -d; k <= d; k += 2)
				{
					Int32 x = k == -d || (k != d && v[middle + k - 1] < v[middle + k + 1]) ? v[middle + k + 1] : v[middle + k - 1] + 1;
					Int32 y = x - k;
					while (x < n && y < m && Same(oldText, oldLines[offset + x], newText, newLines[offset + y]))
					{
						x++;
						y++;
					}

					v[middle + k] = x;
					if (x >= n && y >= m)
					{
						found = d;
						break;
					}
				}
			}

			if (found < 0)
			{
				return null;
			}

			// Walk the trace backwards to collect the matched diagonals.
			List<(Int32 X, Int32 Y)> matches = [];
			Int32 px = n;
			Int32 py = m;
			for (Int32 d = found; d > 0; d--)
			{
				Int32[] previous = trace[d];
				Int32 k = px - py;
				Boolean down = k == -d || (k != d && previous[middle + k - 1] < previous[middle + k + 1]);
				Int32 prevK = down ? k + 1 : k - 1;
				Int32 prevX = previous[middle + prevK];
				Int32 prevY = prevX - prevK;
				Int32 startX = down ? prevX : prevX + 1;
				Int32 startY = down ? prevY + 1 : prevY;
				while (px > startX && py > startY)
				{
					px--;
					py--;
					matches.Add((px, py));
				}

				px = prevX;
				py = prevY;
			}

			while (px > 0 && py > 0)
			{
				px--;
				py--;
				matches.Add((px, py));
			}

			matches.Reverse();
			List<Hunk> hunks = [];
			Int32 oldPos = 0;
			Int32 newPos = 0;
			foreach ((Int32 mx, Int32 my) in matches)
			{
				if (mx > oldPos || my > newPos)
				{
					hunks.Add(new Hunk(offset + oldPos, mx - oldPos, offset + newPos, my - newPos));
				}

				oldPos = mx + 1;
				newPos = my + 1;
			}

			if (oldPos < n || newPos < m)
			{
				hunks.Add(new Hunk(offset + oldPos, n - oldPos, offset + newPos, m - newPos));
			}

			return hunks;
		}

		private readonly record struct Hunk(Int32 OldStart, Int32 OldCount, Int32 NewStart, Int32 NewCount);
	}
}