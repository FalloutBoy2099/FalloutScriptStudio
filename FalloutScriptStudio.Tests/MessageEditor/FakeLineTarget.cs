namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>
	/// An <see cref="IMessageLineTarget"/> that records what the editor tells it.
	/// </summary>
	internal sealed class FakeLineTarget : IMessageLineTarget
	{
		public String? ScriptPath { get; set; } = @"C:\scripts\hakunin.ssl";

		public Boolean CanAcceptMessageLine { get; set; } = true;

		public Boolean CanOpenAsText { get; set; } = true;

		public List<(Int32 Id, String Text)> Received { get; } = new List<(Int32, String)>();

		public List<(String Path, MessageDocument Document)> Updates { get; } = new List<(String, MessageDocument)>();

		public List<String> OpenedAsText { get; } = new List<String>();

		public void AcceptMessageLine(Int32 messageId, String messageText)
		{
			this.Received.Add((messageId, messageText));
		}

		public void MessagesUpdated(String messageFilePath, MessageDocument document)
		{
			this.Updates.Add((messageFilePath, document));
		}

		public Boolean TryOpenAsText(String messageFilePath)
		{
			this.OpenedAsText.Add(messageFilePath);
			return this.CanOpenAsText;
		}
	}
}
