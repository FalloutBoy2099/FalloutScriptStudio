namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.ObjectModel;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Workspace.Project;

	/// <summary>A folder or file in the project explorer. Folders load their content when they are expanded.</summary>
	public sealed partial class ProjectNodeViewModel : ObservableObject
	{
		private readonly ProjectFileFilter _filter;
		private Boolean _loaded;

		/// <summary>Initializes a node.</summary>
		public ProjectNodeViewModel(String fullPath, Boolean isDirectory, ProjectFileFilter filter)
		{
			this.FullPath = fullPath;
			this.IsDirectory = isDirectory;
			this._filter = filter;
			this.Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			if (this.Name.Length == 0)
			{
				this.Name = fullPath;
			}

			this.Kind = isDirectory ? DocumentKind.Other : DocumentViewModel.KindFromPath(fullPath);
			if (isDirectory)
			{
				// A placeholder makes the expander visible; the real content is read on first expansion.
				this.Children.Add(new ProjectNodeViewModel(String.Empty, false, filter) { Name = "..." });
			}
		}

		/// <summary>Gets the display name.</summary>
		public String Name { get; private init; }

		/// <summary>Gets the full path.</summary>
		public String FullPath { get; }

		/// <summary>Gets a value indicating whether the node is a folder.</summary>
		public Boolean IsDirectory { get; }

		/// <summary>Gets the document kind of a file (used for the icon).</summary>
		public DocumentKind Kind { get; }

		/// <summary>Gets a value indicating whether the file is a compiled script.</summary>
		public Boolean IsCompiled => !this.IsDirectory && String.Equals(Path.GetExtension(this.FullPath), ".int", StringComparison.OrdinalIgnoreCase);

		/// <summary>Gets a value indicating whether the file is a flowchart.</summary>
		public Boolean IsFlowchart => !this.IsDirectory && String.Equals(Path.GetExtension(this.FullPath), ".fcd", StringComparison.OrdinalIgnoreCase);

		/// <summary>Gets the child nodes of a folder.</summary>
		public ObservableCollection<ProjectNodeViewModel> Children { get; } = [];

		/// <summary>True when the folder is expanded.</summary>
		[ObservableProperty]
		public partial Boolean IsExpanded { get; set; }

		/// <summary>True when the node is selected.</summary>
		[ObservableProperty]
		public partial Boolean IsSelected { get; set; }

		/// <summary>Reads the content of the folder (once).</summary>
		public void EnsureLoaded()
		{
			if (!this.IsDirectory || this._loaded)
			{
				return;
			}

			this._loaded = true;
			this.Children.Clear();
			foreach (String directory in this._filter.GetDirectories(this.FullPath))
			{
				this.Children.Add(new ProjectNodeViewModel(directory, true, this._filter));
			}

			foreach (String file in this._filter.GetFiles(this.FullPath))
			{
				this.Children.Add(new ProjectNodeViewModel(file, false, this._filter));
			}
		}

		partial void OnIsExpandedChanged(Boolean value)
		{
			if (value)
			{
				this.EnsureLoaded();
			}
		}
	}
}