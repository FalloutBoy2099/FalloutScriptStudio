namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.Workspace;

	/// <summary>Everything a flowchart test needs: the real Core services, fakes for the application services, and a script on disk.</summary>
	internal sealed class FlowchartHarness : IDisposable
	{
		public FlowchartHarness(String? scriptText = null, Boolean withoutExitNode = false, IDialogueQualityAnalyzer? quality = null)
		{
			this.Quality = quality;
			this.Folder = new ScriptToolsTempDirectory();
			this.Settings = new FakeSettingsService(this.Folder.Path);
			this.Settings.Settings.Flowchart.WithoutExitNode = withoutExitNode;

			(IWorkspace workspace, TestWorkspace recorder) = TestWorkspace.Create();
			this.Workspace = workspace;
			this.WorkspaceRecorder = recorder;
			(IScriptToolService tools, ScriptToolsStub toolRecorder) = ScriptToolsStub.Create();
			this.Tools = tools;
			this.ToolRecorder = toolRecorder;

			DialogueNodeParser parser = new DialogueNodeParser(new DialogueFunctionRules());
			ProcedureTextService procedures = new ProcedureTextService();
			this.Procedures = procedures;
			this.Parser = parser;
			this.Builder = new FlowchartBuilder(parser, procedures);
			this.Store = new FlowchartDocumentStore();
			this.ScriptEditor = new DialogueScriptEditor(procedures, parser, new DialogueNodeFactory(procedures), workspace);
			this.NodeEditor = new DialogueNodeEditor(parser);

			String path = this.Folder.Write(Path.Combine("scripts", "dcjohn.ssl"), String.Empty);
			this.Script = FlowchartSamples.CreateDocument(scriptText, path);
			this.Canvas = new DiagramCanvas();
			this.ViewModel = this.CreateViewModel();
			this.ViewModel.Attach(this.Script, this.Canvas);
		}

		public ScriptToolsTempDirectory Folder { get; }

		/// <summary>Gets the analyzer of the dialogue check the view model uses; <see langword="null"/> for a view model without marks.</summary>
		public IDialogueQualityAnalyzer? Quality { get; }

		public FakeSettingsService Settings { get; }

		public IWorkspace Workspace { get; }

		public TestWorkspace WorkspaceRecorder { get; }

		public IScriptToolService Tools { get; }

		public ScriptToolsStub ToolRecorder { get; }

		public FakeDialogs Dialogs { get; } = new FakeDialogs();

		public FakeDialogueMessageService Messages { get; } = new FakeDialogueMessageService();

		public FakeFlowchartDialogService FlowDialogs { get; } = new FakeFlowchartDialogService();

		public FakeNodeCodeEditorService Editors { get; } = new FakeNodeCodeEditorService();

		public FakeMessageEditorService MessageEditor { get; } = new FakeMessageEditorService();

		public ProcedureTextService Procedures { get; }

		public DialogueNodeParser Parser { get; }

		public IFlowchartBuilder Builder { get; }

		public FlowchartDocumentStore Store { get; }

		public DialogueScriptEditor ScriptEditor { get; }

		public DialogueNodeEditor NodeEditor { get; }

		public DocumentViewModel Script { get; }

		public DiagramCanvas Canvas { get; }

		public FlowchartViewModel ViewModel { get; }

		/// <summary>Creates another view model on the same services.</summary>
		public FlowchartViewModel CreateViewModel()
		{
			return new FlowchartViewModel(
				this.Workspace,
				this.Settings,
				this.Dialogs,
				this.Builder,
				this.Store,
				this.Messages,
				this.ScriptEditor,
				this.FlowDialogs,
				this.Editors,
				this.Tools,
				this.Quality);
		}

		/// <summary>Creates a node code editor view model on the same services.</summary>
		public NodeCodeEditorViewModel CreateNodeEditor(INodeCodeSnippetStore? snippets = null)
		{
			return new NodeCodeEditorViewModel(this.ScriptEditor, this.NodeEditor, this.Messages, this.Dialogs, this.MessageEditor, snippets);
		}

		/// <summary>Creates a dialogue preview view model on the same services.</summary>
		public DialoguePreviewViewModel CreatePreview()
		{
			return new DialoguePreviewViewModel(new DialoguePreview(this.Parser, this.Procedures), this.Workspace, this.Messages, this.MessageEditor);
		}

		/// <summary>Loads the flowchart (a new chart when there is no flowchart file).</summary>
		public async Task LoadAsync()
		{
			await this.ViewModel.LoadAsync();
		}

		public void Dispose()
		{
			this.ViewModel.Dispose();
			this.Folder.Dispose();
		}
	}
}