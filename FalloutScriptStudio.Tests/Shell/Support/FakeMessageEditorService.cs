namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>A message editor service that records the files it was asked to open.</summary>
	internal sealed class FakeMessageEditorService : IMessageEditorService
	{
		public event EventHandler? AllWindowsClosed
		{
			add { }
			remove { }
		}

		public Int32 OpenWindowCount => this.Opened.Count;

		public List<String?> Opened { get; } = new List<String?>();

		public List<IMessageLineTarget?> Targets { get; } = new List<IMessageLineTarget?>();

		public Boolean Open(String? msgFilePath, IMessageLineTarget? target = null)
		{
			this.Opened.Add(msgFilePath);
			this.Targets.Add(target);
			return true;
		}

		public List<MessageEditorOpenOptions> Options { get; } = new List<MessageEditorOpenOptions>();

		public Boolean Open(MessageEditorOpenOptions options)
		{
			this.Options.Add(options);
			return this.Open(options.FilePath, options.Target);
		}

		public Task<Boolean> OpenForScriptAsync(String scriptPath, Int32? scriptNameId, IMessageLineTarget target, Boolean createIfMissing = true)
		{
			this.Opened.Add(scriptPath);
			this.Targets.Add(target);
			return Task.FromResult(true);
		}

		public Task<Boolean> CloseAllAsync()
		{
			return Task.FromResult(true);
		}
	}
}
