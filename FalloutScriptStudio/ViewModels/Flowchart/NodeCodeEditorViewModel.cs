namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Threading;
	using CommunityToolkit.Mvvm.ComponentModel;
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
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// The view model of the node code editor window (legacy <c>FlowchartTE</c>): edits the code of one dialogue node apart from
	/// the script, lists the messages the code uses, and applies the code to the script when the user is done.
	/// </summary>
	public sealed partial class NodeCodeEditorViewModel : ToolWindowViewModelBase, IMessageLineTarget, IDisposable
	{
		/// <summary>
		/// The first entry of the node list; it stands for "no node". It is a fixed marker, not a text: the window shows the
		/// localized text <c>Str.Flowchart.NoNodeChoice</c> in its place.
		/// </summary>
		public const String NoNodeChoice = "<Unselect Node>";

		private readonly DialogueScriptEditor _scriptEditor;
		private readonly DialogueNodeEditor _nodeEditor;
		private readonly IDialogueMessageService _messages;
		private readonly IDialogService _dialogs;
		private readonly IMessageEditorService _messageEditor;
		private readonly INodeCodeSnippetStore? _snippets;
		private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
		private DocumentViewModel? _script;
		private NodeMessageRowViewModel? _sendRow;
		private Boolean _refreshing;
		private Boolean _refreshAgain;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeCodeEditorViewModel"/> class.
		/// </summary>
		/// <param name="scriptEditor">Replaces the code of the node in the script.</param>
		/// <param name="nodeEditor">Lists the messages of the code and changes message numbers.</param>
		/// <param name="messages">Reads and writes the message files.</param>
		/// <param name="dialogs">Shows messages.</param>
		/// <param name="messageEditor">Opens the message editor.</param>
		/// <param name="snippets">The saved code snippets, or <see langword="null"/> when there is no snippet file.</param>
		public NodeCodeEditorViewModel(
			DialogueScriptEditor scriptEditor,
			DialogueNodeEditor nodeEditor,
			IDialogueMessageService messages,
			IDialogService dialogs,
			IMessageEditorService messageEditor,
			INodeCodeSnippetStore? snippets = null)
		{
			ArgumentNullException.ThrowIfNull(scriptEditor);
			ArgumentNullException.ThrowIfNull(nodeEditor);
			ArgumentNullException.ThrowIfNull(messages);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(messageEditor);
			this._scriptEditor = scriptEditor;
			this._nodeEditor = nodeEditor;
			this._messages = messages;
			this._dialogs = dialogs;
			this._messageEditor = messageEditor;
			this._snippets = snippets;
			this.Code = new TextDocument();
			this.Code.TextChanged += this.OnCodeChanged;
			this.Code.UndoStack.PropertyChanged += this.OnUndoStackChanged;
			if (snippets is not null)
			{
				snippets.Changed += this.OnSnippetsChanged;
			}
		}

		/// <summary>Stops listening to the snippet store, which outlives the window, so the closed editor can be released.</summary>
		public void Dispose()
		{
			if (this._snippets is not null)
			{
				this._snippets.Changed -= this.OnSnippetsChanged;
			}

			this.Code.TextChanged -= this.OnCodeChanged;
			this.Code.UndoStack.PropertyChanged -= this.OnUndoStackChanged;
		}

		/// <summary>Occurs after the code of the node was applied to the script.</summary>
		public event EventHandler? CodeApplied;

		/// <summary>Occurs when text should be inserted at the caret of the code editor (replacing the selection).</summary>
		public event EventHandler<String>? InsertTextRequested;

		/// <summary>Occurs when a range of the code should be selected in the code editor; the arguments are the offset and the length.</summary>
		public event EventHandler<(Int32 Offset, Int32 Length)>? SelectRangeRequested;

		/// <summary>Gets the script the node belongs to.</summary>
		public DocumentViewModel? Script => this._script;

		/// <summary>Gets the code of the node (the document of the code editor).</summary>
		public TextDocument Code { get; }

		/// <summary>Gets the messages the code uses.</summary>
		public ObservableCollection<NodeMessageRowViewModel> Rows { get; } = new ObservableCollection<NodeMessageRowViewModel>();

		/// <summary>Gets the saved code snippets.</summary>
		public ObservableCollection<NodeCodeSnippet> Snippets { get; } = new ObservableCollection<NodeCodeSnippet>();

		/// <summary>Gets the names offered by the node list, <see cref="NoNodeChoice"/> first.</summary>
		public ObservableCollection<String> NodeChoices { get; } = new ObservableCollection<String>();

		/// <summary>Gets a value indicating whether code snippets can be saved.</summary>
		public Boolean CanStoreSnippets => this._snippets is not null;

		/// <summary>Gets or sets the window the message editor belongs to.</summary>
		public Window? Owner { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether "Done" also closes the window. It is set when the editor was opened from the flowchart
		/// (legacy <c>CodeArgs.Close</c>); an editor opened from the procedures tree stays open after the code was applied.
		/// </summary>
		public Boolean CloseOnDone { get; set; }

		/// <summary>Gets or sets the name of the node being edited.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Title))]
		public partial String NodeName { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the code differs from the code in the script.</summary>
		[ObservableProperty]
		public partial Boolean HasChanges { get; set; }

		/// <summary>Gets or sets the message row that is selected.</summary>
		[ObservableProperty]
		public partial NodeMessageRowViewModel? SelectedRow { get; set; }

		/// <summary>Gets or sets the entry of the node list.</summary>
		[ObservableProperty]
		public partial String SelectedNodeChoice { get; set; } = NoNodeChoice;

		/// <summary>Gets or sets a value indicating whether the window stays above its owner.</summary>
		[ObservableProperty]
		public partial Boolean IsOnTop { get; set; }

		/// <summary>Gets the title of the window.</summary>
		public String Title => Loc.Format("Str.Flowchart.NodeEditorTitle", this.NodeName);

		/// <summary>Opens the code of a node of a script in the editor.</summary>
		/// <param name="script">The script.</param>
		/// <param name="nodeName">The node procedure name.</param>
		/// <returns>A task that completes when the code and the messages are shown.</returns>
		public async Task InitializeAsync(DocumentViewModel script, String nodeName)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);

			this._script = script;
			this.RefillNodeChoices();
			if (this._snippets is not null)
			{
				this.OnSnippetsChanged(this, EventArgs.Empty);
			}

			await this.ShowNodeAsync(nodeName);
		}

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

		/// <summary>
		/// Applies the code to the script when it changed. The window closes afterwards when <paramref name="close"/> is set.
		/// </summary>
		/// <param name="close">Whether the window closes.</param>
		/// <returns><see langword="false"/> when the script has no such node any more.</returns>
		public async Task<Boolean> ApplyAsync(Boolean close)
		{
			DocumentViewModel script = this.RequireScript();
			if (this.HasChanges)
			{
				if (!await this._scriptEditor.ReplaceNodeCodeAsync(script, this.NodeName, this.Code.Text))
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.ApplyError.Title"), Loc.Get("Str.Flowchart.Msg.ApplyError.Text"));
					return false;
				}

				this.Code.UndoStack.MarkAsOriginalFile();
				this.HasChanges = false;
				this.CodeApplied?.Invoke(this, EventArgs.Empty);
			}

			if (close)
			{
				this.RequestClose(true);
			}

			return true;
		}

		/// <summary>
		/// Asks what to do with unsaved changes before the window closes or another node is shown.
		/// </summary>
		/// <param name="force">Whether the window must close, so there is no Cancel choice.</param>
		/// <returns><see langword="true"/> when the window may close.</returns>
		public async Task<Boolean> ConfirmCloseAsync(Boolean force)
		{
			if (!this.HasChanges)
			{
				return true;
			}

			String caption = Loc.Get("Str.Flowchart.Msg.ApplyChanges.Title");
			String question = Loc.Get("Str.Flowchart.Msg.ApplyChanges.Text");
			if (force)
			{
				Boolean apply = await this._dialogs.ConfirmAsync(caption, question);
				return !apply || await this.ApplyAsync(false);
			}

			switch (await this._dialogs.AskYesNoCancelAsync(caption, question))
			{
				case YesNoCancel.Yes:
					return await this.ApplyAsync(false);
				case YesNoCancel.No:
					return true;
				default:
					return false;
			}
		}

		/// <summary>Shows the code of the node chosen in the node list (asks about unsaved changes first).</summary>
		/// <returns>A task that completes when the node is shown.</returns>
		[RelayCommand]
		public async Task GoToNodeAsync()
		{
			if (this.SelectedNodeChoice == NoNodeChoice || this._script is null || !await this.ConfirmCloseAsync(false))
			{
				return;
			}

			await this.ShowNodeAsync(this.SelectedNodeChoice);
		}

		/// <summary>Writes the code into the script and closes the window.</summary>
		/// <returns>A task that completes when the code is applied.</returns>
		[RelayCommand]
		public async Task DoneAsync()
		{
			await this.ApplyAsync(this.CloseOnDone);
		}

		/// <summary>Undoes the last change of the code.</summary>
		[RelayCommand]
		public void Undo()
		{
			if (this.Code.UndoStack.CanUndo)
			{
				this.Code.UndoStack.Undo();
			}
		}

		/// <summary>Redoes the last undone change of the code.</summary>
		[RelayCommand]
		public void Redo()
		{
			if (this.Code.UndoStack.CanRedo)
			{
				this.Code.UndoStack.Redo();
			}
		}

		/// <summary>Pastes the name of the node chosen in the node list at the caret.</summary>
		[RelayCommand]
		public void PasteNodeName()
		{
			if (this.SelectedNodeChoice != NoNodeChoice)
			{
				this.InsertTextRequested?.Invoke(this, this.SelectedNodeChoice);
			}
		}

		/// <summary>
		/// Called after the user picked a node in the node list: when some code is selected, the selection is replaced by the node name
		/// (legacy <c>cmbNodesName_DropDownClosed</c>).
		/// </summary>
		/// <param name="hasSelection">Whether the code editor has a selection.</param>
		public void NodeChoiceCommitted(Boolean hasSelection)
		{
			if (hasSelection)
			{
				this.PasteNodeName();
			}
		}

		/// <summary>Pastes a statement or a saved snippet at the caret.</summary>
		/// <param name="text">The text to paste.</param>
		[RelayCommand]
		public void InsertText(String? text)
		{
			if (!String.IsNullOrEmpty(text))
			{
				this.InsertTextRequested?.Invoke(this, text);
			}
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

		/// <summary>Saves a piece of code as a named snippet for the "Custom" menu.</summary>
		/// <param name="code">The code, usually the selected text.</param>
		/// <returns>A task that completes when the snippet is saved.</returns>
		public async Task StoreSnippetAsync(String code)
		{
			ArgumentNullException.ThrowIfNull(code);

			if (this._snippets is null || code.Length == 0)
			{
				return;
			}

			String? name = await this._dialogs.PromptAsync(Loc.Get("Str.Flowchart.Msg.CustomCode.Title"), Loc.Get("Str.Flowchart.Msg.CustomCode.Prompt"));
			if (!String.IsNullOrWhiteSpace(name))
			{
				await this._snippets.AddAsync(name.Trim(), code);
			}
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
		}

		partial void OnSelectedRowChanged(NodeMessageRowViewModel? value)
		{
			if (value is not null && this.GetStatementRange(value) is { } range)
			{
				this.SelectRangeRequested?.Invoke(this, range);
			}
		}

		private DocumentViewModel RequireScript()
		{
			return this._script ?? throw new InvalidOperationException("The editor was not initialized.");
		}

		private async Task ShowNodeAsync(String nodeName)
		{
			DocumentViewModel script = this.RequireScript();
			String code = this._scriptEditor.GetNodeCode(script, nodeName) ?? String.Empty;
			this.NodeName = nodeName;
			this.Code.Text = code;
			this.Code.UndoStack.ClearAll();
			this.Code.UndoStack.MarkAsOriginalFile();
			this.HasChanges = false;
			this.SelectedNodeChoice = NoNodeChoice;
			await this.RefreshRowsAsync();
		}

		private void RefillNodeChoices()
		{
			this.NodeChoices.Clear();
			this.NodeChoices.Add(NoNodeChoice);
			foreach (String name in this._scriptEditor.GetNodeNames(this.RequireScript()).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
			{
				this.NodeChoices.Add(name);
			}
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

		private void OnCodeChanged(Object? sender, EventArgs e)
		{
			this.HasChanges = !this.Code.UndoStack.IsOriginalFile;
		}

		private void OnUndoStackChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
			{
				this.HasChanges = !this.Code.UndoStack.IsOriginalFile;
			}
		}

		private void OnSnippetsChanged(Object? sender, EventArgs e)
		{
			if (!this._dispatcher.CheckAccess())
			{
				// The store raises the event on the thread that finished its file operation; the list is bound to the window.
				_ = this._dispatcher.BeginInvoke(new Action(() => this.OnSnippetsChanged(sender, e)));
				return;
			}

			this.Snippets.Clear();
			if (this._snippets is not null)
			{
				foreach (NodeCodeSnippet snippet in this._snippets.Snippets)
				{
					this.Snippets.Add(snippet);
				}
			}
		}
	}
}
