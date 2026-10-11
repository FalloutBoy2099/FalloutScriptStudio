namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Project;

	/// <summary>The "Solution" tab: the project folder as a tree of scripts, headers and message files.</summary>
	public sealed partial class ProjectExplorerViewModel : ObservableObject
	{
		private readonly ISettingsService _settings;
		private readonly IWorkspaceManager _workspace;
		private readonly IDialogService _dialogs;
		private readonly IShellService _shell;
		private String? _loadedFolder;

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

		/// <summary>Gets the top level nodes (the content of the project folder, or the search results).</summary>
		public ObservableCollection<ProjectNodeViewModel> Nodes { get; } = new ObservableCollection<ProjectNodeViewModel>();

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
			this.Nodes.Clear();
			String? folder = this.ProjectFolder;
			this._loadedFolder = folder;
			this.HasProject = folder is not null && Directory.Exists(folder);
			if (!this.HasProject || folder is null)
			{
				return;
			}

			ProjectFileFilter filter = new ProjectFileFilter(this.ShowCompiled);
			if (this.FilterText.Trim().Length > 0)
			{
				foreach (String file in filter.Search(folder, this.FilterText.Trim()))
				{
					this.Nodes.Add(new ProjectNodeViewModel(file, false, filter));
				}

				return;
			}

			ProjectNodeViewModel root = new ProjectNodeViewModel(folder, true, filter);
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
		[RelayCommand]
		public void CopyPath(ProjectNodeViewModel? node)
		{
			if (node is not null)
			{
				this._shell.SetClipboardText(node.FullPath);
			}
		}

		partial void OnFilterTextChanged(String value)
		{
			this.Reload();
		}

		partial void OnShowCompiledChanged(Boolean value)
		{
			this.Reload();
		}
	}
}
