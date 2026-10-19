namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;

	/// <summary>The compile profile chosen in the toolbar, and the build commands that depend on up to date scripts.</summary>
	public sealed partial class MainViewModel
	{
		private Boolean _loadingProfiles;

		/// <summary>Gets the compile profiles the toolbar offers: "none", then the profiles of the settings and of the project.</summary>
		public ObservableCollection<CompileProfileOption> CompileProfileOptions { get; } = [];

		/// <summary>Gets or sets the name of the active compile profile of the toolbar (empty for none).</summary>
		public String SelectedCompileProfile
		{
			get => this._settings.Effective.ActiveCompileProfile?.Name ?? String.Empty;
			set
			{
				if (this._loadingProfiles)
				{
					return;
				}

				String? name = String.IsNullOrWhiteSpace(value) ? null : value;
				String current = this.SelectedCompileProfile;
				if (String.Equals(name ?? String.Empty, current, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}

				// The project owns the choice when it makes one; otherwise the global setting is changed, as without a project.
				if (this._projects.Settings is { ActiveCompileProfile: not null } project)
				{
					ProjectSettings changed = project.Clone();
					changed.ActiveCompileProfile = name ?? String.Empty;
					if (this._projects.SaveSettings(changed))
					{
						// The project change parses the open documents again.
						this.OnPropertyChanged();
					}

					return;
				}

				this._settings.Update(settings => settings.Compiler.ActiveProfile = name);
				this.OnPropertyChanged();
				this.OnPropertyChanged(nameof(this.CanChooseDefine));
				this.OnPropertyChanged(nameof(this.SelectedPreprocessorDefine));
				this.OnPropertyChanged(nameof(this.Title));
				foreach (DocumentViewModel document in this._workspace.Documents.Where(d => d.IsParsable))
				{
					_ = this._workspace.RequestParseAsync(document, false);
				}
			}
		}

		/// <summary>Gets a value indicating whether the definition of the toolbar can be changed: not while the active profile sets the definition itself.</summary>
		public Boolean CanChooseDefine => this._settings.Effective.ActiveCompileProfile?.PreprocessorDefine is null;

		/// <summary>Gets or sets a value indicating whether a mass compilation skips the scripts that are up to date.</summary>
		public Boolean MassCompileOnlyChanged
		{
			get => this._settings.Settings.Compiler.MassCompileOnlyChanged;
			set => this.SetSetting(settings => settings.Compiler.MassCompileOnlyChanged = value, nameof(this.MassCompileOnlyChanged));
		}

		/// <summary>Compiles the open scripts that changed since their last successful build.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task BuildChangedAsync()
		{
			await this._build.BuildChangedAsync();
		}

		/// <summary>Compiles all scripts of a folder, including the ones that are up to date.</summary>
		[RelayCommand(CanExecute = nameof(CanBuild))]
		public async Task RebuildAllAsync()
		{
			await this._build.MassCompileAsync(true);
		}

		private void LoadCompileProfiles()
		{
			EffectiveSettings effective = this._settings.Effective;
			List<String> values = [String.Empty, .. effective.CompileProfiles.Select(profile => profile.Name)];

			// The list is rebuilt only when it changed: the selection of the combo box must not be disturbed while it is being made.
			if (!values.SequenceEqual(this.CompileProfileOptions.Select(option => option.Value)))
			{
				this._loadingProfiles = true;
				try
				{
					this.CompileProfileOptions.Clear();
					foreach (String name in values)
					{
						this.CompileProfileOptions.Add(new CompileProfileOption(name));
					}
				}
				finally
				{
					this._loadingProfiles = false;
				}
			}

			this.OnPropertyChanged(nameof(this.SelectedCompileProfile));
			this.OnPropertyChanged(nameof(this.CanChooseDefine));
		}
	}
}