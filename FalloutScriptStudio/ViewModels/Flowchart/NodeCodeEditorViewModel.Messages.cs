namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>The messages the node code uses: the rows, the message editor hand-over (<see cref="IMessageLineTarget"/>) and saving a message text.</summary>
	public sealed partial class NodeCodeEditorViewModel
	{
		/// <inheritdoc />
		public String? ScriptPath => this._script?.FilePath;

		/// <inheritdoc />
		public Boolean CanAcceptMessageLine => true;

		/// <inheritdoc />
		public void AcceptMessageLine(Int32 messageId, String messageText)
		{
			String number = messageId.ToString(System.Globalization.CultureInfo.InvariantCulture);
			NodeMessageRowViewModel? row = this._sendRow;
			if (row is null)
			{
				this.InsertTextRequested?.Invoke(this, number);
			}
			else
			{
				TextEdit? edit = this._nodeEditor.CreateMessageNumberEdit(this.Code.Text, row.Source.Line, number);
				if (edit is not null)
				{
					DocumentEdits.Apply(this.Code, new[] { edit });
				}
			}

			_ = this.RefreshRowsAsync();
		}

		/// <inheritdoc />
		public void MessagesUpdated(String messageFilePath, MessageDocument document)
		{
			_ = this.RefreshRowsAsync();
		}

		/// <inheritdoc />
		public Boolean TryOpenAsText(String messageFilePath)
		{
			return false;
		}

		/// <summary>Reads the messages the code uses again (after the code or a message file changed).</summary>
		/// <returns>A task that completes when the rows are replaced.</returns>
		public async Task RefreshRowsAsync()
		{
			if (this._script is null)
			{
				return;
			}

			if (this._refreshing)
			{
				this._refreshAgain = true;
				return;
			}

			this._refreshing = true;
			try
			{
				do
				{
					this._refreshAgain = false;
					await this.LoadRowsAsync(this._script);
				}
				while (this._refreshAgain);
			}
			finally
			{
				this._refreshing = false;
			}
		}

		/// <summary>
		/// Gets the range of the code that holds the statement of a message row: from the start of the statement name to the closing
		/// parenthesis.
		/// </summary>
		/// <param name="row">The row.</param>
		/// <returns>The range, or <see langword="null"/> when the statement is not found in the code.</returns>
		public (Int32 Offset, Int32 Length)? GetStatementRange(NodeMessageRowViewModel row)
		{
			ArgumentNullException.ThrowIfNull(row);

			DialogueLine line = row.Source.Line;
			Int32 lineNumber = line.CodeLineIndex + 1;
			if (line.ShortCode is null || lineNumber < 1 || lineNumber > this.Code.LineCount)
			{
				return null;
			}

			DocumentLine documentLine = this.Code.GetLineByNumber(lineNumber);
			String text = this.Code.GetText(documentLine);
			Int32 index = text.IndexOf(line.ShortCode, StringComparison.Ordinal);
			if (index < 0)
			{
				return null;
			}

			Int32 length = line.ShortCode.Length;
			while (index > 0 && Char.IsLetter(text[index - 1]))
			{
				index--;
				length++;
			}

			return (documentLine.Offset + index, length);
		}

		/// <summary>Opens the message editor to pick a message number that is pasted at the caret.</summary>
		[RelayCommand]
		public async Task InsertMessageAsync()
		{
			DocumentViewModel script = this.RequireScript();
			IDialogueMessageSource source = await this._messages.CreateSourceAsync(script);
			String? path = source.GetMessageFilePath(IDialogueMessageSource.OwnFile) ?? script.MessageFilePath;
			if (path is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MissingMessageFile.Title"), Loc.Get("Str.Flowchart.Msg.MissingOwnMessageFile"));
				return;
			}

			this._sendRow = null;
			this.OpenMessageEditor(path, null);
		}

		/// <summary>Opens the message editor at the message of a row; the line the user sends replaces the number in the code.</summary>
		/// <param name="row">The row.</param>
		[RelayCommand]
		public void OpenMessage(NodeMessageRowViewModel? row)
		{
			if (row is null)
			{
				return;
			}

			String? path = row.FilePath;
			if (path is null)
			{
				_ = this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MissingMessageFile.Title"), Loc.Get("Str.Flowchart.Msg.MissingRequiredMessageFile"));
				return;
			}

			this._sendRow = row;
			this.OpenMessageEditor(path, row.MessageNumber);
		}

		private async Task LoadRowsAsync(DocumentViewModel script)
		{
			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return;
			}

			IReadOnlyList<NodeMessageRow> rows;
			try
			{
				IDialogueMessageSource source = await this._messages.CreateSourceAsync(script);
				rows = await this._nodeEditor.GetMessageRowsAsync(this.Code.Text, info, source);
			}
			catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
			{
				return;
			}

			Int32? selected = this.SelectedRow?.MessageNumber;
			this.Rows.Clear();
			foreach (NodeMessageRow row in rows)
			{
				this.Rows.Add(new NodeMessageRowViewModel(row, this.OnRowEdited));
			}

			if (selected is Int32 number)
			{
				this.SelectedRow = this.Rows.FirstOrDefault(r => r.MessageNumber == number);
			}
		}

		private void OnRowEdited(NodeMessageRowViewModel row)
		{
			_ = this.SaveRowTextAsync(row);
		}

		private async Task SaveRowTextAsync(NodeMessageRowViewModel row)
		{
			if (MessageTextEditor.ContainsBraces(row.Text))
			{
				row.Revert();
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MessageFile.Title"), Loc.Get("Str.MessageEditor.Msg.BracesInText"));
				return;
			}

			if (row.FilePath is null)
			{
				return;
			}

			if (await this._messages.UpdateMessageTextAsync(row.FilePath, row.MessageNumber, row.Text))
			{
				row.MarkSaved();
			}
			else
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MessageFile.Title"), Loc.Format("Str.Flowchart.Msg.MessageSaveFailed", row.FilePath));
			}
		}

		private void OpenMessageEditor(String? path, Int32? messageId)
		{
			this._messageEditor.Open(new MessageEditorOpenOptions
			{
				FilePath = path,
				Target = this,
				SelectMessageId = messageId,
				CloseOnSend = true,
				Modal = true,
				Owner = this.Owner,
			});
			this._sendRow = null;
			_ = this.RefreshRowsAsync();
		}
	}
}
