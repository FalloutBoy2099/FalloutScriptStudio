namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;

	/// <summary>The output panel at the bottom of the main window: parser log, build log and the list of errors and search results.</summary>
	public sealed partial class OutputPanelViewModel : ObservableObject, IOutputSink
	{
		private IWorkspace? _workspace;
		private readonly ISettingsService _settings;
		private readonly IShellService _shell;
		private DocumentViewModel? _document;
		private IReadOnlyList<BuildError>? _aggregatedErrors;

		/// <summary>Initializes the panel.</summary>
		public OutputPanelViewModel(ISettingsService settings, IShellService shell)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(shell);
			this._settings = settings;
			this._shell = shell;
			this.Tabs = new ObservableCollection<OutputTabViewModel>
			{
				new ParserLogTabViewModel(this),
				new BuildLogTabViewModel(this),
				new ErrorsTabViewModel(this),
			};
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.RebuildErrors());
		}

		/// <summary>Connects the panel to the workspace whose active document it shows. Called once by the main view model.</summary>
		public void Attach(IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			this._workspace.ActiveDocumentChanged += (_, _) => this.Follow(this._workspace.ActiveDocument);
			this.Follow(this._workspace.ActiveDocument);
		}

		/// <summary>Raised when the panel should be made visible (an operation put something on it).</summary>
		public event EventHandler? ShowRequested;

		/// <summary>
		/// Gets the tabs: Parser, Build and Errors (at the indexes of <see cref="OutputTab"/>), then one closable tab for every search
		/// and every "find references".
		/// </summary>
		public ObservableCollection<OutputTabViewModel> Tabs { get; }

		/// <summary>Gets the rows of the Errors tab.</summary>
		public ObservableCollection<ErrorItemViewModel> Errors { get; } = new ObservableCollection<ErrorItemViewModel>();

		/// <summary>The text of the Parser tab.</summary>
		[ObservableProperty]
		public partial String ParserText { get; set; } = String.Empty;

		/// <summary>The text of the Build tab.</summary>
		[ObservableProperty]
		public partial String BuildText { get; set; } = String.Empty;

		/// <summary>The index of the selected tab (see <see cref="OutputTab"/>).</summary>
		[ObservableProperty]
		public partial Int32 SelectedTab { get; set; }

		/// <summary>The selected row of the Errors tab.</summary>
		[ObservableProperty]
		public partial ErrorItemViewModel? SelectedError { get; set; }

		/// <summary>The last short status message.</summary>
		[ObservableProperty]
		public partial String StatusMessage { get; set; } = String.Empty;

		/// <summary>True when the Errors tab refreshes itself whenever the active document or its logs change.</summary>
		[ObservableProperty]
		public partial Boolean AutoRefresh { get; set; } = true;

		/// <summary>True when parser entries are listed in the Errors tab.</summary>
		[ObservableProperty]
		public partial Boolean ShowParserEntries { get; set; } = true;

		/// <summary>True when build entries are listed in the Errors tab.</summary>
		[ObservableProperty]
		public partial Boolean ShowBuildEntries { get; set; } = true;

		/// <summary>The number of rows in the Errors tab, for the tab header.</summary>
		public Int32 ErrorCount => this.Errors.Count;

		/// <summary>Rebuilds the logs and the Errors tab from the active document.</summary>
		[RelayCommand]
		public void Refresh()
		{
			this._aggregatedErrors = null;
			this.LoadTexts();
			this.RebuildErrors();
		}

		/// <summary>Clears the logs of the active document and the Errors tab.</summary>
		[RelayCommand]
		public void ClearAll()
		{
			this._aggregatedErrors = null;
			this.Errors.Clear();
			this.OnPropertyChanged(nameof(this.ErrorCount));
			if (this._document is not null)
			{
				this._document.BuildErrors = Array.Empty<BuildError>();
				this._document.ParserErrors = Array.Empty<BuildError>();
			}
		}

		/// <summary>Copies the message of the selected row to the clipboard.</summary>
		[RelayCommand]
		public void CopySelected()
		{
			if (this.SelectedError is not null)
			{
				this._shell.SetClipboardText(this.SelectedError.Message);
			}
		}

		/// <summary>Copies the message of a row to the clipboard.</summary>
		/// <param name="item">The row.</param>
		public void CopyError(ErrorItemViewModel? item)
		{
			if (item is not null)
			{
				this._shell.SetClipboardText(item.Message);
			}
		}

		/// <summary>Jumps to the place an Errors row points to.</summary>
		[RelayCommand]
		public async Task GoToErrorAsync(ErrorItemViewModel? item)
		{
			ErrorItemViewModel? target = item ?? this.SelectedError;
			if (target is null || !target.CanNavigate)
			{
				return;
			}

			if (this._workspace is null)
			{
				return;
			}

			await this._workspace.GoToAsync(target.FullPath.Length == 0 ? null : target.FullPath, target.Line, target.Error.Column, target.Error.Length);
		}

		public void ShowSearchResults(IReadOnlyList<BuildError> results, String titleKey, String? titleArgument = null)
		{
			ArgumentNullException.ThrowIfNull(results);
			ArgumentException.ThrowIfNullOrEmpty(titleKey);

			ResultsTabViewModel tab = new ResultsTabViewModel(this, titleKey, titleArgument, results);
			this.Tabs.Add(tab);
			this.SelectedTab = this.Tabs.Count - 1;
			this.StatusMessage = Loc.Format("Str.Shell.Output.SearchStatus", tab.Title, results.Count);
			this.ShowRequested?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Closes a search or references tab and selects the tab before it.</summary>
		/// <param name="tab">The tab.</param>
		public void CloseResultsTab(ResultsTabViewModel tab)
		{
			ArgumentNullException.ThrowIfNull(tab);

			Int32 index = this.Tabs.IndexOf(tab);
			if (index <= (Int32)OutputTab.Errors)
			{
				return;
			}

			Boolean wasSelected = this.SelectedTab == index;
			this.Tabs.RemoveAt(index);
			if (wasSelected)
			{
				this.SelectedTab = index - 1;
			}
			else if (this.SelectedTab > index)
			{
				this.SelectedTab--;
			}
		}

		public void ShowBuildErrors(IReadOnlyList<BuildError> errors)
		{
			ArgumentNullException.ThrowIfNull(errors);
			this._aggregatedErrors = errors;
			this.RebuildErrors();
		}

		public void ReportStatus(String message)
		{
			this.StatusMessage = message;
		}

		public void SetBuildText(String text)
		{
			this.BuildText = text;
		}

		public void AppendBuildLine(String line)
		{
			this.BuildText = this.BuildText.Length == 0 ? line : this.BuildText + "\r\n" + line;
		}

		public void ShowTab(OutputTab tab)
		{
			this.SelectedTab = (Int32)tab;
			this.ShowRequested?.Invoke(this, EventArgs.Empty);
		}

		public void ClearErrors()
		{
			this._aggregatedErrors = null;
			this.Errors.Clear();
			this.OnPropertyChanged(nameof(this.ErrorCount));
		}

		partial void OnShowParserEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowBuildEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		private void Follow(DocumentViewModel? document)
		{
			if (this._document is not null)
			{
				this._document.PropertyChanged -= this.OnDocumentPropertyChanged;
			}

			this._document = document;
			if (document is not null)
			{
				document.PropertyChanged += this.OnDocumentPropertyChanged;
			}

			this.LoadTexts();
			if (this.AutoRefresh && document is not null && (document.ParserErrors.Count > 0 || document.BuildErrors.Count > 0))
			{
				this._aggregatedErrors = null;
				this.RebuildErrors();
			}
		}

		private void OnDocumentPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(DocumentViewModel.ParserLog):
				case nameof(DocumentViewModel.BuildLog):
					this.LoadTexts();
					break;
				case nameof(DocumentViewModel.ParserErrors):
				case nameof(DocumentViewModel.BuildErrors):
					if (this._aggregatedErrors is null)
					{
						this.RebuildErrors();
					}

					break;
			}
		}

		private void LoadTexts()
		{
			DocumentViewModel? document = this._document;
			this.ParserText = document?.ParserLog ?? String.Empty;
			if (document is not null && document.BuildLog.Length > 0)
			{
				this.BuildText = document.BuildLog;
			}
		}

		private void RebuildErrors()
		{
			this.Errors.Clear();
			if (this._aggregatedErrors is not null)
			{
				foreach (BuildError error in this._aggregatedErrors)
				{
					this.Errors.Add(new ErrorItemViewModel(error, Loc.Get("Str.Shell.Output.GroupBuild")));
				}

				this.OnPropertyChanged(nameof(this.ErrorCount));
				return;
			}

			DocumentViewModel? document = this._document;
			if (document is not null)
			{
				if (this._settings.Settings.Editor.EnableParser && this.ShowParserEntries)
				{
					foreach (BuildError error in document.ParserErrors)
					{
						this.Errors.Add(new ErrorItemViewModel(error, Loc.Get("Str.Shell.Output.GroupParser")));
					}
				}

				if (this.ShowBuildEntries)
				{
					foreach (BuildErrorType type in new[] { BuildErrorType.Error, BuildErrorType.Warning, BuildErrorType.Message })
					{
						foreach (BuildError error in document.BuildErrors.Where(e => e.Type == type))
						{
							this.Errors.Add(new ErrorItemViewModel(error, Loc.Get("Str.Shell.Output.GroupBuild")));
						}
					}
				}
			}

			this.OnPropertyChanged(nameof(this.ErrorCount));
		}
	}
}
