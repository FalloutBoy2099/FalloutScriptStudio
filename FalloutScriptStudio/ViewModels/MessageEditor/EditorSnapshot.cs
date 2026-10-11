namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The complete state of the grid at one moment; used by undo and redo.
	/// </summary>
	/// <param name="Rows">The rows.</param>
	/// <param name="SelectedIndex">The index of the current row, or -1.</param>
	/// <param name="Revision">The document revision this state represents.</param>
	internal sealed record EditorSnapshot(IReadOnlyList<RowState> Rows, Int32 SelectedIndex, Int32 Revision);
}
