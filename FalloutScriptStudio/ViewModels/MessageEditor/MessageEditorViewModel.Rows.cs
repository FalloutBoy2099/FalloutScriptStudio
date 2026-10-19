namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;

	public sealed partial class MessageEditorViewModel
	{
		/// <summary>
		/// Starts editing the current cell, or, when the cell is empty, adds the next numbered message (the legacy Enter key behaviour).
		/// </summary>
		public void ActivateCurrentCell()
		{
			MessageRowViewModel? row = this.SelectedRow;
			if (row is null)
			{
				return;
			}

			MessageGridColumn column = this.CurrentColumn;
			if (row.GetCellText(column).Length > 0 || !this.TryAddNextLine())
			{
				this.RequestFocus(row, column, true);
			}
		}

		/// <summary>
		/// Adds a message after the current row. Its number follows the preceding message; after a comment it is rounded
		/// to tens and the comment step is added.
		/// </summary>
		/// <returns><see langword="false"/> if the calculated number is already used (nothing is added).</returns>
		public Boolean TryAddNextLine()
		{
			Boolean wasEditing = this.IsCellEditing;
			MessageGridColumn column = this.CurrentColumn == MessageGridColumn.Player ? MessageGridColumn.Text : this.CurrentColumn;
			this.EndEdit();

			Int32 selected = this.GetCurrentIndex();
			if (!this.BuildLiveDocument().TryGetNextId(selected, this._commentStep, out Int32 id))
			{
				this._platform.Beep(MessageEditorBeep.Refused);
				this.StatusMessage = Loc.Format("Str.MessageEditor.Status.NumberUsed", id);
				return false;
			}

			// An empty line is replaced in front of, any other row is followed by the new message.
			Int32 position = this._rows[selected].LineText.Length > 0 ? selected + 1 : selected;
			MessageRowViewModel row = this.CreateRow(MessageEntry.CreateMessage(id));
			this.Mutate(() => this._rows.Insert(position, row));
			this.RequestFocus(row, column, wasEditing);
			this.StatusMessage = Loc.Format("Str.MessageEditor.Status.Added", id);
			return true;
		}

		/// <summary>Adds the next numbered message below the current row.</summary>
		[RelayCommand]
		private void AddNextLine()
		{
			this.TryAddNextLine();
		}

		/// <summary>Inserts an empty line below the current row and selects it.</summary>
		[RelayCommand]
		private void InsertBlankBelow()
		{
			this.InsertBlank(false);
		}

		/// <summary>Inserts an empty line above the current row; the current row stays selected.</summary>
		[RelayCommand]
		private void InsertBlankAbove()
		{
			this.InsertBlank(true);
		}

		/// <summary>Inserts a comment line below the current row, or turns the current empty line into a comment.</summary>
		[RelayCommand]
		private void InsertComment()
		{
			if (this.IsCellEditing)
			{
				return;
			}

			this.EndEdit();
			Int32 selected = this.GetCurrentIndex();
			MessageRowViewModel current = this._rows[selected];
			String text = current.IsBlank ? MessageFileTexts.CommentPrefix + current.Entry.Text : MessageFileTexts.CommentPrefix;
			MessageRowViewModel comment = this.CreateRow(MessageEntry.CreateComment(text));
			if (current.IsBlank)
			{
				this.Mutate(() => this._rows[selected] = comment);
			}
			else
			{
				this.Mutate(() => this._rows.Insert(selected + 1, comment));
			}

			this.RequestFocus(comment, MessageGridColumn.Text, true);
		}

		/// <summary>Deletes the selected rows.</summary>
		[RelayCommand]
		private void DeleteRows()
		{
			this.EndEdit();
			List<Int32> indexes = this.GetSelectedIndexes();
			if (indexes.Count == 0 || (this._rows.Count == 1 && this._rows[0].IsBlank))
			{
				return;
			}

			this.Mutate(() =>
			{
				for (Int32 i = indexes.Count - 1; i >= 0; i--)
				{
					this._rows.RemoveAt(indexes[i]);
				}

				if (this._rows.Count == 0)
				{
					this._rows.Add(this.CreateBlankRow());
				}
			});
			this.RequestFocus(this._rows[Math.Min(indexes[0], this._rows.Count - 1)], this.CurrentColumn, false);
			this.StatusMessage = indexes.Count == 1 ? Loc.Get("Str.MessageEditor.Status.DeletedOne") : Loc.Format("Str.MessageEditor.Status.DeletedMany", indexes.Count);
		}

		/// <summary>Copies the selected rows below them; copies of messages get the next free numbers.</summary>
		[RelayCommand(CanExecute = nameof(HasSelection))]
		private void DuplicateRows()
		{
			this.EndEdit();
			List<Int32> indexes = this.GetSelectedIndexes();
			if (indexes.Count == 0)
			{
				return;
			}

			HashSet<Int32> used = this._rows.Where(row => row.IsMessage).Select(row => row.Entry.Id).ToHashSet();
			List<MessageRowViewModel> copies = [];
			foreach (Int32 index in indexes)
			{
				MessageEntry entry = MessageRowViewModel.CloneEntry(this._rows[index].Entry);
				if (entry.Kind == MessageLineKind.Message)
				{
					Int32 id = entry.Id;
					do
					{
						id++;
					}
					while (used.Contains(id));
					used.Add(id);
					entry.Id = id;
				}

				copies.Add(this.CreateRow(entry));
			}

			Int32 insertAt = indexes[^1] + 1;
			this.Mutate(() =>
			{
				for (Int32 i = 0; i < copies.Count; i++)
				{
					this._rows.Insert(insertAt + i, copies[i]);
				}
			});
			this.RequestFocus(copies, this.CurrentColumn, false);
		}

		/// <summary>Moves the selected rows one position up.</summary>
		[RelayCommand]
		private void MoveUp()
		{
			this.MoveSelectedRows(-1);
		}

		/// <summary>Moves the selected rows one position down.</summary>
		[RelayCommand]
		private void MoveDown()
		{
			this.MoveSelectedRows(1);
		}

		/// <summary>Marks the selected messages as player lines, or removes the mark when the first one already is.</summary>
		[RelayCommand]
		private void TogglePlayerLine()
		{
			this.EndEdit();
			List<MessageRowViewModel> messages = this.GetSelectedOrCurrentRows().Where(row => row.IsMessage).ToList();
			if (messages.Count == 0)
			{
				return;
			}

			Boolean value = !messages[0].IsPlayerLine;
			this.Mutate(() =>
			{
				foreach (MessageRowViewModel row in messages)
				{
					row.ApplyPlayerLine(value);
				}
			});
		}

		/// <summary>Asks for the comment that follows the message and stores it.</summary>
		[RelayCommand]
		private async Task EditCommentAsync()
		{
			this.EndEdit();
			MessageRowViewModel? row = this.SelectedRow;
			if (row is null)
			{
				return;
			}

			if (!row.IsMessage)
			{
				this.NotificationMessage = Loc.Get("Str.MessageEditor.Msg.OnlyMessagesComment");
				return;
			}

			String? text = await this._dialogs.PromptAsync(Loc.Get("Str.MessageEditor.Menu.EditComment"), Loc.Get("Str.MessageEditor.Msg.CommentPrompt"), row.Comment);
			if (text is not null)
			{
				row.Comment = text;
			}
		}

		/// <summary>Sends the number of the current message to the owner (the script editor).</summary>
		[RelayCommand(CanExecute = nameof(CanSendLine))]
		private void SendLine()
		{
			this.EndEdit();
			MessageRowViewModel? row = this.SelectedRow;
			if (this.Target is null || !this.Target.CanAcceptMessageLine || row is null || !row.IsMessage)
			{
				return;
			}

			this.Target.AcceptMessageLine(row.Entry.Id, row.Entry.Text);
			this.StatusMessage = Loc.Format("Str.MessageEditor.Status.Sent", row.Entry.Id);
			if (this.CloseOnSend)
			{
				this.CloseRequested?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Selects a row and sends its message to the owner (the middle mouse button).
		/// </summary>
		/// <param name="row">The row.</param>
		public void SendRow(MessageRowViewModel row)
		{
			ArgumentNullException.ThrowIfNull(row);
			this.RequestFocus(row, this.CurrentColumn, false);
			this.SelectedRow = row;
			this._selectedRows = new[] { row };
			this.SendLine();
		}

		/// <summary>Takes back the last change.</summary>
		[RelayCommand(CanExecute = nameof(CanUndo))]
		private void Undo()
		{
			this.EndEdit();
			if (!this._history.CanUndo)
			{
				return;
			}

			this.RestoreSnapshot(this._history.Undo(this.CaptureSnapshot()));
		}

		/// <summary>Repeats the change that was taken back.</summary>
		[RelayCommand(CanExecute = nameof(CanRedo))]
		private void Redo()
		{
			this.EndEdit();
			if (!this._history.CanRedo)
			{
				return;
			}

			this.RestoreSnapshot(this._history.Redo(this.CaptureSnapshot()));
		}

		private Boolean CanUndo()
		{
			return this._history.CanUndo;
		}

		private Boolean CanRedo()
		{
			return this._history.CanRedo;
		}

		private Boolean HasSelection()
		{
			return this._selectedRows.Count > 0;
		}

		private Boolean CanSendLine()
		{
			return this.Target is { CanAcceptMessageLine: true } && this.SelectedRow is { IsMessage: true };
		}

		private Int32 GetCurrentIndex()
		{
			Int32 index = this.SelectedRow is null ? -1 : this._rows.IndexOf(this.SelectedRow);
			return index >= 0 ? index : Math.Max(0, this._rows.Count - 1);
		}

		private List<MessageRowViewModel> GetSelectedOrCurrentRows()
		{
			if (this._selectedRows.Count > 0)
			{
				return this._selectedRows.ToList();
			}

			return this.SelectedRow is null ? [] : [this.SelectedRow];
		}

		/// <summary>
		/// Gets the indexes of the selected rows (or of the current row) in ascending order.
		/// </summary>
		private List<Int32> GetSelectedIndexes()
		{
			return this.GetSelectedOrCurrentRows()
				.Select(row => this._rows.IndexOf(row))
				.Where(index => index >= 0)
				.Distinct()
				.OrderBy(index => index)
				.ToList();
		}

		private void InsertBlank(Boolean above)
		{
			this.EndEdit();
			Int32 selected = this.GetCurrentIndex();
			MessageRowViewModel current = this._rows[selected];
			MessageRowViewModel blank = this.CreateRow(MessageEntry.CreateBlank());
			this.Mutate(() => this._rows.Insert(above ? selected : selected + 1, blank));
			this.RequestFocus(above ? current : blank, this.CurrentColumn, false);
		}

		private void MoveSelectedRows(Int32 direction)
		{
			this.EndEdit();
			if (this.IsFilterActive)
			{
				this.NotificationMessage = Loc.Get("Str.MessageEditor.Msg.MoveFiltered");
				return;
			}

			List<Int32> indexes = this.GetSelectedIndexes();
			if (indexes.Count == 0)
			{
				return;
			}

			List<MessageRowViewModel> moving = indexes.Select(index => this._rows[index]).ToList();
			HashSet<Int32> positions = new(indexes);
			List<(Int32 From, Int32 To)> moves = [];

			IEnumerable<Int32> order = direction < 0 ? indexes : indexes.AsEnumerable().Reverse();
			foreach (Int32 index in order)
			{
				Int32 target = index + direction;
				if (target < 0 || target >= this._rows.Count || positions.Contains(target))
				{
					continue;
				}

				positions.Remove(index);
				positions.Add(target);
				moves.Add((index, target));
			}

			if (moves.Count == 0)
			{
				return;
			}

			this.Mutate(() =>
			{
				foreach ((Int32 from, Int32 to) in moves)
				{
					this._rows.Move(from, to);
				}
			});
			this.RequestFocus(moving, this.CurrentColumn, false);
		}
	}
}