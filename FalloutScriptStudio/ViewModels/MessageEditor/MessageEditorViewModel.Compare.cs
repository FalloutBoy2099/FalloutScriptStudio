namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>The comparison of the file with the files of the other languages.</summary>
	public sealed partial class MessageEditorViewModel
	{
		/// <summary>Opens "Compare Message Files" on the file that is edited (the file as it is on the disk).</summary>
		/// <returns>A task that completes when the window is shown.</returns>
		[RelayCommand(CanExecute = nameof(CanCompareFiles))]
		private async Task CompareMessageFilesAsync()
		{
			String? file = this._filePath;
			if (file is not null && this._toolWindows is not null)
			{
				await this._toolWindows.ShowCompareMessagesAsync(new CompareMessagesRequest(file));
			}
		}

		private Boolean CanCompareFiles()
		{
			return this._filePath is not null && this._toolWindows is not null;
		}
	}
}