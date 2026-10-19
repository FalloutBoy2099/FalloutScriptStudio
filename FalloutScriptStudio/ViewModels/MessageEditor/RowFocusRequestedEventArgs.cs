namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Asks the view to select rows, scroll them into view and optionally start editing a cell.
	/// </summary>
	public sealed class RowFocusRequestedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RowFocusRequestedEventArgs"/> class.
		/// </summary>
		/// <param name="rows">The rows to select; the first one becomes the current row.</param>
		/// <param name="column">The column of the current cell.</param>
		/// <param name="beginEdit">Whether the cell should enter edit mode.</param>
		public RowFocusRequestedEventArgs(IReadOnlyList<MessageRowViewModel> rows, MessageGridColumn column, Boolean beginEdit)
		{
			ArgumentNullException.ThrowIfNull(rows);
			this.Rows = rows;
			this.Column = column;
			this.BeginEdit = beginEdit;
		}

		/// <summary>Gets the rows to select.</summary>
		public IReadOnlyList<MessageRowViewModel> Rows { get; }

		/// <summary>Gets the column of the current cell.</summary>
		public MessageGridColumn Column { get; }

		/// <summary>Gets a value indicating whether the cell should enter edit mode.</summary>
		public Boolean BeginEdit { get; }
	}
}