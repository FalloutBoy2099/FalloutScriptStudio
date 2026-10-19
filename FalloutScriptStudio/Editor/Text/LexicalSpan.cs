namespace FalloutScriptStudio.Editor.Text
{
	using System;

	/// <summary>A non-code region of the text found by <see cref="LexicalMap"/>.</summary>
	/// <param name="Start">Offset of the first character.</param>
	/// <param name="Length">Length in characters.</param>
	/// <param name="Kind">The category of the region.</param>
	/// <param name="IsClosed">True when the region ended with its closing token (not at the end of the line / file).</param>
	public readonly record struct LexicalSpan(Int32 Start, Int32 Length, LexicalKind Kind, Boolean IsClosed)
	{
		/// <summary>Gets the offset just after the last character.</summary>
		public Int32 End => this.Start + this.Length;
	}
}