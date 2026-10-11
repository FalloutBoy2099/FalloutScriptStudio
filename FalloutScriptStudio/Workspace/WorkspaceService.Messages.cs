namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>Message file association, delegated to <see cref="MessageAssociationService"/>.</summary>
	public sealed partial class WorkspaceService
	{
		public String? GetMessageText(DocumentViewModel document, Int32 messageId)
		{
			return this._messages.GetMessageText(document, messageId);
		}

		public void UpdateMessageTexts(DocumentViewModel document, MessageDocument messages)
		{
			this._messages.UpdateMessageTexts(document, messages);
		}

		public Task OpenMessageEditorAsync(DocumentViewModel document)
		{
			return this._messages.OpenEditorAsync(this, document);
		}

		public Task RefreshMessageTextsAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this._messages.RefreshAsync(document, this.GetOpenDocumentTexts());
		}

		public IMessageLineTarget CreateMessageTarget(DocumentViewModel document)
		{
			return new DocumentMessageTarget(this, document);
		}

		/// <summary>Refreshes the cached texts of the scripts that use the message file of a message document that was saved or reloaded.</summary>
		private async Task RefreshScriptsOfMessageFileAsync(DocumentViewModel messageDocument)
		{
			if (messageDocument.Kind != DocumentKind.Message || messageDocument.FilePath is null)
			{
				return;
			}

			String full = DocumentPaths.GetFullPath(messageDocument.FilePath);
			foreach (DocumentViewModel script in this.Documents.Where(d => d.Kind == DocumentKind.Script && d.MessageFilePath is not null).ToList())
			{
				if (String.Equals(DocumentPaths.GetFullPath(script.MessageFilePath!), full, StringComparison.OrdinalIgnoreCase))
				{
					await this.RefreshMessageTextsAsync(script);
				}
			}
		}

		public Task<String?> AssociateMessageFileAsync(DocumentViewModel document, Boolean create, Boolean reportProblems)
		{
			return this._messages.AssociateAsync(document, create, reportProblems);
		}
	}
}
