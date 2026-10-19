namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// A range of characters in a text (a selection, a search match, a quoted string…).
	/// </summary>
	/// <param name="Offset">Zero-based offset of the first character.</param>
	/// <param name="Length">Number of characters.</param>
	public readonly record struct TextSpan(Int32 Offset, Int32 Length)
	{
		/// <summary>Gets the offset just after the last character.</summary>
		public Int32 End => this.Offset + this.Length;

		/// <summary>Gets a value indicating whether the span contains no characters (a caret position).</summary>
		public Boolean IsEmpty => this.Length == 0;

		/// <summary>Creates a span from its two ends.</summary>
		/// <param name="start">Offset of the first character.</param>
		/// <param name="end">Offset just after the last character.</param>
		/// <returns>The span.</returns>
		public static TextSpan FromBounds(Int32 start, Int32 end)
		{
			return new TextSpan(start, end - start);
		}

		/// <summary>Determines whether an offset lies inside the span (the end offset is excluded).</summary>
		/// <param name="offset">The offset.</param>
		/// <returns><c>true</c> when <paramref name="offset"/> is in <c>[Offset, End)</c>.</returns>
		public Boolean Contains(Int32 offset)
		{
			return offset >= this.Offset && offset < this.End;
		}
	}
}