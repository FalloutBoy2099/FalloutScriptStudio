namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;

	/// <summary>Replace in Files: the command that opens the search window ready to replace over a folder, and the one that undoes the last run.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Opens the search window with the folder scope chosen, to replace in the files of a folder with a preview (Edit menu).</summary>
		[RelayCommand]
		public async Task ReplaceInFilesAsync()
		{
			await this._scriptTools.ShowReplaceInFilesAsync(this.ActiveEditor?.WordAtCaret);
		}

		/// <summary>Gets a value indicating whether the last Replace in Files can be undone (the Edit menu item and the palette command are disabled otherwise).</summary>
		public Boolean CanUndoReplaceInFiles => this._scriptTools.CanUndoReplaceInFiles;

		/// <summary>Puts back what the last Replace in Files replaced, in every file that was not changed since (Edit menu).</summary>
		[RelayCommand(CanExecute = nameof(CanUndoReplaceInFiles))]
		public async Task UndoReplaceInFilesAsync()
		{
			await this._scriptTools.UndoReplaceInFilesAsync();
		}

		private void OnUndoReplaceInFilesStateChanged()
		{
			this.OnPropertyChanged(nameof(this.CanUndoReplaceInFiles));
			this.UndoReplaceInFilesCommand.NotifyCanExecuteChanged();
		}
	}
}