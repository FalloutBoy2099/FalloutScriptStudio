namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>A workspace with the project index, the code intelligence operations and the live semantic diagnostics, wired to fake dialogs and a scratch folder.</summary>
	internal sealed class DiagnosticsHarness : IDisposable
	{
		public DiagnosticsHarness(TimeSpan? debounce = null)
		{
			this.Folder = new TempFolder();
			this.Paths = AppPaths.FromBaseDirectory(this.Folder.Combine("app"));
			this.Paths.EnsureCreated();
			CopyDescriptions(this.Paths.DescriptionsDirectory);
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
			this.Diagnostics = new LiveDiagnosticsService(this.Settings, this.Problems, this.Symbols, null, debounce ?? TimeSpan.Zero);
			this.Intelligence = new CodeIntelligenceOperations(this.Symbols, this.ToolWindows, this.ScriptTools, this.Dialogs, this.Settings, this.Coordinator, this.Problems, this.Output, this.Diagnostics);
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
				this.Intelligence,
				null,
				this.Diagnostics);
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

		public LiveDiagnosticsService Diagnostics { get; }

		public CodeIntelligenceOperations Intelligence { get; }

		public WorkspaceService Workspace { get; }

		private static void CopyDescriptions(String target)
		{
			String source = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions");
			Directory.CreateDirectory(target);
			foreach (String name in new[] { "opcodes.txt", "functions.ini" })
			{
				File.Copy(Path.Combine(source, name), Path.Combine(target, name), true);
			}
		}

		/// <summary>Writes a file below the scratch folder with an old time stamp (so that the include cache keeps it).</summary>
		public String Write(String relativePath, String text)
		{
			String path = Path.GetFullPath(this.Folder.Write(relativePath, text));
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
			return path;
		}

		/// <summary>Opens a file as a document, parses it, and waits until the analysis that follows the parse has ended.</summary>
		public async Task<DocumentViewModel> OpenAsync(String path)
		{
			DocumentViewModel? document = await this.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = false });
			document = document ?? throw new InvalidOperationException("The file was not opened.");
			await this.Workspace.RequestParseAsync(document, true);
			await this.WaitAsync(document);
			return document;
		}

		/// <summary>Waits until the analysis of a document (if one is pending) has ended.</summary>
		public async Task WaitAsync(DocumentViewModel document)
		{
			for (Int32 attempt = 0; attempt < 20; attempt++)
			{
				Task? pending = this.Diagnostics.GetPending(document);
				if (pending is null)
				{
					return;
				}

				await pending;
			}
		}

		/// <summary>Gets the problems of the live diagnostics.</summary>
		public Problem[] Findings()
		{
			return this.Problems.Problems.Where(problem => problem.Origin == ProblemOrigins.LiveDiagnostics).ToArray();
		}

		public void Dispose()
		{
			this.Diagnostics.Dispose();
			this.Symbols.Dispose();
			this.Index.Dispose();
			this.Projects.Dispose();
			this.Folder.Dispose();
		}
	}
}