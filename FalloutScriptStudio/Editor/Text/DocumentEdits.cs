namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Collections.Generic;
	using ICSharpCode.AvalonEdit.Document;
	using Core.TextTools;

	/// <summary>
	/// Applies the edit lists of <c>Core.TextTools</c> to an AvalonEdit document as one undoable change and provides the
	/// few line operations the Core library does not have.
	/// </summary>
	public static class DocumentEdits
	{
		/// <summary>Applies edits given in the coordinates of the current text (they must not overlap).</summary>
		/// <param name="document">The document.</param>
		/// <param name="edits">The edits.</param>
		/// <returns>The number of edits applied.</returns>
		public static Int32 Apply(TextDocument document, IEnumerable<TextEdit> edits)
		{
			IReadOnlyList<TextEdit> ordered = TextEditApplier.Normalize(edits);
			if (ordered.Count == 0)
			{
				return 0;
			}

			using (document.RunUpdate())
			{
				for (Int32 i = ordered.Count - 1; i >= 0; i--)
				{
					TextEdit edit = ordered[i];
					document.Replace(edit.Offset, edit.Length, edit.NewText);
				}
			}

			return ordered.Count;
		}

		/// <summary>
		/// Applies one replacement. When old and new text have the same number of lines only the lines that differ are
		/// replaced, so that folds, markers and the caret on the other lines are not disturbed.
		/// </summary>
		/// <param name="document">The document.</param>
		/// <param name="edit">The replacement.</param>
		public static void ApplyLineWise(TextDocument document, TextEdit edit)
		{
			String oldText = document.GetText(edit.Offset, edit.Length);
			String[] oldLines = oldText.Split('\n');
			String[] newLines = edit.NewText.Split('\n');
			if (oldLines.Length != newLines.Length)
			{
				Apply(document, new[] { edit });
				return;
			}

			List<TextEdit> edits = [];
			Int32 offset = edit.Offset;
			for (Int32 i = 0; i < oldLines.Length; i++)
			{
				if (!String.Equals(oldLines[i], newLines[i], StringComparison.Ordinal))
				{
					edits.Add(new TextEdit(offset, oldLines[i].Length, newLines[i]));
				}

				offset += oldLines[i].Length + 1;
			}

			Apply(document, edits);
		}

		/// <summary>Puts one indentation level in front of every non-blank line of a selection.</summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span is the caret, which selects its line.</param>
		/// <param name="indentation">One level (a tab or spaces).</param>
		/// <returns>One insertion per line.</returns>
		public static IReadOnlyList<TextEdit> IncreaseIndent(String text, TextSpan selection, String indentation)
		{
			TextLineMap map = new(text);
			FalloutScriptStudio.Core.Parsing.LineRange lines = map.GetSelectedLines(selection);
			List<TextEdit> edits = [];
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				if (!map.IsBlank(line))
				{
					edits.Add(TextEdit.Insert(map.GetLineStart(line), indentation));
				}
			}

			return edits;
		}

		/// <summary>Comments the selected lines, or uncomments them when every non-blank line is commented already.</summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span is the caret, which selects its line.</param>
		/// <returns>The edits.</returns>
		public static IReadOnlyList<TextEdit> ToggleLineComment(String text, TextSpan selection)
		{
			TextLineMap map = new(text);
			FalloutScriptStudio.Core.Parsing.LineRange lines = map.GetSelectedLines(selection);
			Boolean allCommented = true;
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				String trimmed = map.GetLineText(line).TrimStart();
				if (trimmed.Length > 0 && !trimmed.StartsWith(CommentTools.LineComment, StringComparison.Ordinal))
				{
					allCommented = false;
					break;
				}
			}

			return allCommented ? CommentTools.UncommentLines(text, selection) : CommentTools.CommentLines(text, selection);
		}
	}
}