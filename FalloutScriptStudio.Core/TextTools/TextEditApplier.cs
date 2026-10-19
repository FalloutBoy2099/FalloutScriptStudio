namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// Applies lists of <see cref="TextEdit"/> to strings and maps offsets through them.
	/// </summary>
	public static class TextEditApplier
	{
		/// <summary>
		/// Applies edits (expressed in the coordinates of <paramref name="text"/>) and returns the new text.
		/// </summary>
		/// <param name="text">The original text.</param>
		/// <param name="edits">The edits; they may be given in any order but must not overlap.</param>
		/// <returns>The edited text.</returns>
		/// <exception cref="ArgumentException">An edit lies outside the text or two edits overlap.</exception>
		public static String Apply(String text, IEnumerable<TextEdit> edits)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(edits);

			IReadOnlyList<TextEdit> ordered = Normalize(edits);
			if (ordered.Count == 0)
			{
				return text;
			}

			var result = new StringBuilder(text.Length);
			Int32 position = 0;
			foreach (TextEdit edit in ordered)
			{
				if (edit.Offset < position || edit.End > text.Length)
				{
					throw new ArgumentException("An edit lies outside the text or overlaps another edit.", nameof(edits));
				}

				result.Append(text, position, edit.Offset - position);
				result.Append(edit.NewText);
				position = edit.End;
			}

			result.Append(text, position, text.Length - position);
			return result.ToString();
		}

		/// <summary>
		/// Orders edits by offset (edits at the same offset keep their relative order) and checks that they do not overlap.
		/// </summary>
		/// <param name="edits">The edits.</param>
		/// <returns>The ordered edits.</returns>
		/// <exception cref="ArgumentException">Two edits overlap or an edit has a negative offset or length.</exception>
		public static IReadOnlyList<TextEdit> Normalize(IEnumerable<TextEdit> edits)
		{
			ArgumentNullException.ThrowIfNull(edits);

			List<TextEdit> ordered = edits.OrderBy(e => e.Offset).ToList();
			Int32 position = 0;
			foreach (TextEdit edit in ordered)
			{
				if (edit.Offset < 0 || edit.Length < 0)
				{
					throw new ArgumentException("An edit has a negative offset or length.", nameof(edits));
				}

				if (edit.Offset < position)
				{
					throw new ArgumentException("Two edits overlap.", nameof(edits));
				}

				position = edit.End;
			}

			return ordered;
		}

		/// <summary>
		/// Finds the smallest single edit that turns one text into another (common prefix and suffix are kept).
		/// </summary>
		/// <param name="oldText">The original text.</param>
		/// <param name="newText">The new text.</param>
		/// <returns>The edit, or <c>null</c> when the texts are equal.</returns>
		public static TextEdit? CreateDiff(String oldText, String newText)
		{
			ArgumentNullException.ThrowIfNull(oldText);
			ArgumentNullException.ThrowIfNull(newText);

			if (String.Equals(oldText, newText, StringComparison.Ordinal))
			{
				return null;
			}

			Int32 limit = Math.Min(oldText.Length, newText.Length);
			Int32 prefix = 0;
			while (prefix < limit && oldText[prefix] == newText[prefix])
			{
				prefix++;
			}

			Int32 suffix = 0;
			while (suffix < limit - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
			{
				suffix++;
			}

			return new TextEdit(prefix, oldText.Length - prefix - suffix, newText.Substring(prefix, newText.Length - prefix - suffix));
		}

		/// <summary>
		/// Maps an offset of the original text to the corresponding offset of the edited text (to move a caret).
		/// </summary>
		/// <param name="offset">The offset in the original text.</param>
		/// <param name="edits">The edits that were applied.</param>
		/// <returns>
		/// The new offset. An offset inside a replaced range moves to the end of its replacement; an offset exactly at an
		/// insertion moves after the inserted text.
		/// </returns>
		public static Int32 MapOffset(Int32 offset, IEnumerable<TextEdit> edits)
		{
			ArgumentNullException.ThrowIfNull(edits);

			Int32 delta = 0;
			foreach (TextEdit edit in Normalize(edits))
			{
				if (edit.End <= offset)
				{
					delta += edit.NewText.Length - edit.Length;
				}
				else if (edit.Offset < offset)
				{
					return edit.Offset + delta + edit.NewText.Length;
				}
				else
				{
					break;
				}
			}

			return offset + delta;
		}
	}
}