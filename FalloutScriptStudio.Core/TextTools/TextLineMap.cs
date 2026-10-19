namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>
	/// An index of the lines of a text (line terminators: <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c>).
	/// Line numbers are 1-based, offsets are zero-based; a text always has at least one line, and a text that ends with a
	/// terminator has a last empty line.
	/// </summary>
	public sealed class TextLineMap
	{
		/// <summary>The Windows line terminator, used when a text has none to copy.</summary>
		public const String DefaultNewLine = "\r\n";

		private static readonly SearchValues<Char> _lineBreakChars = SearchValues.Create("\r\n");

		private readonly List<Int32> _starts = [];
		private readonly List<Int32> _lengths = [];
		private readonly List<Int32> _terminators = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="TextLineMap"/> class.
		/// </summary>
		/// <param name="text">The text to index.</param>
		public TextLineMap(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			this.Text = text;

			Int32 start = 0;
			Int32 i = 0;
			while (i < text.Length)
			{
				Char c = text[i];
				if (c == '\n' || c == '\r')
				{
					Int32 terminator = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
					this._starts.Add(start);
					this._lengths.Add(i - start);
					this._terminators.Add(terminator);
					i += terminator;
					start = i;
				}
				else
				{
					i++;
				}
			}

			this._starts.Add(start);
			this._lengths.Add(text.Length - start);
			this._terminators.Add(0);
		}

		/// <summary>Gets the indexed text.</summary>
		public String Text { get; }

		/// <summary>Gets the number of lines (at least 1).</summary>
		public Int32 LineCount => this._starts.Count;

		/// <summary>
		/// Detects the line terminator used by a text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns><c>\r\n</c> when the first terminator is a CRLF pair or the text has none, otherwise <c>\n</c> (or <c>\r</c>).</returns>
		public static String DetectNewLine(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 index = text.AsSpan().IndexOfAny(_lineBreakChars);
			if (index < 0)
			{
				return DefaultNewLine;
			}

			if (text[index] == '\n')
			{
				return "\n";
			}

			return index + 1 < text.Length && text[index + 1] == '\n' ? DefaultNewLine : "\r";
		}

		/// <summary>Gets the offset of the first character of a line.</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The offset.</returns>
		public Int32 GetLineStart(Int32 line)
		{
			return this._starts[this.Index(line)];
		}

		/// <summary>Gets the number of characters of a line, without its terminator.</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The length.</returns>
		public Int32 GetLineLength(Int32 line)
		{
			return this._lengths[this.Index(line)];
		}

		/// <summary>Gets the offset just after the last character of a line (where its terminator starts).</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The offset.</returns>
		public Int32 GetLineEnd(Int32 line)
		{
			Int32 index = this.Index(line);
			return this._starts[index] + this._lengths[index];
		}

		/// <summary>Gets the length of the terminator of a line (0 for the last line).</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The length: 0, 1 or 2.</returns>
		public Int32 GetTerminatorLength(Int32 line)
		{
			return this._terminators[this.Index(line)];
		}

		/// <summary>Gets the offset at which the next line starts (the end of the text for the last line).</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The offset.</returns>
		public Int32 GetNextLineStart(Int32 line)
		{
			Int32 index = this.Index(line);
			return this._starts[index] + this._lengths[index] + this._terminators[index];
		}

		/// <summary>Gets the text of a line, without its terminator.</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns>The text.</returns>
		public String GetLineText(Int32 line)
		{
			Int32 index = this.Index(line);
			return this.Text.Substring(this._starts[index], this._lengths[index]);
		}

		/// <summary>Gets the text of a range of lines including the terminators between them but not the last one's.</summary>
		/// <param name="firstLine">1-based first line.</param>
		/// <param name="lastLine">1-based last line.</param>
		/// <returns>The text; empty when the range is empty.</returns>
		public String GetLinesText(Int32 firstLine, Int32 lastLine)
		{
			if (lastLine < firstLine)
			{
				return String.Empty;
			}

			Int32 start = this.GetLineStart(firstLine);
			return this.Text.Substring(start, this.GetLineEnd(lastLine) - start);
		}

		/// <summary>Determines whether a line is empty or contains only white space.</summary>
		/// <param name="line">1-based line number.</param>
		/// <returns><c>true</c> when the line is blank.</returns>
		public Boolean IsBlank(Int32 line)
		{
			Int32 index = this.Index(line);
			Int32 start = this._starts[index];
			for (Int32 i = 0; i < this._lengths[index]; i++)
			{
				if (!Char.IsWhiteSpace(this.Text[start + i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Gets the line that contains an offset.</summary>
		/// <param name="offset">The offset; clamped to the text. An offset inside a terminator belongs to the line it ends.</param>
		/// <returns>The 1-based line number.</returns>
		public Int32 GetLineFromOffset(Int32 offset)
		{
			Int32 clamped = Math.Clamp(offset, 0, this.Text.Length);
			Int32 low = 0;
			Int32 high = this._starts.Count - 1;
			while (low < high)
			{
				Int32 middle = (low + high + 1) / 2;
				if (this._starts[middle] <= clamped)
				{
					low = middle;
				}
				else
				{
					high = middle - 1;
				}
			}

			return low + 1;
		}

		/// <summary>Gets the zero-based column of an offset.</summary>
		/// <param name="offset">The offset; clamped to the text.</param>
		/// <returns>The number of characters between the start of the line and the offset.</returns>
		public Int32 GetColumnFromOffset(Int32 offset)
		{
			Int32 clamped = Math.Clamp(offset, 0, this.Text.Length);
			return clamped - this.GetLineStart(this.GetLineFromOffset(clamped));
		}

		/// <summary>
		/// Gets the lines touched by a selection (the line of the caret for an empty selection).
		/// </summary>
		/// <param name="selection">The selection.</param>
		/// <returns>
		/// The 1-based inclusive range. When a multi-line selection ends exactly at the start of a line, that last line is
		/// not included (the way most editors behave when whole lines are selected).
		/// </returns>
		public LineRange GetSelectedLines(TextSpan selection)
		{
			Int32 first = this.GetLineFromOffset(selection.Offset);
			if (selection.IsEmpty)
			{
				return new LineRange(first, first);
			}

			Int32 last = this.GetLineFromOffset(selection.End);
			if (last > first && selection.End == this.GetLineStart(last))
			{
				last--;
			}

			return new LineRange(first, last);
		}

		private Int32 Index(Int32 line)
		{
			if (line < 1 || line > this._starts.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(line), line, "The line number must be between 1 and " + this._starts.Count + ".");
			}

			return line - 1;
		}
	}
}