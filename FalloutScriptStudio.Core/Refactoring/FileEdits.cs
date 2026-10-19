namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using TextTools;

	/// <summary>
	/// The edits of one file produced by a refactoring.
	/// </summary>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Edits">The edits, in the coordinates of the text that was passed in; they never overlap.</param>
	/// <param name="IsOpen">Whether the file was passed as an open editor document.</param>
	public sealed record FileEdits(String Path, IReadOnlyList<TextEdit> Edits, Boolean IsOpen)
	{
		/// <summary>Applies the edits to the text of the file.</summary>
		/// <param name="originalText">The text the edits were computed for.</param>
		/// <returns>The new text.</returns>
		public String ApplyTo(String originalText)
		{
			return TextEditApplier.Apply(originalText, this.Edits);
		}
	}
}