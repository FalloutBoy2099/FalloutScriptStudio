namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>Records the message editors that were requested; it can send a line to the target like the real one does.</summary>
	internal sealed class FakeMessageEditorService : IMessageEditorService
	{
		public event EventHandler? AllWindowsClosed;

		public Int32 OpenWindowCount => 0;

		public List<MessageEditorOpenOptions> Requests { get; } = new List<MessageEditorOpenOptions>();

		/// <summary>Gets or sets the message the user "sends" from the editor, or <see langword="null"/> to send nothing.</summary>
		public Int32? SendMessageId { get; set; }

		public Boolean Open(String? msgFilePath, IMessageLineTarget? target = null)
		{
			return this.Open(new MessageEditorOpenOptions { FilePath = msgFilePath, Target = target });
		}

		public Boolean Open(MessageEditorOpenOptions options)
		{
			this.Requests.Add(options);
			if (this.SendMessageId is Int32 id && options.Target is not null)
			{
				options.Target.AcceptMessageLine(id, "text");
			}

			this.AllWindowsClosed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		public Task<Boolean> OpenForScriptAsync(String scriptPath, Int32? scriptNameId, IMessageLineTarget target, Boolean createIfMissing = true)
		{
			return Task.FromResult(true);
		}

		public Task<Boolean> CloseAllAsync()
		{
			return Task.FromResult(true);
		}
	}
}
