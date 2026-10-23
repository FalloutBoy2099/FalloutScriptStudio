namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.MessageEditor;

	/// <summary>The comparison of the message files of the languages of the game.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>
		/// Opens "Compare Message Files" on a reference message file: the message file of the document (the document itself when it is a message file, the
		/// associated message file of a script), otherwise a file chosen by the user. The files of the other languages of the game are offered as targets.
		/// </summary>
		/// <param name="document">The document the command was started from (the tab of a message file); the active document when <see langword="null"/>.</param>
		/// <returns>A task that completes when the window is shown.</returns>
		[RelayCommand]
		public async Task CompareMessageFilesAsync(DocumentViewModel? document = null)
		{
			DocumentViewModel? source = document ?? this._workspace.ActiveDocument;
			String? reference = await this.FindComparisonReferenceAsync(source);
			if (reference is null)
			{
				reference = this._dialogs.PickOpenFile(Loc.Get("Str.ToolWindows.Compare.ChooseReferenceTitle"), MessageEditorViewModel.FileFilter, source?.FilePath is null ? null : Path.GetDirectoryName(source.FilePath));
			}

			if (reference is not null)
			{
				await this._toolWindows.ShowCompareMessagesAsync(new CompareMessagesRequest(reference));
			}
		}

		private async Task<String?> FindComparisonReferenceAsync(DocumentViewModel? document)
		{
			if (document is null || document.FilePath is null)
			{
				return null;
			}

			if (document.Kind == DocumentKind.Message)
			{
				return document.FilePath;
			}

			if (document.Kind != DocumentKind.Script)
			{
				return null;
			}

			if (document.MessageFilePath is { } known && File.Exists(known))
			{
				return known;
			}

			return await this._workspace.AssociateMessageFileAsync(document, false, false);
		}
	}
}