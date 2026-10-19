namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// Builds a <see cref="MessageEditorViewModel"/> with fake dialogs and settings and the real message file service,
	/// working in private scratch folders of <c>_test_files\work</c>.
	/// </summary>
	internal sealed class MessageEditorHarness : IDisposable
	{
		private readonly TestFolder _folder;
		private readonly TestFolder _installation;

		public MessageEditorHarness(Action<FakeSettingsService>? configure = null)
		{
			this._folder = new TestFolder("msgeditor");
			this._installation = new TestFolder("msgeditor-app");
			this.Settings = new FakeSettingsService(this._installation.Path);
			configure?.Invoke(this.Settings);
			this.Dialogs = new FakeDialogService();
			this.Platform = new FakePlatform();
			this.Editor = new MessageEditorViewModel(
				new MessageFileService(NullLogger<MessageFileService>.Instance),
				new MessageStructureChecker(),
				this.Settings,
				this.Dialogs,
				this.Platform,
				NullLogger<MessageEditorViewModel>.Instance);
			this.Editor.RowFocusRequested += (_, e) => this.FocusRequests.Add(e);
		}

		public String Folder => this._folder.Path;

		public FakeSettingsService Settings { get; }

		public FakeDialogService Dialogs { get; }

		public FakePlatform Platform { get; }

		public MessageEditorViewModel Editor { get; }

		public List<RowFocusRequestedEventArgs> FocusRequests { get; } = [];

		public IReadOnlyList<MessageRowViewModel> Rows => this.Editor.Rows;

		/// <summary>Gets the rows that were selected by the last focus request.</summary>
		public IReadOnlyList<MessageRowViewModel> FocusedRows => this.FocusRequests[^1].Rows;

		public String WriteFile(String name, params String[] lines)
		{
			String path = Path.Combine(this.Folder, name);
			File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
			return path;
		}

		public async Task<Boolean> OpenAsync(String name, params String[] lines)
		{
			String path = this.WriteFile(name, lines);
			return await this.Editor.LoadFileAsync(path);
		}

		/// <summary>Imitates the view: selects the rows and the column.</summary>
		public void Select(Int32 index, MessageGridColumn column = MessageGridColumn.Text, params Int32[] more)
		{
			List<MessageRowViewModel> selected = [this.Rows[index], .. more.Select(extra => this.Rows[extra])];
			this.Editor.UpdateSelection(this.Rows[index], selected, column);
		}

		public String[] Format()
		{
			return this.Editor.BuildDocument().Format().ToArray();
		}

		public void Dispose()
		{
			this.Editor.Dispose();
			this._folder.Dispose();
			this._installation.Dispose();
		}
	}
}