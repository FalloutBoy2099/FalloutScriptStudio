namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Result of a replace operation.
	/// </summary>
	/// <param name="NewText">The text after the replacement.</param>
	/// <param name="Edits">The replacements, in the coordinates of the original text.</param>
	public sealed record ReplaceResult(String NewText, IReadOnlyList<TextEdit> Edits)
	{
		/// <summary>Gets the number of replacements.</summary>
		public Int32 Count => this.Edits.Count;
	}
}