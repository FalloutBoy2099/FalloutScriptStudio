namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// A single replacement in a text: the characters <c>[Offset, Offset + Length)</c> are replaced by <see cref="NewText"/>.
	/// </summary>
	/// <remarks>
	/// Every service of this library returns its result as a list of edits expressed in the coordinates of the text that
	/// was passed in (the edits of one list never overlap), so an editor can apply them as one undo-able change, for example
	/// from the last to the first edit. <see cref="TextEditApplier"/> applies them to a plain string.
	/// </remarks>
	/// <param name="Offset">Zero-based offset of the first replaced character.</param>
	/// <param name="Length">Number of replaced characters (0 for a pure insertion).</param>
	/// <param name="NewText">The replacement text (empty for a pure deletion).</param>
	public sealed record TextEdit(Int32 Offset, Int32 Length, String NewText)
	{
		/// <summary>Gets the offset just after the replaced characters.</summary>
		public Int32 End => this.Offset + this.Length;

		/// <summary>Gets a value indicating whether the edit only inserts text.</summary>
		public Boolean IsInsertion => this.Length == 0;

		/// <summary>Creates an insertion.</summary>
		/// <param name="offset">Where to insert.</param>
		/// <param name="text">The text to insert.</param>
		/// <returns>The edit.</returns>
		public static TextEdit Insert(Int32 offset, String text)
		{
			return new TextEdit(offset, 0, text);
		}

		/// <summary>Creates a deletion.</summary>
		/// <param name="offset">Offset of the first deleted character.</param>
		/// <param name="length">Number of deleted characters.</param>
		/// <returns>The edit.</returns>
		public static TextEdit Delete(Int32 offset, Int32 length)
		{
			return new TextEdit(offset, length, String.Empty);
		}
	}
}
