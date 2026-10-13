namespace FalloutScriptStudio.Tests.Shell.Support
{
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>Builds a <see cref="WorkspaceService"/> with its collaborators wired to the given fakes (the composition root does the same with DI).</summary>
	internal static class WorkspaceServiceFactory
	{
		public static WorkspaceService Create(
			ISettingsService settings,
			ParseCoordinator parser,
			IScriptCompiler compiler,
			IDatArchiveExtractor archives,
			IDialogService dialogs,
			IOutputSink output,
			IMessageEditorService messageEditor,
			IScriptToolService scriptTools,
			IDialogueDialogsService dialogueDialogs,
			IProblemService? problems = null,
			IOperationTracker? operations = null,
			ProjectSymbolService? symbols = null,
			CodeIntelligenceOperations? intelligence = null)
		{
			DocumentFileService files = new DocumentFileService(settings, dialogs, NullLogger<DocumentFileService>.Instance, null, problems);
			return new WorkspaceService(
				settings,
				parser,
				dialogs,
				output,
				new SymbolRenameService(new RefactoringService(), dialogs, scriptTools, dialogueDialogs, settings),
				files,
				new ScriptDecompilationService(settings, compiler, dialogs, output, files, operations, problems),
				new ArchiveScriptExtractor(settings, archives, dialogs, problems),
				new ExternalChangeDetector(dialogs),
				new MessageAssociationService(settings, new MessageFileService(NullLogger<MessageFileService>.Instance), dialogs, messageEditor, NullLogger<MessageAssociationService>.Instance, problems),
				new ProcedureOperations(settings, parser, dialogs, new ProcedureTextService(), scriptTools),
				new ReferenceResultsPresenter(dialogs, output, settings),
				scriptTools,
				null,
				problems,
				symbols,
				intelligence);
		}
	}
}
