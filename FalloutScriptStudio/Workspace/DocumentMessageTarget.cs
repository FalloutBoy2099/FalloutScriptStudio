namespace FalloutScriptStudio.Workspace
{
	using System;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>Lets the Message Editor send message numbers into the script tab that owns the message file.</summary>
	public sealed class DocumentMessageTarget : IMessageLineTarget
	{
		private readonly IWorkspaceManager _workspace;
		private readonly DocumentViewModel _document;

		/// <summary>Initializes the target for a script.</summary>
		public DocumentMessageTarget(IWorkspaceManager workspace, DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			this._workspace = workspace;
			this._document = document;
		}

		public String? ScriptPath => this._document.FilePath;

		public Boolean CanAcceptMessageLine => this._workspace.Documents.Contains(this._document);

		public void AcceptMessageLine(Int32 messageId, String messageText)
		{
			if (!this._workspace.Documents.Contains(this._document))
			{
				return;
			}

			this._workspace.ActiveDocument = this._document;
			this._document.Editor?.InsertText(messageId.ToString(System.Globalization.CultureInfo.InvariantCulture));
			this._document.Editor?.FocusEditor();
		}

		public void MessagesUpdated(String messageFilePath, MessageDocument document)
		{
			this._document.MessageFilePath = messageFilePath;
			this._workspace.UpdateMessageTexts(this._document, document);
		}

		public Boolean TryOpenAsText(String messageFilePath)
		{
			_ = this._workspace.OpenAsync(messageFilePath, new OpenDocumentOptions { AddToRecent = false });
			return true;
		}
	}
}
