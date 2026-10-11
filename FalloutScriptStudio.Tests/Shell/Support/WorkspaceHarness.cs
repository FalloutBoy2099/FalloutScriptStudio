namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>A workspace wired with fake services and a temporary program folder.</summary>
	internal sealed class WorkspaceHarness : IDisposable
	{
		public WorkspaceHarness(TimeSpan? debounce = null)
		{
			this.Folder = new TempFolder();
			this.Paths = AppPaths.FromBaseDirectory(this.Folder.Combine("app"));
			this.Paths.EnsureCreated();
			this.Settings = new SettingsService(this.Paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
			this.Settings.Load();
			this.Parser = new FakeParser();
			this.Coordinator = new ParseCoordinator(this.Parser, this.Settings, null, debounce ?? TimeSpan.FromMilliseconds(30));
			this.Compiler = new FakeScriptCompiler();
			this.Archives = new FakeArchiveExtractor();
			this.Dialogs = new FakeDialogService();
			this.Output = new FakeOutputSink();
			this.MessageEditor = new FakeMessageEditorService();
			this.ScriptTools = new FakeScriptToolService();
			this.DialogueDialogs = new FakeDialogueDialogsService();
			this.Workspace = WorkspaceServiceFactory.Create(this.Settings, this.Coordinator, this.Compiler, this.Archives, this.Dialogs, this.Output, this.MessageEditor, this.ScriptTools, this.DialogueDialogs);
		}

		public TempFolder Folder { get; }

		public AppPaths Paths { get; }

		public SettingsService Settings { get; }

		public FakeParser Parser { get; }

		public ParseCoordinator Coordinator { get; }

		public FakeScriptCompiler Compiler { get; }

		public FakeArchiveExtractor Archives { get; }

		public FakeDialogService Dialogs { get; }

		public FakeOutputSink Output { get; }

		public FakeMessageEditorService MessageEditor { get; }

		public FakeScriptToolService ScriptTools { get; }

		public FakeDialogueDialogsService DialogueDialogs { get; }

		public WorkspaceService Workspace { get; }

		/// <summary>Writes a file into the temporary folder and opens it.</summary>
		public async Task<DocumentViewModel> OpenFileAsync(String relativePath, String text)
		{
			String path = this.Folder.Write(relativePath, text);
			DocumentViewModel? document = await this.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = true });
			return document ?? throw new InvalidOperationException("The file was not opened.");
		}

		/// <summary>Attaches a fake editor surface to a document as the editor view would.</summary>
		public FakeEditorSurface AttachEditor(DocumentViewModel document)
		{
			FakeEditorSurface editor = new FakeEditorSurface(document.TextDocument);
			document.Editor = editor;
			document.ViewState.HasBeenShown = true;
			return editor;
		}

		public void Dispose()
		{
			this.Folder.Dispose();
		}
	}
}
