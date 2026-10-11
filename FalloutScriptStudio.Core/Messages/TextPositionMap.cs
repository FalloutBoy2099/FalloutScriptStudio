namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Converts text offsets to line/column positions. Lines end with CRLF, CR or LF.
	/// </summary>
	internal sealed class TextPositionMap
	{
		private readonly List<Int32> _lineStarts = new List<Int32> { 0 };
		private readonly Int32 _length;

		public TextPositionMap(String text)
		{
			this._length = text.Length;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '\r')
				{
					if (i + 1 < text.Length && text[i + 1] == '\n')
					{
						i++;
					}

					this._lineStarts.Add(i + 1);
				}
				else if (text[i] == '\n')
				{
					this._lineStarts.Add(i + 1);
				}
			}
		}

		/// <summary>Converts an offset to a zero-based line and column.</summary>
		public (Int32 Line, Int32 Column) GetPosition(Int32 offset)
		{
			offset = Math.Clamp(offset, 0, this._length);
			Int32 line = this._lineStarts.BinarySearch(offset);
			if (line < 0)
			{
				line = ~line - 1;
			}

			return (line, offset - this._lineStarts[line]);
		}

		/// <summary>Gets the offset of the first character of a zero-based line.</summary>
		public Int32 GetLineStart(Int32 line)
		{
			return this._lineStarts[line];
		}

		/// <summary>Gets the length of a zero-based line without its line break.</summary>
		public Int32 GetLineLength(Int32 line, String text)
		{
			Int32 start = this._lineStarts[line];
			Int32 end = line + 1 < this._lineStarts.Count ? this._lineStarts[line + 1] : this._length;
			while (end > start && (text[end - 1] == '\r' || text[end - 1] == '\n'))
			{
				end--;
			}

			return end - start;
		}
	}
}
