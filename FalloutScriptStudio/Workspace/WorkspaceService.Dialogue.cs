namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Documents;
	using Localization;
	using Services.ScriptTools;

	/// <summary>The dialogue tools that start from a script: the preview from a node and the check of the messages.</summary>
	public sealed partial class WorkspaceService
	{
		public async Task PreviewDialogueFromNodeAsync(DocumentViewModel document, String nodeName)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentException.ThrowIfNullOrEmpty(nodeName);

			String title = Loc.Get("Str.Shell.Dlg.PreviewTitle");
			if (document.Kind != DocumentKind.Script || document.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.NoMessageFileKind"));
				return;
			}

			if (document.ProgramInfo is null)
			{
				await this.RequestParseAsync(document, true);
			}

			if (await this.AssociateMessageFileAsync(document, false, true) is null)
			{
				await this._dialogs.ShowInfoAsync(title, Loc.Get("Str.Shell.Dlg.MessageFileNotFound"));
				return;
			}

			await this._scriptTools.ShowDialoguePreviewAsync(document, new DialoguePreviewOpenOptions { StartAtNode = nodeName });
		}

		public Task<Int32> CheckMessagesAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this._consistency is null ? Task.FromResult(0) : this._consistency.CheckAsync(document);
		}

		public Task<DialogueCheckResult> CheckDialogueAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this._dialogueQuality is null ? Task.FromResult(default(DialogueCheckResult)) : this._dialogueQuality.CheckAsync(document);
		}

		public Task AddMissingMessagesAsync(String scriptPath, IReadOnlyList<Int32> numbers)
		{
			return this._consistency is null ? Task.CompletedTask : this._consistency.AddMissingMessagesAsync(scriptPath, numbers);
		}

		public Task CreateMessageFileAsync(String scriptPath)
		{
			return this._consistency is null ? Task.CompletedTask : this._consistency.CreateMessageFileAsync(scriptPath);
		}
	}
}