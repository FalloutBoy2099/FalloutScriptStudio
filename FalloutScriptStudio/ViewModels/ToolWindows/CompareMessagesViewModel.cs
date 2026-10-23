namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.MessageEditor;

	/// <summary>
	/// View model of "Compare Message Files": a reference message file (a language) and one or several target files (the other languages), the table of the
	/// messages by number with the status of each one against the reference, the filters per status, and the actions that follow: add the missing lines to a
	/// target, copy the numbers, export a report, list the lines as problems and open a line in the Message Editor.
	/// </summary>
	public sealed partial class CompareMessagesViewModel : ToolWindowViewModelBase
	{
		/// <summary>The comment written after the closing brace of the lines that "Add missing lines" adds when the option is on.</summary>
		public const String UntranslatedMarker = "# TODO: translate";

		/// <summary>The number of problems "Show as problems" lists at most.</summary>
		public const Int32 MaxProblems = 500;

		private readonly IMessageComparisonService _service;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly IShellService _shell;
		private readonly IMessageEditorService _editors;
		private readonly IProblemService _problems;
		private readonly RangeObservableCollection<CompareRowViewModel> _rows = [];
		private StatusKind _status;
		private Object?[] _statusArguments = Array.Empty<Object?>();
		private Boolean _refreshing;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ReferenceName))]
		[NotifyPropertyChangedFor(nameof(ReferenceInfo))]
		private String? _referencePath;

		[ObservableProperty]
		private LoadedMessageFile? _reference;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(NotesText))]
		[NotifyPropertyChangedFor(nameof(HasNotes))]
		[NotifyPropertyChangedFor(nameof(TargetPath))]
		[NotifyCanExecuteChangedFor(nameof(AddMissingCommand))]
		[NotifyCanExecuteChangedFor(nameof(RemoveTargetCommand))]
		[NotifyCanExecuteChangedFor(nameof(CopyNumbersCommand))]
		[NotifyCanExecuteChangedFor(nameof(ExportCommand))]
		[NotifyCanExecuteChangedFor(nameof(ShowAsProblemsCommand))]
		private CompareTargetViewModel? _selectedTarget;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(OpenRowCommand))]
		[NotifyCanExecuteChangedFor(nameof(OpenReferenceRowCommand))]
		private CompareRowViewModel? _selectedRow;

		[ObservableProperty]
		private Boolean _markUntranslated;

		[ObservableProperty]
		private Boolean _issuesOnly;

		[ObservableProperty]
		private String _filterText = String.Empty;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(AddMissingCommand))]
		[NotifyCanExecuteChangedFor(nameof(CompareCommand))]
		[NotifyCanExecuteChangedFor(nameof(ChooseReferenceCommand))]
		[NotifyCanExecuteChangedFor(nameof(AddTargetCommand))]
		private Boolean _isBusy;

		/// <summary>Initializes a new instance of the <see cref="CompareMessagesViewModel"/> class.</summary>
		/// <param name="service">Reads, compares and writes the files.</param>
		/// <param name="settings">Gives the encoding of the message files.</param>
		/// <param name="dialogs">Asks for files and confirmations.</param>
		/// <param name="shell">Gives access to the clipboard.</param>
		/// <param name="editors">Opens a line in the Message Editor.</param>
		/// <param name="problems">Receives the lines listed as problems.</param>
		public CompareMessagesViewModel(IMessageComparisonService service, ISettingsService settings, IDialogService dialogs, IShellService shell, IMessageEditorService editors, IProblemService problems)
		{
			ArgumentNullException.ThrowIfNull(service);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(shell);
			ArgumentNullException.ThrowIfNull(editors);
			ArgumentNullException.ThrowIfNull(problems);
			this._service = service;
			this._settings = settings;
			this._dialogs = dialogs;
			this._shell = shell;
			this._editors = editors;
			this._problems = problems;

			foreach (MessageComparisonStatus status in Enum.GetValues<MessageComparisonStatus>())
			{
				CompareFilterViewModel filter = new(status, status != MessageComparisonStatus.Ok);
				filter.Toggled += this.OnFilterToggled;
				this.Filters.Add(filter);
			}
		}

		private enum StatusKind
		{
			Idle,
			Loading,
			Compared,
			NoTargets,
			Added,
			AddedNothing,
			AddedPartly,
			NumbersCopied,
			Exported,
			Listed,
			ListedPartly,
			Failed,
		}

		/// <summary>Gets the lines of the table: the lines of the selected target that the filters let through.</summary>
		public RangeObservableCollection<CompareRowViewModel> Rows => this._rows;

		/// <summary>Gets the toggles of the statuses.</summary>
		public ObservableCollection<CompareFilterViewModel> Filters { get; } = [];

		/// <summary>Gets the target files.</summary>
		public ObservableCollection<CompareTargetViewModel> Targets { get; } = [];

		/// <summary>Gets the name of the reference file, or an empty text.</summary>
		public String ReferenceName => this.ReferencePath is null ? String.Empty : Path.GetFileName(this.ReferencePath);

		/// <summary>Gets the language and the encoding of the reference file, in the language of the interface.</summary>
		public String ReferenceInfo
		{
			get
			{
				if (this.Reference is null)
				{
					return String.Empty;
				}

				return Loc.Format("Str.ToolWindows.Compare.ReferenceInfo", this.Reference.Language ?? "?", this.Reference.EncodingName);
			}
		}

		/// <summary>Gets the path of the selected target, or an empty text.</summary>
		public String TargetPath => this.SelectedTarget?.Path ?? String.Empty;

		/// <summary>Gets the notes about the selected comparison (the encodings, the duplicate numbers...), one per line, in the language of the interface.</summary>
		public String NotesText => this.SelectedTarget is null ? String.Empty : String.Join(Environment.NewLine, this.SelectedTarget.Comparison.Notes.Select(ComparisonTexts.NoteText));

		/// <summary>Gets a value indicating whether there is a note to show.</summary>
		public Boolean HasNotes => this.SelectedTarget is { } target && target.Comparison.Notes.Count > 0;

		/// <summary>Gets the sentence that counts the lines of the selected comparison, in the language of the interface.</summary>
		public String SummaryText
		{
			get
			{
				if (this.SelectedTarget is not { File: not null } target)
				{
					return String.Empty;
				}

				MessageComparison c = target.Comparison;
				return Loc.Format(
					"Str.ToolWindows.Compare.Summary",
					c.Rows.Count,
					c.Count(MessageComparisonStatus.MissingInTarget),
					c.Count(MessageComparisonStatus.ExtraInTarget),
					c.Count(MessageComparisonStatus.SameText),
					c.Count(MessageComparisonStatus.PlaceholdersOnly),
					c.Count(MessageComparisonStatus.EmptyInTarget),
					c.Count(MessageComparisonStatus.PlayerOrDescription),
					c.Count(MessageComparisonStatus.Ok),
					c.IssueCount);
			}
		}

		/// <summary>Gets the line under the table, in the language of the interface; a live region.</summary>
		public String StatusText => this._status switch
		{
			StatusKind.Loading => Loc.Get("Str.ToolWindows.Compare.Status.Loading"),
			StatusKind.Compared => Loc.Format("Str.ToolWindows.Compare.Shown", this._statusArguments),
			StatusKind.NoTargets => Loc.Get("Str.ToolWindows.Compare.NoTargets"),
			StatusKind.Added => Loc.Format("Str.ToolWindows.Compare.Added", this._statusArguments),
			StatusKind.AddedNothing => Loc.Get("Str.ToolWindows.Compare.AddedNothing"),
			StatusKind.AddedPartly => Loc.Format("Str.ToolWindows.Compare.AddedPartly", this._statusArguments),
			StatusKind.NumbersCopied => Loc.Format("Str.ToolWindows.Compare.NumbersCopied", this._statusArguments),
			StatusKind.Exported => Loc.Format("Str.ToolWindows.Compare.Exported", this._statusArguments),
			StatusKind.Listed => Loc.Format("Str.ToolWindows.Compare.Listed", this._statusArguments),
			StatusKind.ListedPartly => Loc.Format("Str.ToolWindows.Compare.ListedPartly", this._statusArguments),
			StatusKind.Failed => Loc.Format("Str.ToolWindows.Compare.Failed", this._statusArguments),
			_ => Loc.Get("Str.ToolWindows.Compare.Hint"),
		};

		/// <summary>Opens the window on a reference file: reads it, offers the files of the other languages as targets and compares them.</summary>
		/// <param name="request">What to show.</param>
		/// <returns>A task that completes when the comparison is shown.</returns>
		public async Task InitializeAsync(CompareMessagesRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			this.ReferencePath = Path.GetFullPath(request.ReferencePath);
			this.Targets.Clear();
			foreach (MessageLanguageFile sibling in this._service.FindSiblings(this.ReferencePath))
			{
				this.Targets.Add(new CompareTargetViewModel(sibling.Path, sibling.Language));
			}

			await this.CompareAllAsync();
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			base.OnLanguageChanged();
			foreach (CompareFilterViewModel filter in this.Filters)
			{
				filter.RefreshTexts();
			}

			foreach (CompareTargetViewModel target in this.Targets)
			{
				target.RefreshTexts();
			}

			this.OnPropertyChanged(nameof(this.StatusText));
			this.OnPropertyChanged(nameof(this.SummaryText));
			this.OnPropertyChanged(nameof(this.NotesText));
			this.OnPropertyChanged(nameof(this.ReferenceInfo));
			this.RefreshRows();
		}

		partial void OnSelectedTargetChanged(CompareTargetViewModel? value)
		{
			this.RefreshRows();
		}

		partial void OnIssuesOnlyChanged(Boolean value)
		{
			this.RefreshRows();
		}

		partial void OnFilterTextChanged(String value)
		{
			this.RefreshRows();
		}

		/// <summary>Reads the files again and compares them.</summary>
		[RelayCommand(CanExecute = nameof(CanRun))]
		private Task CompareAsync()
		{
			return this.CompareAllAsync();
		}

		/// <summary>Asks for another reference file and compares again; the files of the other languages of the new reference are offered as targets.</summary>
		[RelayCommand(CanExecute = nameof(CanRun))]
		private async Task ChooseReferenceAsync()
		{
			String? path = this._dialogs.PickOpenFile(Loc.Get("Str.ToolWindows.Compare.ChooseReferenceTitle"), MessageEditorViewModel.FileFilter, Path.GetDirectoryName(this.ReferencePath));
			if (path is not null)
			{
				await this.InitializeAsync(new CompareMessagesRequest(path));
			}
		}

		/// <summary>Asks for one or several target files and adds them to the targets.</summary>
		[RelayCommand(CanExecute = nameof(CanRun))]
		private async Task AddTargetAsync()
		{
			IReadOnlyList<String> paths = this._dialogs.PickOpenFiles(Loc.Get("Str.ToolWindows.Compare.AddTargetTitle"), MessageEditorViewModel.FileFilter, Path.GetDirectoryName(this.TargetPath.Length > 0 ? this.TargetPath : this.ReferencePath));
			CompareTargetViewModel? first = null;
			foreach (String path in paths)
			{
				String full = Path.GetFullPath(path);
				if (this.Targets.Any(target => String.Equals(target.Path, full, StringComparison.OrdinalIgnoreCase)) || String.Equals(full, this.ReferencePath, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				CompareTargetViewModel added = new(full, MessageLanguages.DetectLanguage(full));
				this.Targets.Add(added);
				first ??= added;
			}

			if (first is not null)
			{
				await this.CompareAllAsync(first);
			}
		}

		/// <summary>Removes the selected target from the comparison (the file is not touched).</summary>
		[RelayCommand(CanExecute = nameof(CanRemoveTarget))]
		private void RemoveTarget()
		{
			if (this.SelectedTarget is { } target)
			{
				Int32 index = this.Targets.IndexOf(target);
				this.Targets.Remove(target);
				this.SelectedTarget = this.Targets.Count == 0 ? null : this.Targets[Math.Min(index, this.Targets.Count - 1)];
				if (this.Targets.Count == 0)
				{
					this.SetStatus(StatusKind.NoTargets);
				}
			}
		}

		/// <summary>Appends the messages the target lacks to the target file.</summary>
		[RelayCommand(CanExecute = nameof(CanAddMissing))]
		private async Task AddMissingAsync()
		{
			if (this.SelectedTarget is not { File: { } file } target)
			{
				return;
			}

			List<MessageEntry> missing = target.Comparison.Rows.Where(row => row.Status == MessageComparisonStatus.MissingInTarget && row.Reference is not null).Select(row => row.Reference!).ToList();
			if (missing.Count == 0)
			{
				return;
			}

			String name = Path.GetFileName(target.Path);
			if (!await this._dialogs.ConfirmAsync(Loc.Get("Str.ToolWindows.Compare.AddMissingTitle"), Loc.Format("Str.ToolWindows.Compare.ConfirmAdd", missing.Count, name), Loc.Get("Str.Common.Yes"), Loc.Get("Str.Common.No")))
			{
				return;
			}

			this.IsBusy = true;
			try
			{
				MessageAppendResult result = await this._service.AddMissingAsync(file, missing, this.MarkUntranslated ? UntranslatedMarker : null);
				if (result.NotEncodable.Count > 0)
				{
					this.SetStatus(StatusKind.AddedPartly, result.Added.Count, name, result.NotEncodable.Count);
				}
				else if (result.Added.Count == 0)
				{
					this.SetStatus(StatusKind.AddedNothing);
				}
				else
				{
					this.SetStatus(StatusKind.Added, result.Added.Count, name);
				}

				await this.CompareAllAsync(target, keepStatus: true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this.SetStatus(StatusKind.Failed, ex.Message);
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Puts the numbers of the lines that are listed on the clipboard, separated by commas.</summary>
		[RelayCommand(CanExecute = nameof(HasListedRows))]
		private void CopyNumbers()
		{
			String numbers = MessageComparisonReport.JoinNumbers(this._rows.Select(row => row.Row));
			this._shell.SetClipboardText(numbers);
			this.SetStatus(StatusKind.NumbersCopied, this._rows.Count);
		}

		/// <summary>Writes the lines that are listed to a CSV or text file.</summary>
		[RelayCommand(CanExecute = nameof(HasListedRows))]
		private async Task ExportAsync()
		{
			String? directory = Path.GetDirectoryName(this.TargetPath);
			String baseName = Path.GetFileNameWithoutExtension(this.TargetPath);
			String? path = this._dialogs.PickSaveFile(
				Loc.Get("Str.ToolWindows.Compare.ExportTitle"),
				Loc.Get("Str.ToolWindows.Compare.FilterCsv") + "|*.csv|" + Loc.Get("Str.ToolWindows.Compare.FilterText") + "|*.txt",
				directory,
				baseName + ".csv");
			if (path is null)
			{
				return;
			}

			MessageReportLabels labels = ComparisonTexts.CreateReportLabels();
			IEnumerable<MessageComparisonRow> rows = this._rows.Select(row => row.Row).ToList();
			String content = String.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase) ? MessageComparisonReport.ToText(rows, labels) : MessageComparisonReport.ToCsv(rows, labels);
			try
			{
				await this._service.WriteReportAsync(path, content);
				this.SetStatus(StatusKind.Exported, this._rows.Count, Path.GetFileName(path));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this.SetStatus(StatusKind.Failed, ex.Message);
			}
		}

		/// <summary>Lists the lines that are shown, except the ones that are fine, in the Errors tab as problems (at most <see cref="MaxProblems"/>).</summary>
		[RelayCommand(CanExecute = nameof(HasListedRows))]
		private void ShowAsProblems()
		{
			if (this.SelectedTarget is not { File: { } file } target || this.Reference is null)
			{
				return;
			}

			List<Problem> found = [];
			Int32 rows = 0;
			Boolean partly = false;
			foreach (CompareRowViewModel row in this._rows)
			{
				List<Problem> mine = ProblemFactory.MessageComparison(row.Row, this.Reference.Path, file.Path, ComparisonTexts.IssueNames).ToList();
				if (mine.Count == 0)
				{
					continue;
				}

				if (rows >= MaxProblems)
				{
					partly = true;
					break;
				}

				rows++;
				found.AddRange(mine);
			}

			this._problems.Remove(problem => problem.Origin == ProblemOrigins.MessageComparison);
			this._problems.ReportRange(found);
			this.SetStatus(partly ? StatusKind.ListedPartly : StatusKind.Listed, rows, Path.GetFileName(target.Path));
		}

		/// <summary>Opens the selected line in the Message Editor, on the message of the target (on the message of the reference when the target lacks it).</summary>
		[RelayCommand(CanExecute = nameof(HasSelectedRow))]
		private void OpenRow()
		{
			if (this.SelectedRow is not { } row || this.SelectedTarget is not { } target)
			{
				return;
			}

			if (row.Row.Target is not null)
			{
				this._editors.Open(new MessageEditorOpenOptions { FilePath = target.Path, SelectMessageId = row.Number });
			}
			else if (this.ReferencePath is not null)
			{
				this._editors.Open(new MessageEditorOpenOptions { FilePath = this.ReferencePath, SelectMessageId = row.Number });
			}
		}

		/// <summary>Opens the selected line in the Message Editor, on the message of the reference.</summary>
		[RelayCommand(CanExecute = nameof(HasSelectedReferenceRow))]
		private void OpenReferenceRow()
		{
			if (this.SelectedRow is { } row && this.ReferencePath is not null)
			{
				this._editors.Open(new MessageEditorOpenOptions { FilePath = this.ReferencePath, SelectMessageId = row.Number });
			}
		}

		/// <summary>Closes the window.</summary>
		[RelayCommand]
		private void Close()
		{
			this.RequestClose(false);
		}

		private Boolean CanRun()
		{
			return !this.IsBusy;
		}

		private Boolean CanRemoveTarget()
		{
			return this.SelectedTarget is not null;
		}

		private Boolean CanAddMissing()
		{
			return !this.IsBusy && this.SelectedTarget is { File: not null } target && target.Comparison.Count(MessageComparisonStatus.MissingInTarget) > 0;
		}

		private Boolean HasListedRows()
		{
			return this._rows.Count > 0;
		}

		private Boolean HasSelectedRow()
		{
			return this.SelectedRow is not null && this.SelectedTarget is not null;
		}

		private Boolean HasSelectedReferenceRow()
		{
			return this.SelectedRow?.Row.Reference is not null;
		}

		private void OnFilterToggled(Object? sender, EventArgs e)
		{
			this.RefreshRows();
		}

		private void SetStatus(StatusKind kind, params Object?[] arguments)
		{
			this._status = kind;
			this._statusArguments = arguments;
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private async Task CompareAllAsync(CompareTargetViewModel? select = null, Boolean keepStatus = false)
		{
			if (this.ReferencePath is null)
			{
				return;
			}

			this.IsBusy = true;
			if (!keepStatus)
			{
				this.SetStatus(StatusKind.Loading);
			}

			try
			{
				System.Text.Encoding encoding = this._settings.Effective.ToMessageFileOptions().Encoding;
				LoadedMessageFile? reference = null;
				try
				{
					reference = await this._service.LoadAsync(this.ReferencePath, encoding, true);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this.SetStatus(StatusKind.Failed, ex.Message);
				}

				this.Reference = reference;
				this.OnPropertyChanged(nameof(this.ReferenceInfo));
				foreach (CompareTargetViewModel target in this.Targets)
				{
					if (reference is null)
					{
						target.SetError(Loc.Get("Str.ToolWindows.Compare.NoReference"));
						continue;
					}

					try
					{
						LoadedMessageFile loaded = await this._service.LoadAsync(target.Path, encoding, false);
						MessageComparison comparison = await Task.Run(() => this._service.Compare(reference, loaded));
						target.SetResult(loaded, comparison);
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						target.SetError(ex.Message);
					}
				}

				CompareTargetViewModel? chosen = select ?? this.SelectedTarget;
				this.SelectedTarget = chosen is not null && this.Targets.Contains(chosen) ? chosen : this.Targets.FirstOrDefault();
				this.RefreshRows();
				if (!keepStatus && reference is not null)
				{
					this.SetStatus(this.Targets.Count == 0 ? StatusKind.NoTargets : StatusKind.Compared, this._rows.Count);
				}
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		private void RefreshRows()
		{
			if (this._refreshing)
			{
				return;
			}

			this._refreshing = true;
			try
			{
				MessageComparison comparison = this.SelectedTarget?.Comparison ?? MessageComparison.Empty;
				foreach (CompareFilterViewModel filter in this.Filters)
				{
					filter.SetCount(comparison.Count(filter.Status));
				}

				HashSet<MessageComparisonStatus> shown = this.Filters.Where(filter => filter.IsChecked).Select(filter => filter.Status).ToHashSet();
				String text = this.FilterText.Trim();
				Int32? number = Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 parsed) ? parsed : null;
				IEnumerable<MessageComparisonRow> rows = comparison.Rows.Where(row => shown.Contains(row.Status) && (!this.IssuesOnly || row.Issues != MessageComparisonIssues.None));
				if (text.Length > 0)
				{
					rows = rows.Where(row => (number is not null && row.Number == number) || row.ReferenceText.Contains(text, StringComparison.OrdinalIgnoreCase) || row.TargetText.Contains(text, StringComparison.OrdinalIgnoreCase));
				}

				Int32 selected = this.SelectedRow?.Number ?? -1;
				List<CompareRowViewModel> list = rows.Select(row => new CompareRowViewModel(row)).ToList();
				this._rows.ReplaceAll(list);
				this.SelectedRow = list.FirstOrDefault(row => row.Number == selected);
			}
			finally
			{
				this._refreshing = false;
			}

			this.OnPropertyChanged(nameof(this.SummaryText));
			this.OnPropertyChanged(nameof(this.NotesText));
			this.OnPropertyChanged(nameof(this.HasNotes));
			this.AddMissingCommand.NotifyCanExecuteChanged();
			this.CopyNumbersCommand.NotifyCanExecuteChanged();
			this.ExportCommand.NotifyCanExecuteChanged();
			this.ShowAsProblemsCommand.NotifyCanExecuteChanged();
			if (this._status == StatusKind.Compared)
			{
				this.SetStatus(StatusKind.Compared, this._rows.Count);
			}
		}
	}
}