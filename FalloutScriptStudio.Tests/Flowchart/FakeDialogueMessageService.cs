namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>Serves the sample messages from memory and records the texts written back.</summary>
	internal sealed class FakeDialogueMessageService : IDialogueMessageService
	{
		private MessageDocument _messages = FlowchartSamples.Messages();

		public List<(String Path, Int32 Id, String Text)> Updates { get; } = [];

		public Int32 SourcesCreated { get; private set; }

		public Boolean UpdateSucceeds { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether loading other message files finishes on a thread-pool thread, like the real source does.</summary>
		public Boolean LoadOnAnotherThread { get; set; }

		/// <summary>Gets the other message files (by file number) that new sources report as not found, with the path they were expected at.</summary>
		public Dictionary<Int32, String> MissingFiles { get; } = [];

		/// <summary>Replaces the messages that new sources serve.</summary>
		public void SetMessages(MessageDocument messages)
		{
			this._messages = messages;
		}

		public Task<IDialogueMessageSource> CreateSourceAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
		{
			this.SourcesCreated++;
			IDialogueMessageSource source = new DialogueMessageSource(this._messages, FlowchartSamples.MessagePath);
			IDialogueMessageSource result = this.MissingFiles.Count == 0 ? source : new MissingFilesSource(source, this.MissingFiles);
			return Task.FromResult(this.LoadOnAnotherThread ? new ThreadHoppingSource(result) : result);
		}

		private sealed class ThreadHoppingSource : IDialogueMessageSource
		{
			private readonly IDialogueMessageSource _inner;

			public ThreadHoppingSource(IDialogueMessageSource inner)
			{
				this._inner = inner;
			}

			public async Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default)
			{
				await Task.Delay(30, cancellationToken).ConfigureAwait(false);
				await this._inner.EnsureLoadedAsync(messageFileNumbers, cancellationToken).ConfigureAwait(false);
			}

			public DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber)
			{
				return this._inner.GetMessage(messageFileNumber, messageNumber);
			}

			public String? GetMessageFilePath(Int32 messageFileNumber)
			{
				return this._inner.GetMessageFilePath(messageFileNumber);
			}
		}

		private sealed class MissingFilesSource : IDialogueMessageSource
		{
			private readonly IDialogueMessageSource _inner;
			private readonly Dictionary<Int32, String> _missing;

			public MissingFilesSource(IDialogueMessageSource inner, Dictionary<Int32, String> missing)
			{
				this._inner = inner;
				this._missing = missing;
			}

			public Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default)
			{
				return this._inner.EnsureLoadedAsync(messageFileNumbers, cancellationToken);
			}

			public DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber)
			{
				return this._missing.TryGetValue(messageFileNumber, out String? path)
					? new DialogueMessageLookup(DialogueMessageStatus.FileNotFound, "[Error] missing", path)
					: this._inner.GetMessage(messageFileNumber, messageNumber);
			}

			public String? GetMessageFilePath(Int32 messageFileNumber)
			{
				return this._missing.TryGetValue(messageFileNumber, out String? path) ? path : this._inner.GetMessageFilePath(messageFileNumber);
			}
		}

		public Task<Boolean> UpdateMessageTextAsync(String messageFilePath, Int32 messageId, String text, CancellationToken cancellationToken = default)
		{
			this.Updates.Add((messageFilePath, messageId, text));
			MessageEntry? entry = this._messages.FindMessage(messageId);
			if (this.UpdateSucceeds && entry is not null)
			{
				entry.Text = text;
			}

			return Task.FromResult(this.UpdateSucceeds);
		}
	}
}