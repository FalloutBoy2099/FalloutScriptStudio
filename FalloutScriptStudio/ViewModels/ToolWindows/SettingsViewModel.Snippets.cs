namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;

	// Code editor page: the button that opens the snippet manager.
	public sealed partial class SettingsViewModel
	{
		/// <summary>Opens the snippet manager. The snippets are stored in their own files, so the manager saves them itself.</summary>
		/// <returns>A task that completes when the manager is closed.</returns>
		[RelayCommand(CanExecute = nameof(CanManageSnippets))]
		public Task ManageSnippetsAsync()
		{
			return this._toolWindows is null ? Task.CompletedTask : this._toolWindows.ShowSnippetManagerAsync();
		}

		private Boolean CanManageSnippets()
		{
			return this._toolWindows is not null;
		}
	}
}