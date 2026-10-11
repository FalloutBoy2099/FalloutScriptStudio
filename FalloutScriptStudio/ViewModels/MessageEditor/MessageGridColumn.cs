namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	/// <summary>
	/// The editable columns of the message grid, in the order they are searched.
	/// </summary>
	public enum MessageGridColumn
	{
		/// <summary>The message number.</summary>
		Line = 0,

		/// <summary>The player line marker.</summary>
		Player,

		/// <summary>The message text (or the comment line).</summary>
		Text,

		/// <summary>The trailing <c># comment</c> of a message.</summary>
		Comment,

		/// <summary>The sound (lip-sync) file name.</summary>
		Sound,
	}
}
