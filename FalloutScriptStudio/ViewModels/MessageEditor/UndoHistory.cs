namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// An undo / redo stack of whole states; it forgets the oldest states when it grows beyond a limit.
	/// </summary>
	/// <typeparam name="T">The state type.</typeparam>
	internal sealed class UndoHistory<T>
	{
		private readonly LinkedList<T> _undo = new();
		private readonly Stack<T> _redo = new();

		public Boolean CanUndo => this._undo.Count > 0;

		public Boolean CanRedo => this._redo.Count > 0;

		/// <summary>Remembers the state before a change; this discards everything that could have been redone.</summary>
		/// <param name="state">The state before the change.</param>
		/// <param name="limit">The number of states to keep.</param>
		public void Record(T state, Int32 limit)
		{
			this._redo.Clear();
			this._undo.AddLast(state);
			while (this._undo.Count > limit)
			{
				this._undo.RemoveFirst();
			}
		}

		/// <summary>Takes back the last change.</summary>
		/// <param name="current">The state that is replaced; it becomes redoable.</param>
		/// <returns>The previous state.</returns>
		public T Undo(T current)
		{
			T previous = this._undo.Last!.Value;
			this._undo.RemoveLast();
			this._redo.Push(current);
			return previous;
		}

		/// <summary>Repeats the last undone change.</summary>
		/// <param name="current">The state that is replaced; it becomes undoable.</param>
		/// <returns>The next state.</returns>
		public T Redo(T current)
		{
			T next = this._redo.Pop();
			this._undo.AddLast(current);
			return next;
		}

		public void Clear()
		{
			this._undo.Clear();
			this._redo.Clear();
		}
	}
}