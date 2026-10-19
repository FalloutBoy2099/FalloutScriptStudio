namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// A free text note on the flowchart canvas (the <c>&lt;Note&gt;</c> element of a <c>.fcd</c> file).
	/// </summary>
	/// <param name="Text">The text of the note (empty when the note was never edited).</param>
	public sealed record FlowchartNoteState(String Text)
	{
		/// <summary>Gets the horizontal position on the canvas (canvas units).</summary>
		public Single X { get; init; }

		/// <summary>Gets the vertical position on the canvas.</summary>
		public Single Y { get; init; }

		/// <summary>Gets the width of the note.</summary>
		public Single Width { get; init; } = 200;

		/// <summary>Gets the height of the note.</summary>
		public Single Height { get; init; } = 50;
	}
}