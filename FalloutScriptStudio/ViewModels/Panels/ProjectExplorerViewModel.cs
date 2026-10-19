namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Project;

	/// <summary>The "Solution" tab: the project folder as a tree of scripts, headers and message files.</summary>
	public sealed partial class ProjectExplorerViewModel : ObservableObject, IDisposable
	{
		private static readonly TimeSpan _defaultFilterDelay = TimeSpan.FromMilliseconds(250);

		private readonly ISettingsService _settings;
		private readonly IWorkspaceManager _workspace;
		private readonly IDialogService _dialogs;
		private readonly IShellService _shell;
		private String? _loadedFolder;
		private CancellationTokenSource? _filterCancellation;
		private IReadOnlyList<String>? _fileIndex;
		private String? _indexFolder;
		private Boolean _indexCompiled;

		/// <summary>Initializes the explorer and loads the project folder.</summary>
		public ProjectExplorerViewModel(ISettingsService settings, IWorkspaceManager workspace, IDialogService dialogs, IShellService shell)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(shell);
			this._settings = settings;
			this._workspace = workspace;
			this._dialogs = dialogs;
			this._shell = shell;
			this._settings.SettingsChanged += (_, _) =>
			{
				if (!String.Equals(this._loadedFolder, this._settings.Settings.Paths.SolutionFolder, StringComparison.OrdinalIgnoreCase))
				{
					this.Reload();
				}

				this.OnPropertyChanged(nameof(this.ProjectFolder));
			};
			this.Reload();
		}

		/// <summary>Gets or sets what the context menu item "Format Project" does (the main window opens the window of Format Project).</summary>
		public Func<Task>? FormatProjectHandler { get; set; }

		/// <summary>Gets or sets how long typing must pause before the file names are filtered (the filter text is debounced).</summary>
		public TimeSpan FilterDelay { get; set; } = _defaultFilterDelay;

		/// <summary>
		/// Gets the filter run that was started last (completed when none is pending). The folder walk of a filter runs on a
		/// worker thread and is cancelled by the next keystroke; its result is shown on the UI thread.
		/// </summary>
		public Task FilterTask { get; private set; } = Task.CompletedTask;

		/// <summary>Gets the top level nodes (the content of the project folder, or the search results).</summary>
		public ObservableCollection<ProjectNodeViewModel> Nodes { get; } = [];

		/// <summary>Gets the project folder, or <see langword="null"/> when none is set.</summary>
		public String? ProjectFolder => this._settings.Settings.Paths.SolutionFolder;

		/// <summary>The text that file names are filtered by.</summary>
		[ObservableProperty]
		public partial String FilterText { get; set; } = String.Empty;

		/// <summary>True when compiled scripts (<c>.int</c>) are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowCompiled { get; set; }

		/// <summary>True when the project folder exists and is listed.</summary>
		[ObservableProperty]
		public partial Boolean HasProject { get; set; }

		/// <summary>The selected node.</summary>
		[ObservableProperty]
		public partial ProjectNodeViewModel? SelectedNode { get; set; }

		/// <summary>Reads the project folder again.</summary>
		[RelayCommand]
		public void Reload()
		{
			this._fileIndex = null;
			this.Refresh();
		}

		private void Refresh()
		{
			this.CancelFilter();
			this.Nodes.Clear();
			String? folder = this.ProjectFolder;
			this._loadedFolder = folder;
			this.HasProject = folder is not null && Directory.Exists(folder);
			if (!this.HasProject || folder is null)
			{
				return;
			}

			ProjectFileFilter filter = new(this.ShowCompiled);
			if (this.FilterText.Trim().Length > 0)
			{
				this.StartFilter(folder, this.FilterText.Trim(), TimeSpan.Zero);
				return;
			}

			ProjectNodeViewModel root = new(folder, true, filter);
			root.EnsureLoaded();
			root.IsExpanded = true;
			this.Nodes.Add(root);
		}

		/// <summary>Asks for a project folder.</summary>
		[RelayCommand]
		public void ChooseFolder()
		{
			String? folder = this._dialogs.PickFolder(Loc.Get("Str.Panels.MsgSelectProjectFolder"), this.ProjectFolder);
			if (folder is not null)
			{
				this._settings.Update(settings => settings.Paths.SolutionFolder = folder);
			}
		}

		/// <summary>Opens a file in the editor.</summary>
		[RelayCommand]
		public async Task OpenAsync(ProjectNodeViewModel? node)
		{
			if (node is null || node.IsDirectory)
			{
				return;
			}

			await this._workspace.OpenAsync(node.FullPath, new OpenDocumentOptions { PromptIfAlreadyOpen = false });
		}

		/// <summary>Shows a file or folder in Explorer.</summary>
		[RelayCommand]
		public void Reveal(ProjectNodeViewModel? node)
		{
			if (node is null)
			{
				return;
			}

			if (node.IsDirectory)
			{
				this._shell.OpenFolder(node.FullPath);
			}
			else
			{
				this._shell.RevealInExplorer(node.FullPath);
			}
		}

		/// <summary>Copies the full path of a node to the clipboard.</summary>
		/// <summary>Opens "Format Project" from the context menu of the explorer.</summary>
		/// <returns>A task that completes when the window is closed.</returns>
		[RelayCommand]
		public async Task FormatProjectAsync()
		{
			if (this.FormatProjectHandler is { } handler)
			{
				await handler();
			}
		}

		[RelayCommand]
		public void CopyPath(ProjectNodeViewModel? node)
		{
			if (node is not null)
			{
				this._shell.SetClipboardText(node.FullPath);
			}
		}

		/// <summary>Cancels a pending filter run.</summary>
		public void Dispose()
		{
			this.CancelFilter();
		}

		partial void OnFilterTextChanged(String value)
		{
			String? folder = this.ProjectFolder;
			String text = value.Trim();
			if (text.Length == 0 || folder is null || !Directory.Exists(folder))
			{
				this.Refresh();
				return;
			}

			this.StartFilter(folder, text, this.FilterDelay);
		}

		partial void OnShowCompiledChanged(Boolean value)
		{
			this.Reload();
		}

		private void CancelFilter()
		{
			CancellationTokenSource? cancellation = this._filterCancellation;
			this._filterCancellation = null;
			cancellation?.Cancel();
			cancellation?.Dispose();
		}

		private void StartFilter(String folder, String text, TimeSpan delay)
		{
			this.CancelFilter();
			CancellationTokenSource cancellation = new();
			this._filterCancellation = cancellation;
			this.FilterTask = this.RunFilterAsync(folder, text, this.ShowCompiled, delay, cancellation.Token);
		}

		private async Task RunFilterAsync(String folder, String text, Boolean showCompiled, TimeSpan delay, CancellationToken cancellationToken)
		{
			try
			{
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken);
				}

				// The folder is walked once (off the UI thread) and the names are filtered in memory until Reload() or a new folder.
				IReadOnlyList<String>? index = this._fileIndex;
				ProjectFileFilter filter = new(showCompiled);
				if (index is null || this._indexCompiled != showCompiled || !String.Equals(this._indexFolder, folder, StringComparison.OrdinalIgnoreCase))
				{
					index = await Task.Run(() => filter.ListAll(folder, cancellationToken), cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					this._fileIndex = index;
					this._indexFolder = folder;
					this._indexCompiled = showCompiled;
				}

				IReadOnlyList<String> matches = ProjectFileFilter.FilterByName(index, text);
				this.Nodes.Clear();
				foreach (String file in matches)
				{
					this.Nodes.Add(new ProjectNodeViewModel(file, false, filter));
				}
			}
			catch (OperationCanceledException)
			{
				// A newer keystroke (or a reload) replaced this run.
			}
		}
	}
}