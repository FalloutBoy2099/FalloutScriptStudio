namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.IO;
	using System.Security;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;

	// File association (shown on the Code editor page).
	public sealed partial class SettingsViewModel
	{
		[ObservableProperty]
		private Boolean _isAssociated;

		/// <summary>Gets the text that describes the state of the file association.</summary>
		public String AssociationStatus => this.IsAssociated
			? Loc.Get("Str.ToolWindows.Settings.AssociationYes")
			: Loc.Get("Str.ToolWindows.Settings.AssociationNo");

		partial void OnIsAssociatedChanged(Boolean value)
		{
			this.OnPropertyChanged(nameof(this.AssociationStatus));
		}

		private void RefreshAssociation()
		{
			try
			{
				this.IsAssociated = this._fileAssociation.IsAssociated;
			}
			catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
			{
				this.IsAssociated = false;
			}
		}

		[RelayCommand]
		private async Task AssociateFileTypesAsync()
		{
			Boolean confirmed = await this._dialogService.ConfirmAsync(
				Loc.Get("Str.ToolWindows.Settings.FileAssociation"),
				Loc.Get("Str.ToolWindows.Settings.AssociateQuestion"),
				Loc.Get("Str.Common.Yes"),
				Loc.Get("Str.Common.No")).ConfigureAwait(true);
			if (!confirmed)
			{
				return;
			}

			AppPaths paths = this._settingsService.Paths;
			String executable = Environment.ProcessPath ?? Path.Combine(paths.ProgramDirectory, "FalloutScriptStudio.exe");

			try
			{
				this._fileAssociation.Associate(executable, paths.ResourcesDirectory);
			}
			catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
			{
				await this._dialogService.ShowErrorAsync(Loc.Get("Str.ToolWindows.Settings.FileAssociation"), Loc.Format("Str.ToolWindows.Settings.AssociateFailed", ex.Message)).ConfigureAwait(true);
				return;
			}

			this.RefreshAssociation();
			await this._dialogService.ShowInfoAsync(Loc.Get("Str.ToolWindows.Settings.FileAssociation"), Loc.Get("Str.ToolWindows.Settings.AssociateDone")).ConfigureAwait(true);
		}
	}
}
