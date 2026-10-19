namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>A workspace with the project index, the symbol service and the code intelligence operations, wired to fake dialogs and a scratch folder.</summary>
	internal sealed class IntelligenceHarness : IDisposable
	{
		public IntelligenceHarness()
		{
			this.Folder = new TempFolder();
			this.Paths = AppPaths.FromBaseDirectory(this.Folder.Combine("app"));
			this.Paths.EnsureCreated();
			this.Settings = new SettingsService(this.Paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
			this.Settings.Load();
			this.Parser = new FakeParser();
			this.Coordinator = new ParseCoordinator(this.Parser, this.Settings, null, TimeSpan.FromMilliseconds(30));
			this.Dialogs = new FakeDialogService();
			this.Output = new FakeOutputSink();
			this.ScriptTools = new FakeScriptToolService();
			this.ToolWindows = new FakeToolWindowService();
			this.Problems = new ProblemService();
			this.Projects = new ProjectService(this.Settings, NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled);
			this.Index = new ProjectSymbolIndex(() => new ProjectIndexOptions { Encoding = this.Settings.Settings.Editor.FileEncoding.ToEncoding() });
			this.Symbols = new ProjectSymbolService(this.Index, this.Settings, this.Projects, this.Output, null, TimeSpan.Zero);
			this.Intelligence = new CodeIntelligenceOperations(this.Symbols, this.ToolWindows, this.ScriptTools, this.Dialogs, this.Settings, this.Coordinator, this.Problems, this.Output);
			this.Workspace = WorkspaceServiceFactory.Create(
				this.Settings,
				this.Coordinator,
				new FakeScriptCompiler(),
				new FakeArchiveExtractor(),
				this.Dialogs,
				this.Output,
				new FakeMessageEditorService(),
				this.ScriptTools,
				new FakeDialogueDialogsService(),
				this.Problems,
				null,
				this.Symbols,
				this.Intelligence);
		}

		public TempFolder Folder { get; }

		public AppPaths Paths { get; }

		public SettingsService Settings { get; }

		public FakeParser Parser { get; }

		public ParseCoordinator Coordinator { get; }

		public FakeDialogService Dialogs { get; }

		public FakeOutputSink Output { get; }

		public FakeScriptToolService ScriptTools { get; }

		public FakeToolWindowService ToolWindows { get; }

		public ProblemService Problems { get; }

		public ProjectService Projects { get; }

		public ProjectSymbolIndex Index { get; }

		public ProjectSymbolService Symbols { get; }

		public CodeIntelligenceOperations Intelligence { get; }

		public WorkspaceService Workspace { get; }

		/// <summary>Writes a file below the scratch folder, with an old time stamp (so that a later run does not read it again for nothing).</summary>
		public String Write(String relativePath, String text)
		{
			String path = Path.GetFullPath(this.Folder.Write(relativePath, text));
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
			return path;
		}

		/// <summary>Opens a folder of the scratch folder as the project and indexes it.</summary>
		public async Task OpenProjectAsync(String relativeFolder)
		{
			String folder = this.Folder.Combine(relativeFolder);
			Directory.CreateDirectory(folder);
			this.Projects.Open(folder);
			await this.Symbols.RefreshAsync();
		}

		/// <summary>Opens a file as a document and parses it.</summary>
		public async Task<DocumentViewModel> OpenAsync(String path)
		{
			DocumentViewModel? document = await this.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = false });
			document = document ?? throw new InvalidOperationException("The file was not opened.");
			await this.Workspace.RequestParseAsync(document, true);
			return document;
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
			this.Symbols.Dispose();
			this.Index.Dispose();
			this.Projects.Dispose();
			this.Folder.Dispose();
		}
	}
}