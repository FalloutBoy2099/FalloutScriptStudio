namespace FalloutScriptStudio.ViewModels.DialoguePreview
{
	using System;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>
	/// The view model of the dialogue preview window (legacy <c>DialogPreview</c>): shows the replies and options of one dialogue
	/// node the way the game would, lets the user follow the options, and jumps to the code or to the message editor.
	/// </summary>
	public sealed partial class DialoguePreviewViewModel : ObservableObject, IDisposable
	{
		/// <summary>The offset of the female messages that the legacy window suggested.</summary>
		public const String DefaultFemaleOffset = "1000";

		private readonly DialoguePreview _preview;
		private readonly IWorkspace _workspace;
		private readonly IDialogueMessageService _messages;
		private readonly IMessageEditorService _messageEditor;
		private DocumentViewModel? _script;
		private ReloadableMessageSource? _source;
		private Boolean _syncing;
		private readonly LanguageChangedSubscription _languageSubscription;
		private Boolean _started;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialoguePreviewViewModel"/> class.
		/// </summary>
		/// <param name="preview">The state of the preview.</param>
		/// <param name="workspace">Navigates to the code and reports changes of the script.</param>
		/// <param name="messages">Reads the message files.</param>
		/// <param name="messageEditor">Opens the message editor.</param>
		public DialoguePreviewViewModel(DialoguePreview preview, IWorkspace workspace, IDialogueMessageService messages, IMessageEditorService messageEditor)
		{
			ArgumentNullException.ThrowIfNull(preview);
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(messages);
			ArgumentNullException.ThrowIfNull(messageEditor);
			this._preview = preview;
			this._workspace = workspace;
			this._messages = messages;
			this._messageEditor = messageEditor;
			this._preview.Changed += this.OnPreviewChanged;
			this._workspace.DocumentParsed += this.OnDocumentParsed;
			this._languageSubscription = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.RefreshTitle());
		}

		/// <summary>Gets the script being previewed.</summary>
		public DocumentViewModel? Script => this._script;

		/// <summary>Gets or sets the window the message editor belongs to.</summary>
		public Window? Owner { get; set; }

		/// <summary>Gets a value indicating whether the script changed since the preview was built.</summary>
		public Boolean NeedUpdate { get; private set; }

		/// <summary>Gets the names of the dialogue nodes of the script.</summary>
		public ObservableCollection<String> NodeNames { get; } = new ObservableCollection<String>();

		/// <summary>Gets the rows of the node being shown.</summary>
		public ObservableCollection<DialoguePreviewRowViewModel> Rows { get; } = new ObservableCollection<DialoguePreviewRowViewModel>();

		/// <summary>Gets or sets the title of the window.</summary>
		[ObservableProperty]
		public partial String Title { get; set; } = Loc.Get("Str.Flowchart.PreviewTitle");

		/// <summary>Gets or sets the node being shown.</summary>
		[ObservableProperty]
		public partial String? SelectedNode { get; set; }

		/// <summary>Gets or sets the text of the last option the user followed.</summary>
		[ObservableProperty]
		public partial String OptionText { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the code editor jumps to the node after an option is followed.</summary>
		[ObservableProperty]
		public partial Boolean AutoJump { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the female variants of the messages are shown.</summary>
		[ObservableProperty]
		public partial Boolean IsFemale { get; set; }

		/// <summary>Gets or sets the number added to the message numbers in female mode.</summary>
		[ObservableProperty]
		public partial String FemaleOffsetText { get; set; } = DefaultFemaleOffset;

		/// <summary>Gets or sets the row that is selected.</summary>
		[ObservableProperty]
		public partial DialoguePreviewRowViewModel? SelectedRow { get; set; }

		/// <summary>Gets a value indicating whether the history has a node to go back to.</summary>
		public Boolean CanGoBack => this._preview.CanGoBack;

		/// <summary>Gets a value indicating whether the history has a node to go forward to.</summary>
		public Boolean CanGoForward => this._preview.CanGoForward;

		/// <summary>
		/// Starts the preview of a script. The node that contains the caret of the editor is shown first, otherwise
		/// <c>talk_p_proc</c>.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <returns><see langword="false"/> when the script has no dialogue to show.</returns>
		public async Task<Boolean> InitializeAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			this._script = script;
			this.RefreshTitle();
			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return false;
			}

			this._source = new ReloadableMessageSource(await this._messages.CreateSourceAsync(script));
			Int32 caretLine = script.Editor?.CaretLine ?? 1;
			this._started = await this._preview.StartAsync(script.TextDocument.Text, info, this._source, caretLine);
			this.NeedUpdate = false;
			this.SyncFromPreview();
			return this._started;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this._preview.Changed -= this.OnPreviewChanged;
			this._workspace.DocumentParsed -= this.OnDocumentParsed;
			this._languageSubscription.Dispose();
		}

		/// <summary>Handles the activation of the window: shows the changes made to the script meanwhile.</summary>
		/// <returns>A task that completes when the preview is up to date.</returns>
		public async Task OnActivatedAsync()
		{
			if (this.NeedUpdate)
			{
				await this.UpdateScriptAsync();
			}
		}

		/// <summary>Reads the script and the message files again and shows the current node again.</summary>
		/// <returns>A task that completes when the rows are rebuilt.</returns>
		[RelayCommand]
		public async Task UpdateScriptAsync()
		{
			DocumentViewModel script = this.RequireScript();
			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			if (script.ProgramInfo is null || this._source is null)
			{
				return;
			}

			this._source.Replace(await this._messages.CreateSourceAsync(script));
			this.NeedUpdate = false;
			await this._preview.UpdateScriptAsync(script.TextDocument.Text, script.ProgramInfo);
		}

		/// <summary>Shows the previous node of the history.</summary>
		/// <returns>A task that completes when the node is shown.</returns>
		[RelayCommand(CanExecute = nameof(CanGoBack))]
		public async Task GoBackAsync()
		{
			await this._preview.GoBackAsync();
		}

		/// <summary>Shows the next node of the history.</summary>
		/// <returns>A task that completes when the node is shown.</returns>
		[RelayCommand(CanExecute = nameof(CanGoForward))]
		public async Task GoForwardAsync()
		{
			await this._preview.GoForwardAsync();
		}

		/// <summary>Follows an option or a call to its node; the code editor jumps to the node when <see cref="AutoJump"/> is on.</summary>
		/// <param name="row">The row.</param>
		/// <returns>A task that completes when the node is shown.</returns>
		[RelayCommand]
		public async Task FollowAsync(DialoguePreviewRowViewModel? row)
		{
			if (row is null || !row.CanFollow)
			{
				return;
			}

			SourceReference? target = this._preview.GetJumpTarget(row.Source);
			this.OptionText = row.Text;
			if (await this._preview.FollowAsync(row.Source) && this.AutoJump)
			{
				await this.GoToReferenceAsync(target);
			}
		}

		/// <summary>Moves the code editor to the node of a row: the current node for texts, the target node for options.</summary>
		/// <param name="row">The row.</param>
		/// <returns>A task that completes when the editor moved.</returns>
		[RelayCommand]
		public async Task JumpToCodeAsync(DialoguePreviewRowViewModel? row)
		{
			if (row is not null)
			{
				await this.GoToReferenceAsync(this._preview.GetJumpTarget(row.Source));
			}
		}

		/// <summary>Opens the message editor at the message of a row, and shows the changes afterwards.</summary>
		/// <param name="row">The row.</param>
		/// <returns>A task that completes when the message editor is closed.</returns>
		[RelayCommand]
		public async Task EditMessageAsync(DialoguePreviewRowViewModel? row)
		{
			if (row is null || !row.CanEditMessage)
			{
				return;
			}

			this._messageEditor.Open(new MessageEditorOpenOptions
			{
				FilePath = row.Source.MessageFilePath,
				SelectMessageId = row.Source.MessageNumber,
				Modal = true,
				Owner = this.Owner,
			});
			await this.ReloadMessagesAsync();
		}

		partial void OnSelectedNodeChanged(String? value)
		{
			if (!this._syncing && value is not null && this._started && !String.Equals(value, this._preview.CurrentNode, StringComparison.Ordinal))
			{
				_ = this._preview.SelectNodeAsync(value);
			}
		}

		partial void OnIsFemaleChanged(Boolean value)
		{
			_ = this.ApplyFemaleModeAsync();
		}

		partial void OnFemaleOffsetTextChanged(String value)
		{
			if (this.IsFemale)
			{
				_ = this.ApplyFemaleModeAsync();
			}
		}

		private void RefreshTitle()
		{
			this.Title = this._script is null ? Loc.Get("Str.Flowchart.PreviewTitle") : Loc.Format("Str.Flowchart.PreviewTitleFor", this._script.FileName);
		}

		private DocumentViewModel RequireScript()
		{
			return this._script ?? throw new InvalidOperationException("The preview was not started.");
		}

		private async Task ApplyFemaleModeAsync()
		{
			if (!this._started)
			{
				return;
			}

			Int32 offset = 0;
			if (this.IsFemale && !Int32.TryParse(this.FemaleOffsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
			{
				offset = 0;
			}

			await this._preview.SetFemaleModeAsync(this.IsFemale, offset);
		}

		private async Task ReloadMessagesAsync()
		{
			if (this._script is null || this._source is null)
			{
				return;
			}

			this._source.Replace(await this._messages.CreateSourceAsync(this._script));
			await this._preview.RefreshAsync();
		}

		private async Task GoToReferenceAsync(SourceReference? target)
		{
			if (target is not null && this._script is not null && target.Line > 0)
			{
				await this._workspace.GoToAsync(target.File ?? this._script.FilePath, target.Line);
			}
		}

		private void OnPreviewChanged(Object? sender, EventArgs e)
		{
			this.SyncFromPreview();
		}

		private void OnDocumentParsed(Object? sender, DocumentViewModel document)
		{
			if (ReferenceEquals(document, this._script))
			{
				this.NeedUpdate = true;
			}
		}

		private void SyncFromPreview()
		{
			this._syncing = true;
			try
			{
				if (!this._preview.NodeNames.SequenceEqual(this.NodeNames, StringComparer.Ordinal))
				{
					this.NodeNames.Clear();
					foreach (String name in this._preview.NodeNames)
					{
						this.NodeNames.Add(name);
					}
				}

				this.SelectedRow = null;
				this.Rows.Clear();
				foreach (DialoguePreviewRow row in this._preview.Rows)
				{
					this.Rows.Add(new DialoguePreviewRowViewModel(row));
				}

				this.SelectedNode = this._preview.CurrentNode;
			}
			finally
			{
				this._syncing = false;
			}

			this.OnPropertyChanged(nameof(this.CanGoBack));
			this.OnPropertyChanged(nameof(this.CanGoForward));
			this.GoBackCommand.NotifyCanExecuteChanged();
			this.GoForwardCommand.NotifyCanExecuteChanged();
		}
	}
}
