namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Globalization;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// One row of the message grid: a message, a comment line, an empty line or an unrecognized line of a <c>.msg</c> file.
	/// All edits made through the properties are validated, recorded for undo and flagged as changed until the file is saved.
	/// </summary>
	public sealed class MessageRowViewModel : ObservableObject
	{
		/// <summary>The marker shown for a player line in the legacy editor; it is removed when typed into a text.</summary>
		private const Char PlayerMarkerChar = '●';

		private static readonly Char[] _braces = { '{', '}' };

		private readonly IMessageRowOwner _owner;
		private Boolean _isLineChanged;
		private Boolean _isTextChanged;
		private Boolean _isCommentChanged;
		private Boolean _isSoundChanged;
		private MessageIssueSeverity _severity;
		private String _issueText = String.Empty;
		private Boolean _isSearchMatch;
		private RowState? _capturedState;

		internal MessageRowViewModel(MessageEntry entry, IMessageRowOwner owner)
		{
			ArgumentNullException.ThrowIfNull(entry);
			ArgumentNullException.ThrowIfNull(owner);
			this.Entry = entry;
			this._owner = owner;
		}

		/// <summary>Gets the underlying entry of the document. Edit it through the row properties only.</summary>
		public MessageEntry Entry { get; }

		/// <summary>Gets the kind of the row.</summary>
		public MessageLineKind Kind => this.Entry.Kind;

		/// <summary>Gets a value indicating whether the row is a <c>{id}{sound}{text}</c> message.</summary>
		public Boolean IsMessage => this.Entry.Kind == MessageLineKind.Message;

		/// <summary>Gets a value indicating whether the row is a comment line.</summary>
		public Boolean IsComment => this.Entry.Kind == MessageLineKind.Comment;

		/// <summary>Gets a value indicating whether the row is an empty line.</summary>
		public Boolean IsBlank => this.Entry.Kind == MessageLineKind.Blank;

		/// <summary>Gets a value indicating whether the row can be edited in the line number column.</summary>
		public Boolean CanEditLine => this.Entry.Kind != MessageLineKind.Comment;

		/// <summary>
		/// Gets or sets the text of the line column: the message number, <c>-</c> for comments, <c>?</c> for unrecognized lines and nothing for empty lines.
		/// Typing a number into an empty or unrecognized line turns it into a message.
		/// </summary>
		public String LineText
		{
			get
			{
				return this.Entry.Kind switch
				{
					MessageLineKind.Message => this.Entry.Id.ToString(CultureInfo.InvariantCulture),
					MessageLineKind.Comment => "-",
					MessageLineKind.Raw => "?",
					_ => String.Empty,
				};
			}

			set
			{
				this.TrySetLine(value);
			}
		}

		/// <summary>
		/// Gets or sets the message text, or the whole line of comments and unrecognized lines.
		/// Messages may span several lines but cannot contain curly braces.
		/// </summary>
		public String Text
		{
			get
			{
				return this.Entry.Text;
			}

			set
			{
				this.TrySetText(value);
			}
		}

		/// <summary>Gets or sets the trailing <c># comment</c> of a message (shown without surrounding spaces).</summary>
		public String Comment
		{
			get
			{
				return this.Entry.Description.Trim();
			}

			set
			{
				this.TrySetComment(value);
			}
		}

		/// <summary>Gets or sets the sound (lip-sync) file name of a message.</summary>
		public String SoundName
		{
			get
			{
				return this.Entry.SoundName;
			}

			set
			{
				this.TrySetSound(value);
			}
		}

		/// <summary>Gets or sets a value indicating whether the message is a player (response) line.</summary>
		public Boolean IsPlayerLine
		{
			get
			{
				return this.Entry.IsPlayerLine;
			}

			set
			{
				if (!this.IsMessage || this.Entry.IsPlayerLine == value)
				{
					this.OnPropertyChanged();
					return;
				}

				this._owner.RowEditing(this);
				this.ApplyPlayerLine(value);
				this._owner.RowEdited(this);
			}
		}

		/// <summary>Gets a value indicating whether the line number was edited since the last save.</summary>
		public Boolean IsLineChanged
		{
			get => this._isLineChanged;
			private set => this.SetProperty(ref this._isLineChanged, value);
		}

		/// <summary>Gets a value indicating whether the text was edited since the last save.</summary>
		public Boolean IsTextChanged
		{
			get => this._isTextChanged;
			private set => this.SetProperty(ref this._isTextChanged, value);
		}

		/// <summary>Gets a value indicating whether the comment was edited since the last save.</summary>
		public Boolean IsCommentChanged
		{
			get => this._isCommentChanged;
			private set => this.SetProperty(ref this._isCommentChanged, value);
		}

		/// <summary>Gets a value indicating whether the sound name was edited since the last save.</summary>
		public Boolean IsSoundChanged
		{
			get => this._isSoundChanged;
			private set => this.SetProperty(ref this._isSoundChanged, value);
		}

		/// <summary>Gets the most serious structure problem found in the row.</summary>
		public MessageIssueSeverity Severity
		{
			get => this._severity;
			internal set
			{
				if (this.SetProperty(ref this._severity, value))
				{
					this.OnPropertyChanged(nameof(this.HasIssue));
				}
			}
		}

		/// <summary>Gets the descriptions of the structure problems of the row, one per line.</summary>
		public String IssueText
		{
			get => this._issueText;
			internal set => this.SetProperty(ref this._issueText, value);
		}

		/// <summary>Gets a value indicating whether the structure check found a problem in the row.</summary>
		public Boolean HasIssue => this._severity != MessageIssueSeverity.None;

		/// <summary>Gets a value indicating whether the row contains the current search text.</summary>
		public Boolean IsSearchMatch
		{
			get => this._isSearchMatch;
			internal set => this.SetProperty(ref this._isSearchMatch, value);
		}

		/// <summary>Gets or sets a value indicating whether the row stays visible while the grid is filtered (a row that was just created).</summary>
		internal Boolean IsPinnedVisible { get; set; }

		internal static String NormalizeLineBreaks(String text)
		{
			if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
			{
				return text;
			}

			return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", MessageEntry.LineBreak, StringComparison.Ordinal);
		}

		/// <summary>
		/// Brings a comment into the stored form: empty, or starting with <c>#</c> and a leading space.
		/// </summary>
		/// <param name="value">The text typed by the user.</param>
		/// <returns>The text to store after a message.</returns>
		internal static String NormalizeComment(String value)
		{
			String trimmed = (value ?? String.Empty).Trim();
			if (trimmed.Length == 0)
			{
				return String.Empty;
			}

			return trimmed.StartsWith(MessageFileTexts.CommentPrefix, StringComparison.Ordinal) ? " " + trimmed : " # " + trimmed;
		}

		internal static MessageEntry CloneEntry(MessageEntry source)
		{
			return new MessageEntry
			{
				Kind = source.Kind,
				Id = source.Id,
				SoundName = source.SoundName,
				Text = source.Text,
				Description = source.Description,
				IsPlayerLine = source.IsPlayerLine,
				IndentTabs = source.IndentTabs,
			};
		}

		/// <summary>
		/// Captures the row for the undo history. A row that did not change since it was captured last returns the same state, so the
		/// snapshots of a big file share the states of the rows that were not edited instead of copying every row at every edit.
		/// </summary>
		internal RowState CaptureState()
		{
			RowState? cached = this._capturedState;
			if (cached is not null && cached.Describes(this.Entry, this._isLineChanged, this._isTextChanged, this._isCommentChanged, this._isSoundChanged))
			{
				return cached;
			}

			RowState state = new RowState(CloneEntry(this.Entry), this._isLineChanged, this._isTextChanged, this._isCommentChanged, this._isSoundChanged);
			this._capturedState = state;
			return state;
		}

		internal static MessageRowViewModel FromState(RowState state, IMessageRowOwner owner)
		{
			return new MessageRowViewModel(CloneEntry(state.Entry), owner)
			{
				_isLineChanged = state.LineChanged,
				_isTextChanged = state.TextChanged,
				_isCommentChanged = state.CommentChanged,
				_isSoundChanged = state.SoundChanged,
				_capturedState = state,
			};
		}

		/// <summary>Forgets the changed flags; called after the file was saved.</summary>
		internal void ClearChangedFlags()
		{
			this.IsLineChanged = false;
			this.IsTextChanged = false;
			this.IsCommentChanged = false;
			this.IsSoundChanged = false;
		}

		/// <summary>Sets or clears the player line state without recording undo information.</summary>
		/// <param name="value">The new state.</param>
		internal void ApplyPlayerLine(Boolean value)
		{
			if (!this.IsMessage || this.Entry.IsPlayerLine == value)
			{
				return;
			}

			this.Entry.IsPlayerLine = value;
			this.Entry.IndentTabs = Math.Max(1, this.Entry.IndentTabs);
			this.OnPropertyChanged(nameof(this.IsPlayerLine));
		}

		/// <summary>
		/// Checks whether any shown cell contains the text.
		/// </summary>
		/// <param name="needle">The text to find; compared ignoring the letter case.</param>
		/// <param name="includeSound">Whether the sound name column is searched too.</param>
		/// <returns><see langword="true"/> if the row matches.</returns>
		internal Boolean Matches(String needle, Boolean includeSound)
		{
			if (needle.Length == 0)
			{
				return false;
			}

			return (this.IsMessage && this.LineText.Contains(needle, StringComparison.OrdinalIgnoreCase))
				|| this.Entry.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)
				|| (this.IsMessage && this.Entry.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
				|| (includeSound && this.IsMessage && this.Entry.SoundName.Contains(needle, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets the text of a column the way the grid shows it.
		/// </summary>
		/// <param name="column">The column.</param>
		/// <returns>The cell text.</returns>
		internal String GetCellText(MessageGridColumn column)
		{
			return column switch
			{
				MessageGridColumn.Line => this.LineText,
				MessageGridColumn.Text => this.Text,
				MessageGridColumn.Comment => this.Comment,
				MessageGridColumn.Sound => this.SoundName,
				_ => String.Empty,
			};
		}

		/// <summary>Raises the change notifications of every property that depends on the kind of the row.</summary>
		internal void NotifyKindChanged()
		{
			this.OnPropertyChanged(nameof(this.Kind));
			this.OnPropertyChanged(nameof(this.IsMessage));
			this.OnPropertyChanged(nameof(this.IsComment));
			this.OnPropertyChanged(nameof(this.IsBlank));
			this.OnPropertyChanged(nameof(this.CanEditLine));
			this.OnPropertyChanged(nameof(this.LineText));
			this.OnPropertyChanged(nameof(this.Comment));
			this.OnPropertyChanged(nameof(this.SoundName));
			this.OnPropertyChanged(nameof(this.IsPlayerLine));
		}

		private static MessageLineKind Classify(String text)
		{
			if (text.Length == 0)
			{
				return MessageLineKind.Blank;
			}

			return text.TrimStart().StartsWith(MessageFileTexts.CommentPrefix, StringComparison.Ordinal) ? MessageLineKind.Comment : MessageLineKind.Raw;
		}

		private void TrySetLine(String? value)
		{
			String text = (value ?? String.Empty).Trim();
			if (this.Entry.Kind == MessageLineKind.Comment || (text.Length == 0 && this.Entry.Kind != MessageLineKind.Message))
			{
				this.OnPropertyChanged(nameof(this.LineText));
				return;
			}

			if (!Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out Int32 id))
			{
				this._owner.RowRejected(Loc.Get("Str.MessageEditor.Msg.LineDigits"));
				this.OnPropertyChanged(nameof(this.LineText));
				return;
			}

			if (this.Entry.Kind == MessageLineKind.Message && this.Entry.Id == id)
			{
				return;
			}

			if (this.Entry.Kind != MessageLineKind.Message && this.Entry.Text.IndexOfAny(_braces) >= 0)
			{
				this._owner.RowRejected(Loc.Get("Str.MessageEditor.Msg.LineRemoveBraces"));
				this.OnPropertyChanged(nameof(this.LineText));
				return;
			}

			this._owner.RowEditing(this);
			Boolean wasMessage = this.IsMessage;
			this.Entry.Kind = MessageLineKind.Message;
			this.Entry.Id = id;
			this.IsLineChanged = true;
			if (wasMessage)
			{
				this.OnPropertyChanged(nameof(this.LineText));
			}
			else
			{
				this.NotifyKindChanged();
			}

			this._owner.RowEdited(this);
		}

		private void TrySetText(String? value)
		{
			String text = NormalizeLineBreaks(value ?? String.Empty);
			if (this.IsMessage)
			{
				text = StripPlayerMarker(text);
				if (text.IndexOfAny(_braces) >= 0)
				{
					this._owner.RowRejected(Loc.Get("Str.MessageEditor.Msg.BracesInText"));
					this.OnPropertyChanged(nameof(this.Text));
					return;
				}

				if (String.Equals(text, this.Entry.Text, StringComparison.Ordinal))
				{
					this.OnPropertyChanged(nameof(this.Text));
					return;
				}

				this._owner.RowEditing(this);
				this.Entry.Text = text;
				this.IsTextChanged = true;
				this.OnPropertyChanged(nameof(this.Text));
				this._owner.RowEdited(this);
				return;
			}

			if (String.Equals(text, this.Entry.Text, StringComparison.Ordinal))
			{
				return;
			}

			this._owner.RowEditing(this);
			this.ApplyFreeText(text);
			this.IsTextChanged = true;
			this.NotifyKindChanged();
			this.OnPropertyChanged(nameof(this.Text));
			this._owner.RowEdited(this);
		}

		private void ApplyFreeText(String text)
		{
			MessageLineKind kind = Classify(text);
			if (kind == MessageLineKind.Raw)
			{
				// A complete "{id}{sound}{text}" line typed or pasted into an empty row becomes a real message.
				MessageDocument parsed = MessageDocument.Parse(new[] { text });
				if (parsed.Entries.Count == 1 && parsed.Entries[0].Kind == MessageLineKind.Message)
				{
					MessageEntry message = parsed.Entries[0];
					this.Entry.Kind = MessageLineKind.Message;
					this.Entry.Id = message.Id;
					this.Entry.SoundName = message.SoundName;
					this.Entry.Text = message.Text;
					this.Entry.Description = message.Description;
					this.Entry.IsPlayerLine = message.IsPlayerLine;
					this.Entry.IndentTabs = message.IndentTabs;
					return;
				}
			}

			this.Entry.Kind = kind;
			this.Entry.Text = text;
			this.Entry.Description = String.Empty;
			this.Entry.Id = 0;
			this.Entry.SoundName = String.Empty;
			this.Entry.IsPlayerLine = false;
		}

		private void TrySetComment(String? value)
		{
			if (!this.IsMessage)
			{
				this.OnPropertyChanged(nameof(this.Comment));
				return;
			}

			String normalized = NormalizeComment(value ?? String.Empty);
			if (String.Equals(normalized.Trim(), this.Entry.Description.Trim(), StringComparison.Ordinal))
			{
				this.OnPropertyChanged(nameof(this.Comment));
				return;
			}

			this._owner.RowEditing(this);
			this.Entry.Description = normalized;
			this.IsCommentChanged = true;
			this.OnPropertyChanged(nameof(this.Comment));
			this._owner.RowEdited(this);
		}

		private void TrySetSound(String? value)
		{
			String sound = value ?? String.Empty;
			if (!this.IsMessage)
			{
				this.OnPropertyChanged(nameof(this.SoundName));
				return;
			}

			if (sound.IndexOfAny(_braces) >= 0)
			{
				this._owner.RowRejected(Loc.Get("Str.MessageEditor.Msg.BracesInSound"));
				this.OnPropertyChanged(nameof(this.SoundName));
				return;
			}

			if (String.Equals(sound, this.Entry.SoundName, StringComparison.Ordinal))
			{
				return;
			}

			this._owner.RowEditing(this);
			this.Entry.SoundName = sound;
			this.IsSoundChanged = true;
			this.OnPropertyChanged(nameof(this.SoundName));
			this._owner.RowEdited(this);
		}

		private static String StripPlayerMarker(String text)
		{
			String trimmed = text.TrimStart();
			return trimmed.Length > 0 && trimmed[0] == PlayerMarkerChar ? trimmed.Substring(1).TrimStart() : text;
		}
	}
}
