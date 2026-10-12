namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The undo history: snapshots of the rows, change tracking and the revision counters.</summary>
	public sealed partial class MessageEditorViewModel
	{
		/// <summary>The most undo steps that are kept.</summary>
		private const Int32 UndoLimit = 200;

		/// <summary>The budget of remembered rows over all undo steps; big files get fewer steps.</summary>
		private const Int32 UndoRowBudget = 250000;

		private EditorSnapshot CaptureSnapshot()
		{
			List<RowState> states = new List<RowState>(this._rows.Count);
			foreach (MessageRowViewModel row in this._rows)
			{
				states.Add(row.CaptureState());
			}

			Int32 selected = this.SelectedRow is null ? -1 : this._rows.IndexOf(this.SelectedRow);
			return new EditorSnapshot(states, selected, this._revision);
		}

		private void RecordUndo()
		{
			Int32 limit = Math.Clamp(UndoRowBudget / Math.Max(1, this._rows.Count), 20, UndoLimit);
			this._history.Record(this.CaptureSnapshot(), limit);
			this.UndoCommand.NotifyCanExecuteChanged();
			this.RedoCommand.NotifyCanExecuteChanged();
		}

		/// <summary>
		/// Runs a change of the rows as one undoable step.
		/// </summary>
		/// <param name="change">The change.</param>
		private void Mutate(Action change)
		{
			this.EndEdit();
			this.RecordUndo();
			this._suspendUndo++;
			try
			{
				change();
			}
			finally
			{
				this._suspendUndo--;
			}

			this.NotifyDocumentChanged();
			this.NotificationMessage = String.Empty;
		}

		private void EndEdit()
		{
			this.EditCommitRequested?.Invoke(this, EventArgs.Empty);
		}

		private void NotifyDocumentChanged()
		{
			this._revision = ++this._revisionCounter;
			this.IsModified = this._revision != this._savedRevision;
			this.AfterRowsChanged();
		}

		private void AfterRowsChanged()
		{
			this.ScheduleStructureCheck();
			this.UpdateSearchMatches();
			this.OnPropertyChanged(nameof(this.MessageCount));
			this.OnPropertyChanged(nameof(this.MessageCountCaption));
			this.UndoCommand.NotifyCanExecuteChanged();
			this.RedoCommand.NotifyCanExecuteChanged();
		}

		private void RestoreSnapshot(EditorSnapshot snapshot)
		{
			List<MessageRowViewModel> rows = snapshot.Rows.Select(state => MessageRowViewModel.FromState(state, this)).ToList();
			this._rows.ReplaceAll(rows);
			this._revision = snapshot.Revision;
			this.IsModified = this._revision != this._savedRevision;
			this.AfterRowsChanged();
			if (rows.Count > 0)
			{
				this.RequestFocus(rows[Math.Clamp(snapshot.SelectedIndex, 0, rows.Count - 1)], this.CurrentColumn, false);
			}
		}
	}
}
