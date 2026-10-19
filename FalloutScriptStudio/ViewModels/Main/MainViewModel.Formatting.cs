namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;

	/// <summary>Code menu: formatting the files of a project and the snippet manager.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Opens "Format Project": the preview of the files that formatting would change, and the application of the formatting to the files the user chooses.</summary>
		/// <returns>A task that completes when the window is closed.</returns>
		[RelayCommand]
		public async Task FormatProjectAsync()
		{
			await this._toolWindows.ShowFormatProjectAsync();
		}

		/// <summary>Opens the snippet manager (Code &gt; Manage Snippets).</summary>
		/// <returns>A task that completes when the window is closed.</returns>
		[RelayCommand]
		public async Task ManageSnippetsAsync()
		{
			await this._toolWindows.ShowSnippetManagerAsync();
		}

		private void InitializeFormatting()
		{
			this.Project.FormatProjectHandler = this.FormatProjectAsync;
		}
	}
}