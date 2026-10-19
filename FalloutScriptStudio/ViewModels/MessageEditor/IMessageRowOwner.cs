namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;

	/// <summary>
	/// What a row needs from the editor that owns it: undo bookkeeping and rejection messages.
	/// </summary>
	internal interface IMessageRowOwner
	{
		/// <summary>Called right before a row changes, so the editor can remember the previous state for undo.</summary>
		/// <param name="row">The row that is about to change.</param>
		void RowEditing(MessageRowViewModel row);

		/// <summary>Called after a row changed.</summary>
		/// <param name="row">The row that changed.</param>
		void RowEdited(MessageRowViewModel row);

		/// <summary>Called when an edit was refused because the value is not valid.</summary>
		/// <param name="message">The explanation for the user.</param>
		void RowRejected(String message);
	}
}