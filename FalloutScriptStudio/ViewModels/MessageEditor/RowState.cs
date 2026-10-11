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
	internal sealed record RowState(MessageEntry Entry, Boolean LineChanged, Boolean TextChanged, Boolean CommentChanged, Boolean SoundChanged);
}
