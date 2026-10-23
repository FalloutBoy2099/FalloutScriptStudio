namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.ObjectModel;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.ViewModels.Help;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Help;

	/// <summary>
	/// The "Help" tab of the right panel: the documentation of the function, opcode, macro or procedure under the caret of the active script
	/// (it follows the caret after a short pause), or of any function the user finds with the filter box. The descriptions are the English ones
	/// of the description files, the labels around them follow the language of the interface.
	/// </summary>
	public sealed partial class HelpPanelViewModel : ObservableObject, IDisposable
	{
		/// <summary>The default pause between a move of the caret and the update of the panel.</summary>
		public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(250);

		/// <summary>The most functions the filter lists.</summary>
		public const Int32 MaxResults = 200;

		private readonly IFunctionHelpService _help;
		private readonly ActiveEditorTracker _tracker;
		private readonly IWorkspace _workspace;
		private readonly IShellService _shell;
		private readonly IDialogService _dialogs;
		private readonly TimeSpan _delay;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private readonly Object _gate = new();
		private Timer? _timer;
		private Int32 _generation;
		private Boolean _disposed;

		/// <summary>Initializes the panel.</summary>
		/// <param name="help">The documentation.</param>
		/// <param name="tracker">The active editor.</param>
		/// <param name="workspace">Writes the status messages.</param>
		/// <param name="shell">The clipboard.</param>
		/// <param name="dialogs">Tells that the documentation could not be opened.</param>
		/// <param name="delay">The pause before the panel follows the caret; <see cref="DefaultDelay"/> when <see langword="null"/>.</param>
		public HelpPanelViewModel(IFunctionHelpService help, ActiveEditorTracker tracker, IWorkspace workspace, IShellService shell, IDialogService dialogs, TimeSpan? delay = null)
		{
			ArgumentNullException.ThrowIfNull(help);
			ArgumentNullException.ThrowIfNull(tracker);
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(shell);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._help = help;
			this._tracker = tracker;
			this._workspace = workspace;
			this._shell = shell;
			this._dialogs = dialogs;
			this._delay = delay ?? DefaultDelay;
			this._tracker.CaretMoved += this.OnCaretMoved;
			this._tracker.EditorChanged += this.OnCaretMoved;
			this._help.Changed += this.OnHelpChanged;
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.OnLanguageChanged());
			this.Update();
		}

		/// <summary>Gets the functions the filter found (empty while the filter is blank).</summary>
		public ObservableCollection<HelpTopic> Results { get; } = [];

		/// <summary>The topic that is shown, or <see langword="null"/> when the caret is not on a word that has help.</summary>
		[ObservableProperty]
		public partial HelpTopicViewModel? Current { get; set; }

		/// <summary>The function chosen in the list of the filter.</summary>
		[ObservableProperty]
		public partial HelpTopic? SelectedResult { get; set; }

		/// <summary>The text the functions are filtered with; while it is blank the panel follows the caret.</summary>
		[ObservableProperty]
		public partial String FilterText { get; set; } = String.Empty;

		/// <summary>Gets a value indicating whether a topic is shown.</summary>
		public Boolean HasCurrent => this.Current is not null;

		/// <summary>Gets a value indicating whether the list of the filter is shown.</summary>
		public Boolean ShowResults => this.FilterText.Trim().Length > 0;

		/// <summary>Gets a value indicating whether the filter found nothing.</summary>
		public Boolean HasNoResults => this.ShowResults && this.Results.Count == 0;

		/// <summary>Gets a value indicating whether the panel has nothing to show (the empty text is shown).</summary>
		public Boolean IsEmpty => !this.HasCurrent && !this.ShowResults;

		/// <summary>Brings the panel up to date now: the topic under the caret, or the list of the filter.</summary>
		public void Update()
		{
			Interlocked.Increment(ref this._generation);
			if (this.ShowResults)
			{
				this.Search();
				return;
			}

			this.ShowTopicUnderCaret();
		}

		/// <summary>Stops following the caret.</summary>
		public void Dispose()
		{
			lock (this._gate)
			{
				this._disposed = true;
				this._timer?.Dispose();
				this._timer = null;
			}

			this._tracker.CaretMoved -= this.OnCaretMoved;
			this._tracker.EditorChanged -= this.OnCaretMoved;
			this._help.Changed -= this.OnHelpChanged;
		}

		partial void OnCurrentChanged(HelpTopicViewModel? value)
		{
			this.OnPropertyChanged(nameof(this.HasCurrent));
			this.OnPropertyChanged(nameof(this.IsEmpty));
		}

		partial void OnFilterTextChanged(String value)
		{
			this.OnPropertyChanged(nameof(this.ShowResults));
			this.Update();
			this.OnPropertyChanged(nameof(this.IsEmpty));
		}

		partial void OnSelectedResultChanged(HelpTopic? value)
		{
			if (value is not null)
			{
				this.Current = this.CreateViewModel(value);
			}
		}

		private void Search()
		{
			this.Results.Clear();
			foreach (HelpTopic topic in this._help.Search(this.FilterText, MaxResults))
			{
				this.Results.Add(topic);
			}

			this.OnPropertyChanged(nameof(this.HasNoResults));
			if (this.SelectedResult is { } selected && this.Results.Contains(selected))
			{
				return;
			}

			this.SelectedResult = this.Results.Count > 0 ? this.Results[0] : null;
			if (this.Results.Count == 0)
			{
				this.Current = null;
			}
		}

		private void ShowTopicUnderCaret()
		{
			if (this.Results.Count > 0)
			{
				this.Results.Clear();
				this.SelectedResult = null;
				this.OnPropertyChanged(nameof(this.HasNoResults));
			}

			IEditorSurface? editor = this._tracker.Editor;
			DocumentViewModel? document = this._tracker.Document;
			HelpTopic? topic = null;
			if (editor is not null && document is not null && document.IsParsable)
			{
				String word = editor.WordAtCaret.Trim();
				if (word.Length > 0 && IsWord(word))
				{
					topic = this._help.Find(word, document.ProgramInfo, document.FilePath, editor.CaretLine);
				}
			}

			if (topic is null)
			{
				this.Current = null;
			}
			else if (this.Current is null || !ReferenceEquals(this.Current.Topic, topic) && !SameTopic(this.Current.Topic, topic))
			{
				this.Current = this.CreateViewModel(topic);
			}
		}

		private static Boolean SameTopic(HelpTopic a, HelpTopic b)
		{
			return a.Kind == b.Kind && String.Equals(a.Name, b.Name, StringComparison.Ordinal) && String.Equals(a.File, b.File, StringComparison.OrdinalIgnoreCase) && a.Line == b.Line
				&& a.Description.Count == b.Description.Count && String.Equals(a.Summary, b.Summary, StringComparison.Ordinal);
		}

		private static Boolean IsWord(String text)
		{
			foreach (Char c in text)
			{
				if (!(c == '_' || Char.IsLetterOrDigit(c)))
				{
					return false;
				}
			}

			return true;
		}

		private HelpTopicViewModel CreateViewModel(HelpTopic topic)
		{
			return new HelpTopicViewModel(topic, this.OpenDocumentation, this.CopySignature, this.InsertCall);
		}

		private void OpenDocumentation(HelpTopic topic)
		{
			if (this._help.OpenDocumentation(topic) is null)
			{
				Uri address = SfallDocumentation.GetUri(topic) ?? SfallDocumentation.BaseUri;
				_ = this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Help"), Loc.Format("Str.Shell.Dlg.DocsNotFound", address.AbsoluteUri));
			}
		}

		private void CopySignature(HelpTopic topic)
		{
			this._shell.SetClipboardText(topic.SignatureText);
			this._workspace.ReportStatus(Loc.Format("Str.Panels.HelpCopied", topic.Name));
		}

		private void InsertCall(HelpTopic topic)
		{
			if (topic.InsertCode is { } code)
			{
				FunctionInserter.Insert(this._tracker.Editor, this._tracker.Document, code);
			}
		}

		private void OnCaretMoved(Object? sender, EventArgs e)
		{
			if (this.ShowResults)
			{
				return;
			}

			lock (this._gate)
			{
				if (this._disposed)
				{
					return;
				}

				Int32 generation = Interlocked.Increment(ref this._generation);
				this._timer?.Dispose();
				this._timer = new Timer(_ => this.Post(generation), null, this._delay, Timeout.InfiniteTimeSpan);
			}
		}

		private void OnHelpChanged(Object? sender, EventArgs e)
		{
			this.Post(Volatile.Read(ref this._generation));
		}

		private void Post(Int32 generation)
		{
			if (this._context is not null)
			{
				this._context.Post(_ => this.Apply(generation), null);
			}
			else
			{
				this.Apply(generation);
			}
		}

		private void Apply(Int32 generation)
		{
			if (this._disposed || generation != Volatile.Read(ref this._generation))
			{
				return;
			}

			this.Update();
		}

		private void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Current));
			if (this.Current is { } current)
			{
				this.Current = this.CreateViewModel(current.Topic);
			}
		}
	}
}