namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// Applies a sequence of edits, each expressed in the coordinates of the text as it is <i>at that moment</i>, and reports
	/// the whole sequence as a list of edits in the coordinates of the <i>original</i> text.
	/// </summary>
	/// <remarks>
	/// This lets multi-step algorithms (delete a procedure, then insert its declaration elsewhere, then paste its body) work
	/// on the evolving text and still hand a single non-overlapping edit list to the editor. It is a small piece table.
	/// </remarks>
	public sealed class TextEditBuilder
	{
		private readonly String _original;
		private readonly List<Piece> _pieces = new List<Piece>();
		private Int32 _length;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextEditBuilder"/> class.
		/// </summary>
		/// <param name="original">The original text.</param>
		public TextEditBuilder(String original)
		{
			ArgumentNullException.ThrowIfNull(original);
			this._original = original;
			this._length = original.Length;
			if (original.Length > 0)
			{
				this._pieces.Add(new Piece(0, original.Length, null));
			}
		}

		/// <summary>Gets the original text.</summary>
		public String OriginalText => this._original;

		/// <summary>Gets the length of the current text.</summary>
		public Int32 Length => this._length;

		/// <summary>Gets the current text (the original text with all edits applied so far).</summary>
		public String CurrentText
		{
			get
			{
				var result = new StringBuilder(this._length);
				foreach (Piece piece in this._pieces)
				{
					result.Append(piece.Text ?? this._original.Substring(piece.Start, piece.Length));
				}

				return result.ToString();
			}
		}

		/// <summary>Inserts text into the current text.</summary>
		/// <param name="offset">Offset in the current text.</param>
		/// <param name="text">The text to insert.</param>
		public void Insert(Int32 offset, String text)
		{
			this.Replace(offset, 0, text);
		}

		/// <summary>Removes text from the current text.</summary>
		/// <param name="offset">Offset in the current text.</param>
		/// <param name="length">Number of characters to remove.</param>
		public void Remove(Int32 offset, Int32 length)
		{
			this.Replace(offset, length, String.Empty);
		}

		/// <summary>Replaces text in the current text.</summary>
		/// <param name="offset">Offset in the current text.</param>
		/// <param name="length">Number of characters to replace.</param>
		/// <param name="text">The new text.</param>
		/// <exception cref="ArgumentOutOfRangeException">The range lies outside the current text.</exception>
		public void Replace(Int32 offset, Int32 length, String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			if (offset < 0 || length < 0 || offset + length > this._length)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), "The range lies outside the text.");
			}

			Int32 index = this.SplitAt(offset);
			if (length > 0)
			{
				Int32 endIndex = this.SplitAt(offset + length);
				this._pieces.RemoveRange(index, endIndex - index);
				this._length -= length;
			}

			if (text.Length > 0)
			{
				this._pieces.Insert(index, new Piece(0, text.Length, text));
				this._length += text.Length;
			}
		}

		/// <summary>
		/// Converts everything done so far to edits in the coordinates of the original text.
		/// </summary>
		/// <returns>The ordered, non-overlapping edits.</returns>
		public IReadOnlyList<TextEdit> ToEdits()
		{
			var edits = new List<TextEdit>();
			var added = new StringBuilder();
			Int32 position = 0;
			Int32 groupOffset = 0;
			Int32 removed = 0;

			foreach (Piece piece in this._pieces)
			{
				if (piece.Text is not null)
				{
					added.Append(piece.Text);
					continue;
				}

				removed += piece.Start - position;
				if (removed > 0 || added.Length > 0)
				{
					edits.Add(new TextEdit(groupOffset, removed, added.ToString()));
				}

				position = piece.Start + piece.Length;
				groupOffset = position;
				removed = 0;
				added.Clear();
			}

			removed += this._original.Length - position;
			if (removed > 0 || added.Length > 0)
			{
				edits.Add(new TextEdit(groupOffset, removed, added.ToString()));
			}

			return edits;
		}

		/// <summary>Makes sure a piece boundary exists at the offset and returns the index of the first piece that starts there.</summary>
		private Int32 SplitAt(Int32 offset)
		{
			Int32 position = 0;
			for (Int32 i = 0; i < this._pieces.Count; i++)
			{
				Piece piece = this._pieces[i];
				if (offset == position)
				{
					return i;
				}

				if (offset < position + piece.Length)
				{
					Int32 inner = offset - position;
					Piece left;
					Piece right;
					if (piece.Text is null)
					{
						left = new Piece(piece.Start, inner, null);
						right = new Piece(piece.Start + inner, piece.Length - inner, null);
					}
					else
					{
						left = new Piece(0, inner, piece.Text.Substring(0, inner));
						right = new Piece(0, piece.Length - inner, piece.Text.Substring(inner));
					}

					this._pieces[i] = left;
					this._pieces.Insert(i + 1, right);
					return i + 1;
				}

				position += piece.Length;
			}

			return this._pieces.Count;
		}

		private readonly record struct Piece(Int32 Start, Int32 Length, String? Text);
	}
}
