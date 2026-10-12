namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using FalloutScriptStudio.Core.Messages;

	/// <summary>
	/// A copy of one row that is independent of later edits; used by undo and redo.
	/// </summary>
	/// <param name="Entry">A private copy of the entry.</param>
	/// <param name="LineChanged">Whether the line number is marked as changed since the last save.</param>
	/// <param name="TextChanged">Whether the text is marked as changed since the last save.</param>
	/// <param name="CommentChanged">Whether the comment is marked as changed since the last save.</param>
	/// <param name="SoundChanged">Whether the sound name is marked as changed since the last save.</param>
	internal sealed record RowState(MessageEntry Entry, Boolean LineChanged, Boolean TextChanged, Boolean CommentChanged, Boolean SoundChanged)
	{
		/// <summary>
		/// Determines whether the state still describes a row, so that a snapshot can share it instead of copying the row again.
		/// </summary>
		/// <param name="entry">The current entry of the row.</param>
		/// <param name="lineChanged">The current changed flag of the line number.</param>
		/// <param name="textChanged">The current changed flag of the text.</param>
		/// <param name="commentChanged">The current changed flag of the comment.</param>
		/// <param name="soundChanged">The current changed flag of the sound name.</param>
		/// <returns><see langword="true"/> when nothing differs.</returns>
		internal Boolean Describes(MessageEntry entry, Boolean lineChanged, Boolean textChanged, Boolean commentChanged, Boolean soundChanged)
		{
			MessageEntry saved = this.Entry;
			return this.LineChanged == lineChanged
				&& this.TextChanged == textChanged
				&& this.CommentChanged == commentChanged
				&& this.SoundChanged == soundChanged
				&& saved.Kind == entry.Kind
				&& saved.Id == entry.Id
				&& saved.IsPlayerLine == entry.IsPlayerLine
				&& saved.IndentTabs == entry.IndentTabs
				&& String.Equals(saved.Text, entry.Text, StringComparison.Ordinal)
				&& String.Equals(saved.SoundName, entry.SoundName, StringComparison.Ordinal)
				&& String.Equals(saved.Description, entry.Description, StringComparison.Ordinal);
		}
	}
}
