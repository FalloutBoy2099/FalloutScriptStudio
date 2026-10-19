namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>
	/// Comment / uncomment lines and toggle block comments (legacy <c>Utilities.CommentText</c>, <c>UnCommentText</c> and the
	/// editor library's <c>ToggleBlockComment</c>).
	/// </summary>
	public static class CommentTools
	{
		/// <summary>The line comment marker of SSL.</summary>
		public const String LineComment = "//";

		/// <summary>The start of a block comment.</summary>
		public const String BlockCommentStart = "/*";

		/// <summary>The end of a block comment.</summary>
		public const String BlockCommentEnd = "*/";

		/// <summary>
		/// Puts <c>//</c> in front of the first non-blank character of each selected line that is not already commented.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span is the caret, which selects its line.</param>
		/// <returns>One insertion per line.</returns>
		public static IReadOnlyList<TextEdit> CommentLines(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			var map = new TextLineMap(text);
			LineRange lines = map.GetSelectedLines(selection);
			var edits = new List<TextEdit>();
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				String lineText = map.GetLineText(line);
				String trimmed = lineText.TrimStart();
				if (trimmed.StartsWith(LineComment, StringComparison.Ordinal))
				{
					continue;
				}

				edits.Add(TextEdit.Insert(map.GetLineStart(line) + lineText.Length - trimmed.Length, LineComment));
			}

			return edits;
		}

		/// <summary>
		/// Removes the leading <c>//</c> of each selected line that starts with a line comment.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span is the caret, which selects its line.</param>
		/// <returns>One deletion per line.</returns>
		public static IReadOnlyList<TextEdit> UncommentLines(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			var map = new TextLineMap(text);
			LineRange lines = map.GetSelectedLines(selection);
			var edits = new List<TextEdit>();
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				String lineText = map.GetLineText(line);
				if (!lineText.TrimStart().StartsWith(LineComment, StringComparison.Ordinal))
				{
					continue;
				}

				edits.Add(TextEdit.Delete(map.GetLineStart(line) + lineText.IndexOf(LineComment, StringComparison.Ordinal), LineComment.Length));
			}

			return edits;
		}

		/// <summary>
		/// Wraps the selection in <c>/* */</c>, or removes the block comment that contains or touches the selection.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection (a caret inside a block comment removes that comment).</param>
		/// <returns>The edits: two insertions, two deletions, or none (empty selection outside a comment).</returns>
		public static IReadOnlyList<TextEdit> ToggleBlockComment(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			TextSpan? region = FindBlockCommentRegion(text, selection, out TextSpan endMarker);
			if (region is not null)
			{
				return new[]
				{
					TextEdit.Delete(region.Value.Offset, BlockCommentStart.Length),
					TextEdit.Delete(endMarker.Offset, endMarker.Length),
				};
			}

			if (selection.IsEmpty)
			{
				return Array.Empty<TextEdit>();
			}

			return new[]
			{
				TextEdit.Insert(selection.Offset, BlockCommentStart),
				TextEdit.Insert(selection.End, BlockCommentEnd),
			};
		}

		/// <summary>
		/// Finds the block comment that contains, or partly overlaps, a selection.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection.</param>
		/// <param name="endMarker">Receives the position of the closing <c>*/</c>.</param>
		/// <returns>The position of the opening <c>/*</c>, or <c>null</c> when there is no such comment.</returns>
		public static TextSpan? FindBlockCommentRegion(String text, TextSpan selection, out TextSpan endMarker)
		{
			endMarker = default;
			if (text.Length == 0)
			{
				return null;
			}

			Int32 selectionStart = selection.Offset;
			Int32 selectionEnd = selection.End;
			String selected = text.Substring(selectionStart, selection.Length);

			Int32 commentStart = selected.IndexOf(BlockCommentStart, StringComparison.Ordinal);
			Int32 commentEnd;
			if (commentStart >= 0)
			{
				commentEnd = selected.IndexOf(BlockCommentEnd, commentStart + BlockCommentStart.Length, StringComparison.Ordinal);
				commentStart += selectionStart;
			}
			else
			{
				commentEnd = selected.IndexOf(BlockCommentEnd, StringComparison.Ordinal);
			}

			if (commentEnd >= 0)
			{
				commentEnd += selectionStart;
			}

			// The comment starts before (or partly inside) the selection.
			if (commentStart == -1)
			{
				Int32 limit = Math.Min(selectionEnd + BlockCommentStart.Length - 1, text.Length);
				String head = text.Substring(0, limit);
				commentStart = head.LastIndexOf(BlockCommentStart, StringComparison.Ordinal);
				if (commentStart >= 0)
				{
					Int32 count = Math.Max(selectionStart - commentStart, 0);
					Int32 closedBefore = head.IndexOf(BlockCommentEnd, commentStart, count, StringComparison.Ordinal);
					if (closedBefore > commentStart)
					{
						commentStart = -1;
					}
				}
			}

			// The comment ends after (or partly inside) the selection.
			if (commentEnd == -1)
			{
				Int32 from = selectionStart + 1 - BlockCommentEnd.Length;
				if (from < 0)
				{
					from = selectionStart;
				}

				Int32 found = text.IndexOf(BlockCommentEnd, from, StringComparison.Ordinal);
				commentEnd = found;
			}

			if (commentStart != -1 && commentEnd != -1)
			{
				endMarker = new TextSpan(commentEnd, BlockCommentEnd.Length);
				return new TextSpan(commentStart, BlockCommentStart.Length);
			}

			return null;
		}
	}
}