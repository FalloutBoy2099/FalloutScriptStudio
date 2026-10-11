namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>The services of an editor under test: real settings in a temporary folder, a fixed theme and a recording workspace.</summary>
	public sealed class EditorTestEnvironment : IDisposable
	{
		private readonly String _directory;
		private readonly Boolean _ownsDirectory;

		public EditorTestEnvironment(Boolean dark = false, String? baseDirectory = null, IUserMacroSource? userMacros = null)
		{
			this._ownsDirectory = baseDirectory is null;
			this._directory = baseDirectory ?? Path.Combine(Path.GetTempPath(), "fss-editor-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this._directory);

			this.SettingsService = new SettingsService(
				AppPaths.FromBaseDirectory(this._directory),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			this.SettingsService.Settings.Editor.TabsToSpaces = true;
			this.SettingsService.Settings.Editor.TabSize = 3;

			this.Theme = new FixedThemeService(dark);
			(IWorkspace workspace, RecordingWorkspace recorder) = RecordingWorkspace.Create();
			this.Workspace = workspace;
			this.Recorder = recorder;
			this.CollapsedStore = new InMemoryCollapsedProcedureStore();
			this.Services = new ScriptEditorServices(this.SettingsService, this.Theme, this.Workspace, this.CollapsedStore, userMacros);
		}

		public SettingsService SettingsService { get; }

		public FixedThemeService Theme { get; }

		public IWorkspace Workspace { get; }

		public RecordingWorkspace Recorder { get; }

		public InMemoryCollapsedProcedureStore CollapsedStore { get; }

		public ScriptEditorServices Services { get; }

		/// <summary>Creates a document with a parse result produced by the internal parser.</summary>
		/// <param name="text">The text.</param>
		/// <param name="path">The file path, or <see langword="null"/> for an unsaved document.</param>
		/// <param name="parse">Whether to fill <c>ProgramInfo</c>.</param>
		/// <returns>The document.</returns>
		public DocumentViewModel CreateDocument(String text, String? path = "C:\\scripts\\test.ssl", Boolean parse = true)
		{
			TextDocument document = new TextDocument(text);
			document.UndoStack.ClearAll();
			DocumentViewModel model = new DocumentViewModel(path, DocumentViewModel.KindFromPath(path ?? DocumentViewModel.UnsavedFileName), document);
			if (parse && model.IsParsable)
			{
				model.ProgramInfo = new InternalParser().Parse(text, path);
			}

			model.IsModified = false;
			return model;
		}

		/// <summary>Creates an editor session on a fresh <see cref="TextEditor"/>; call on the UI thread.</summary>
		/// <param name="document">The document.</param>
		/// <returns>The started session.</returns>
		public EditorSession StartSession(DocumentViewModel document)
		{
			TextEditor editor = new TextEditor();
			EditorSession session = new EditorSession(editor, document, this.Services, () => { });
			session.Start();
			editor.Measure(new System.Windows.Size(900, 600));
			editor.Arrange(new System.Windows.Rect(0, 0, 900, 600));
			editor.UpdateLayout();
			return session;
		}

		public void Dispose()
		{
			if (!this._ownsDirectory)
			{
				return;
			}

			try
			{
				Directory.Delete(this._directory, true);
			}
			catch (IOException)
			{
			}
		}
	}
}
