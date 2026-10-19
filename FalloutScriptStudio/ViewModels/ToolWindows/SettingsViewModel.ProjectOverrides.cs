namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;

	// What the open project overrides: the cards of the options it overrides say so, link to the project settings, and are shown as not in effect.
	public sealed partial class SettingsViewModel
	{
		private IReadOnlyList<String>? _overridden;
		private String? _projectName;

		/// <summary>
		/// Gets or sets the function that shows the project settings with an option selected (set by the window service that opens this
		/// window); without it the link "Edit project setting" does nothing.
		/// </summary>
		public Func<String, Task>? EditProjectSettingHandler { get; set; }

		/// <summary>Gets the identifiers of the options the open project overrides (see <see cref="ProjectOptions"/>); empty when no project is open.</summary>
		public IReadOnlyList<String> OverriddenOptions => this._overridden ??= this.ReadOverriddenOptions();

		/// <summary>Gets a value indicating whether the open project overrides any option.</summary>
		public Boolean HasProjectOverrides => this.OverriddenOptions.Count > 0;

		/// <summary>Gets a value indicating whether the open project replaces the folders searched for message files.</summary>
		public Boolean IsMessagePathsOverridden => this.OverriddenOptions.Contains(ProjectOptions.MessageSearchDirectories, StringComparer.Ordinal);

		/// <summary>Gets a value indicating whether the open project adds definitions to the global ones or selects one.</summary>
		public Boolean HasProjectDefines => this.OverriddenOptions.Contains(ProjectOptions.Defines, StringComparer.Ordinal)
			|| this.OverriddenOptions.Contains(ProjectOptions.PreprocessorDefine, StringComparer.Ordinal);

		/// <summary>Gets the name of the open project.</summary>
		public String ProjectName => this._projectName ??= this._settingsService.Effective.ProjectName;

		/// <summary>Gets the note shown on an option the open project overrides ("Overridden by project X"), in the current language.</summary>
		public String OverrideBadgeText => Loc.Format("Str.ToolWindows.Override.Badge", this.ProjectName);

		/// <summary>Gets the tool tip of the control of an option the open project overrides, in the current language.</summary>
		public String OverrideTipText => Loc.Format("Str.ToolWindows.Override.Tip", this.ProjectName);

		/// <summary>Gets the note shown on the definitions when the open project adds its own, in the current language.</summary>
		public String DefinesBadgeText => Loc.Format("Str.ToolWindows.Override.DefinesBadge", this.ProjectName);

		/// <summary>Reads the overrides of the open project again (after the project settings were edited).</summary>
		public void RefreshProjectOverrides()
		{
			this._overridden = null;
			this._projectName = null;
			this.RaiseProjectOverrideChanges();
		}

		/// <inheritdoc/>
		protected override void OnLanguageChanged()
		{
			base.OnLanguageChanged();
			this.OnPropertyChanged(nameof(this.OverrideBadgeText));
			this.OnPropertyChanged(nameof(this.OverrideTipText));
			this.OnPropertyChanged(nameof(this.DefinesBadgeText));
		}

		[RelayCommand]
		private async Task EditProjectSettingAsync(String? optionId)
		{
			if (this.EditProjectSettingHandler is null || String.IsNullOrEmpty(optionId))
			{
				return;
			}

			await this.EditProjectSettingHandler(optionId);
			this.RefreshProjectOverrides();
		}

		private IReadOnlyList<String> ReadOverriddenOptions()
		{
			return this._settingsService.Effective.GetOverriddenOptions();
		}

		private void RaiseProjectOverrideChanges()
		{
			foreach (String name in new[]
			{
				nameof(this.OverriddenOptions),
				nameof(this.HasProjectOverrides),
				nameof(this.IsMessagePathsOverridden),
				nameof(this.HasProjectDefines),
				nameof(this.ProjectName),
				nameof(this.OverrideBadgeText),
				nameof(this.OverrideTipText),
				nameof(this.DefinesBadgeText),
			})
			{
				this.OnPropertyChanged(name);
			}
		}
	}
}