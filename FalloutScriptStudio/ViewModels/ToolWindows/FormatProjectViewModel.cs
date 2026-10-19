namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Formatting;

	/// <summary>
	/// View model of "Format Project": the scope and the masks, the preview list of the files that formatting would change (with a check
	/// box each and the number of changed lines), the diff of the selected file, and the application of the formatting. The work is an
	/// operation of the status bar that can be cancelled.
	/// </summary>
	public sealed partial class FormatProjectViewModel : ToolWindowViewModelBase
	{
		private readonly IProjectFormatService _service;
		private readonly IOperationTracker _operations;
		private readonly ISettingsService _settings;
		private readonly IWorkspace _workspace;
		private CancellationTokenSource? _cancellation;
		private Int32 _diffVersion;
		private StatusKind _status;
		private Object?[] _statusArguments = Array.Empty<Object?>();

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsOpenDocumentsScope))]
		[NotifyPropertyChangedFor(nameof(AreMasksEnabled))]
		[NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
		private Boolean _isProjectScope;

		[ObservableProperty]
		private String _masks = ProjectFormatRequest.DefaultMasks;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
		[NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
		[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
		[NotifyPropertyChangedFor(nameof(AreMasksEnabled))]
		[NotifyPropertyChangedFor(nameof(IsIdle))]
		private Boolean _isBusy;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsIndeterminate))]
		private Double _progress = -1;

		[ObservableProperty]
		private FormatItemViewModel? _selectedItem;

		[ObservableProperty]
		private String _diffText = String.Empty;

		/// <summary>Initializes a new instance of the <see cref="FormatProjectViewModel"/> class.</summary>
		/// <param name="service">Previews and applies the formatting.</param>
		/// <param name="operations">Shows the work in the status bar and lets the user cancel it.</param>
		/// <param name="settings">Gives the project folder.</param>
		/// <param name="workspace">Gives the open documents and the status bar.</param>
		public FormatProjectViewModel(IProjectFormatService service, IOperationTracker operations, ISettingsService settings, IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(service);
			ArgumentNullException.ThrowIfNull(operations);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(workspace);
			this._service = service;
			this._operations = operations;
			this._settings = settings;
			this._workspace = workspace;
			this.Items.CollectionChanged += (_, _) => this.UpdateSelectionCount();
			this.HasProjectFolder = !String.IsNullOrWhiteSpace(this.GetProjectFolder());
			this._isProjectScope = this.HasProjectFolder;
		}

		private enum StatusKind
		{
			Idle,
			Previewing,
			PreviewDone,
			PreviewCancelled,
			Applying,
			Applied,
			AppliedCancelled,
			OperationRunning,
		}

		/// <summary>Gets the files that formatting would change.</summary>
		public ObservableCollection<FormatItemViewModel> Items { get; } = [];

		/// <summary>Gets a value indicating whether a project folder is open (the scope "project folder" needs one).</summary>
		public Boolean HasProjectFolder { get; }

		/// <summary>Gets or sets a value indicating whether the scope is the open scripts and headers.</summary>
		public Boolean IsOpenDocumentsScope
		{
			get => !this.IsProjectScope;
			set => this.IsProjectScope = !value && this.HasProjectFolder;
		}

		/// <summary>Gets a value indicating whether the masks can be edited.</summary>
		public Boolean AreMasksEnabled => this.IsProjectScope && !this.IsBusy;

		/// <summary>Gets a value indicating whether nothing runs.</summary>
		public Boolean IsIdle => !this.IsBusy;

		/// <summary>Gets a value indicating whether the progress bar has no count to show (the files are being listed).</summary>
		public Boolean IsIndeterminate => this.Progress < 0;

		/// <summary>Gets the number of files that are checked.</summary>
		public Int32 SelectedCount => this.Items.Count(item => item.IsSelected);

		/// <summary>Gets the line under the list, in the current language.</summary>
		public String StatusText => this._status switch
		{
			StatusKind.Previewing => Loc.Format("Str.ToolWindows.Format.Previewing", this._statusArguments),
			StatusKind.PreviewDone => this.Items.Count == 0 ? Loc.Format("Str.ToolWindows.Format.NothingToDo", this._statusArguments) : Loc.Format("Str.ToolWindows.Format.PreviewDone", this._statusArguments),
			StatusKind.PreviewCancelled => Loc.Format("Str.ToolWindows.Format.PreviewCancelled", this._statusArguments),
			StatusKind.Applying => Loc.Format("Str.ToolWindows.Format.Applying", this._statusArguments),
			StatusKind.Applied => Loc.Format("Str.ToolWindows.Format.Applied", this._statusArguments),
			StatusKind.AppliedCancelled => Loc.Format("Str.ToolWindows.Format.AppliedCancelled", this._statusArguments),
			StatusKind.OperationRunning => Loc.Get("Str.ToolWindows.Format.OperationRunning"),
			_ => this.HasProjectFolder ? Loc.Get("Str.ToolWindows.Format.Hint") : Loc.Get("Str.ToolWindows.Format.HintNoProject"),
		};

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			base.OnLanguageChanged();
			this.OnPropertyChanged(nameof(this.StatusText));
			foreach (FormatItemViewModel item in this.Items)
			{
				item.RefreshTexts();
			}
		}

		/// <summary>Looks at the files of the scope and lists the ones that formatting would change.</summary>
		[RelayCommand(CanExecute = nameof(CanStart))]
		private async Task PreviewAsync()
		{
			using IOperation? operation = this._operations.TryBegin(OperationKind.FormatProject, null, 0);
			if (operation is null)
			{
				this.SetStatus(StatusKind.OperationRunning);
				return;
			}

			using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
			this._cancellation = cancellation;
			this.IsBusy = true;
			this.Progress = -1;
			this.SetStatus(StatusKind.Previewing, 0, 0);
			this.ClearItems();
			try
			{
				ProjectFormatRequest request = new(this.IsProjectScope ? FormatScope.ProjectFolder : FormatScope.OpenDocuments, this.Masks);
				Progress<FormatProgress> progress = new(report => this.OnProgress(operation, report, StatusKind.Previewing));
				ProjectFormatPreview preview = await this._service.PreviewAsync(request, progress, cancellation.Token);
				foreach (FormatFileChange change in preview.Changes)
				{
					this.Items.Add(new FormatItemViewModel(change));
				}

				this.WatchItems();
				this.SetStatus(preview.Cancelled ? StatusKind.PreviewCancelled : StatusKind.PreviewDone, preview.Changes.Count, preview.Unchanged, preview.Unreadable);
				this.SelectedItem = this.Items.FirstOrDefault();
			}
			finally
			{
				this._cancellation = null;
				this.IsBusy = false;
				this.Progress = -1;
			}
		}

		/// <summary>Formats the files that are checked.</summary>
		[RelayCommand(CanExecute = nameof(CanApply))]
		private async Task ApplyAsync()
		{
			FormatItemViewModel[] chosen = this.Items.Where(item => item.IsSelected).ToArray();
			using IOperation? operation = this._operations.TryBegin(OperationKind.FormatProject, null, chosen.Length);
			if (operation is null)
			{
				this.SetStatus(StatusKind.OperationRunning);
				return;
			}

			using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
			this._cancellation = cancellation;
			this.IsBusy = true;
			this.Progress = 0;
			this.SetStatus(StatusKind.Applying, 0, chosen.Length);
			try
			{
				Progress<FormatProgress> progress = new(report => this.OnProgress(operation, report, StatusKind.Applying));
				ProjectFormatResult result = await this._service.ApplyAsync(chosen.Select(item => item.Change).ToList(), progress, cancellation.Token);
				this.ClearItems();
				this.SetStatus(result.Cancelled ? StatusKind.AppliedCancelled : StatusKind.Applied, result.Formatted, result.Skipped);
				this._workspace.ReportStatus(this.StatusText);
			}
			finally
			{
				this._cancellation = null;
				this.IsBusy = false;
				this.Progress = -1;
			}
		}

		/// <summary>Stops the preview or the formatting in progress.</summary>
		[RelayCommand(CanExecute = nameof(IsBusy))]
		private void Cancel()
		{
			this._cancellation?.Cancel();
		}

		[RelayCommand]
		private void SelectAll()
		{
			foreach (FormatItemViewModel item in this.Items)
			{
				item.IsSelected = true;
			}
		}

		[RelayCommand]
		private void SelectNone()
		{
			foreach (FormatItemViewModel item in this.Items)
			{
				item.IsSelected = false;
			}
		}

		[RelayCommand]
		private void Close()
		{
			this._cancellation?.Cancel();
			this.RequestClose(true);
		}

		partial void OnSelectedItemChanged(FormatItemViewModel? value)
		{
			_ = this.LoadDiffAsync(value);
		}

		private Boolean CanStart()
		{
			return !this.IsBusy && (!this.IsProjectScope || this.HasProjectFolder);
		}

		private Boolean CanApply()
		{
			return !this.IsBusy && this.SelectedCount > 0;
		}

		private String? GetProjectFolder()
		{
			return this._settings.Effective.ProjectFolder ?? this._settings.Settings.Paths.SolutionFolder;
		}

		private void OnProgress(IOperation operation, FormatProgress report, StatusKind kind)
		{
			operation.Report(report.Name.Length > 0 ? System.IO.Path.GetFileName(report.Name) : String.Empty, report.Index, report.Total);
			this.Progress = report.Total > 0 ? Math.Clamp((Double)report.Index / report.Total, 0, 1) : -1;
			this.SetStatus(kind, report.Index, report.Total);
		}

		private void SetStatus(StatusKind kind, params Object?[] arguments)
		{
			this._status = kind;
			this._statusArguments = arguments;
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private void ClearItems()
		{
			this.SelectedItem = null;
			foreach (FormatItemViewModel item in this.Items)
			{
				item.PropertyChanged -= this.OnItemChanged;
			}

			this.Items.Clear();
			this.DiffText = String.Empty;
		}

		private void WatchItems()
		{
			foreach (FormatItemViewModel item in this.Items)
			{
				item.PropertyChanged += this.OnItemChanged;
			}

			this.UpdateSelectionCount();
		}

		private void OnItemChanged(Object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FormatItemViewModel.IsSelected))
			{
				this.UpdateSelectionCount();
			}
		}

		private void UpdateSelectionCount()
		{
			this.OnPropertyChanged(nameof(this.SelectedCount));
			this.ApplyCommand.NotifyCanExecuteChanged();
		}

		private async Task LoadDiffAsync(FormatItemViewModel? item)
		{
			Int32 version = ++this._diffVersion;
			if (item is null)
			{
				this.DiffText = String.Empty;
				return;
			}

			String diff;
			try
			{
				diff = await this._service.GetDiffAsync(item.Change, CancellationToken.None);
			}
			catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
			{
				diff = ex.Message;
			}

			if (version == this._diffVersion)
			{
				this.DiffText = diff;
			}
		}
	}
}