namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.IO;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;

	// Paths page: output folder, scripts.h and headers folder.
	public sealed partial class SettingsViewModel
	{
		/// <summary>Gets or sets the folder for compiled scripts (empty when not set).</summary>
		public String OutputDirectory
		{
			get => this.Draft.Paths.OutputDirectory ?? String.Empty;
			set
			{
				if (!String.Equals(this.Draft.Paths.OutputDirectory ?? String.Empty, value, StringComparison.Ordinal))
				{
					this.Draft.Paths.OutputDirectory = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>Gets or sets the path of <c>scripts.h</c> (empty when not set).</summary>
		public String ScriptsHeaderFile
		{
			get => this.Draft.Paths.ScriptsHeaderFile ?? String.Empty;
			set
			{
				if (!String.Equals(this.Draft.Paths.ScriptsHeaderFile ?? String.Empty, value, StringComparison.Ordinal))
				{
					this.Draft.Paths.ScriptsHeaderFile = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>Gets or sets the headers folder (empty when not set).</summary>
		public String HeadersDirectory
		{
			get => this.Draft.Paths.HeadersDirectory ?? String.Empty;
			set
			{
				if (!String.Equals(this.Draft.Paths.HeadersDirectory ?? String.Empty, value, StringComparison.Ordinal))
				{
					this.Draft.Paths.HeadersDirectory = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>Gets or sets the path of the external text editor (empty when not chosen yet).</summary>
		public String ExternalEditorPath
		{
			get => this.Draft.Paths.ExternalEditorPath ?? String.Empty;
			set
			{
				if (!String.Equals(this.Draft.Paths.ExternalEditorPath ?? String.Empty, value, StringComparison.Ordinal))
				{
					this.Draft.Paths.ExternalEditorPath = value;
					this.OnPropertyChanged();
				}
			}
		}

		[RelayCommand]
		private void BrowseExternalEditor()
		{
			String? initial = ExistingDirectory(Path.GetDirectoryName(this.ExternalEditorPath)) ?? ExistingDirectory(this._settingsService.Paths.ProgramDirectory);
			String? file = this._dialogService.PickOpenFile(Loc.Get("Str.ToolWindows.Settings.SelectExternalEditor"), CreateFilter("Str.ToolWindows.Filter.ExecutableFiles", "*.exe"), initial);
			if (file != null)
			{
				this.ExternalEditorPath = file;
			}
		}

		[RelayCommand]
		private void ClearExternalEditorPath()
		{
			this.ExternalEditorPath = String.Empty;
		}

		[RelayCommand]
		private void BrowseOutputDirectory()
		{
			String? folder = this._dialogService.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectOutputFolder"), ExistingDirectory(this.OutputDirectory));
			if (folder != null)
			{
				this.OutputDirectory = folder;
			}
		}

		[RelayCommand]
		private void ClearOutputDirectory()
		{
			this.OutputDirectory = String.Empty;
		}

		[RelayCommand]
		private void BrowseScriptsHeaderFile()
		{
			String? initial = ExistingDirectory(Path.GetDirectoryName(this.ScriptsHeaderFile)) ?? ExistingDirectory(this.HeadersDirectory);
			String? file = this._dialogService.PickOpenFile(Loc.Get("Str.ToolWindows.Settings.SelectScriptsHeader"), CreateFilter("Str.ToolWindows.Filter.HeaderFiles", "*.h"), initial);
			if (file != null)
			{
				this.ScriptsHeaderFile = file;
			}
		}

		[RelayCommand]
		private void ClearScriptsHeaderFile()
		{
			this.ScriptsHeaderFile = String.Empty;
		}

		[RelayCommand]
		private void BrowseHeadersDirectory()
		{
			String? folder = this._dialogService.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectHeadersFolder"), ExistingDirectory(this.HeadersDirectory));
			if (folder == null)
			{
				return;
			}

			this.HeadersDirectory = folder;
			if (String.IsNullOrWhiteSpace(this.ScriptsHeaderFile))
			{
				this.ScriptsHeaderFile = Path.Combine(folder, "SCRIPTS.H");
			}
		}

		[RelayCommand]
		private void ClearHeadersDirectory()
		{
			this.HeadersDirectory = String.Empty;
		}
	}
}
