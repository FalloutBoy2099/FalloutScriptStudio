namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>
	/// The view model of the dialogue flowchart window (legacy <c>NodeDiagram</c>): builds the nodes of a script on a
	/// <see cref="DiagramCanvas"/>, keeps them in step with the script, saves and loads the layout, and runs the commands of the
	/// toolbar and the context menu.
	/// </summary>
	public sealed partial class FlowchartViewModel : ObservableObject, IDisposable
	{
		/// <summary>The name of the procedure that starts a dialogue; it is a node that cannot be renamed or deleted.</summary>
		public const String TalkProcedure = "talk_p_proc";

		private const String FileFilterPattern = "*.fcd";
		private const String ImageFilterPattern = "*.png";
		private const Int32 NewFlowchartHintSeconds = 10;
		private const Int32 OutdatedHintSeconds = 5;

		private readonly IWorkspace _workspace;
		private readonly LanguageChangedSubscription _languageSubscription;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly IFlowchartBuilder _builder;
		private readonly IFlowchartDocumentStore _store;
		private readonly IDialogueMessageService _messages;
		private readonly DialogueScriptEditor _scriptEditor;
		private readonly IFlowchartDialogService _flowchartDialogs;
		private readonly INodeCodeEditorService _nodeEditors;
		private readonly IScriptToolService _tools;
		private DocumentViewModel? _script;
		private DiagramCanvas? _canvas;
		private FlowchartModel _model = FlowchartModel.Empty;
		private String? _fcdPath;
		private Boolean _loaded;
		private Boolean _loadingOptions;
		private Boolean _syncingZoom;
		private Boolean _busy;
		private Int32 _selfChange;
		private Int32 _hintVersion;

		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartViewModel"/> class.
		/// </summary>
		/// <param name="workspace">Parses the script and reports its changes.</param>
		/// <param name="settings">Stores the options.</param>
		/// <param name="dialogs">Shows messages and file pickers.</param>
		/// <param name="builder">Builds the nodes from the script.</param>
		/// <param name="store">Reads and writes flowchart files.</param>
		/// <param name="messages">Reads and writes message files.</param>
		/// <param name="scriptEditor">Changes the nodes in the script.</param>
		/// <param name="flowchartDialogs">Shows the small dialogs of the flowchart.</param>
		/// <param name="nodeEditors">Opens the node code editors.</param>
		/// <param name="tools">Opens the function rules configuration.</param>
		public FlowchartViewModel(
			IWorkspace workspace,
			ISettingsService settings,
			IDialogService dialogs,
			IFlowchartBuilder builder,
			IFlowchartDocumentStore store,
			IDialogueMessageService messages,
			DialogueScriptEditor scriptEditor,
			IFlowchartDialogService flowchartDialogs,
			INodeCodeEditorService nodeEditors,
			IScriptToolService tools)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(builder);
			ArgumentNullException.ThrowIfNull(store);
			ArgumentNullException.ThrowIfNull(messages);
			ArgumentNullException.ThrowIfNull(scriptEditor);
			ArgumentNullException.ThrowIfNull(flowchartDialogs);
			ArgumentNullException.ThrowIfNull(nodeEditors);
			ArgumentNullException.ThrowIfNull(tools);
			this._workspace = workspace;
			this._settings = settings;
			this._dialogs = dialogs;
			this._builder = builder;
			this._store = store;
			this._messages = messages;
			this._scriptEditor = scriptEditor;
			this._flowchartDialogs = flowchartDialogs;
			this._nodeEditors = nodeEditors;
			this._tools = tools;

			this.LoadOptions();
			this._workspace.DocumentParsed += this.OnDocumentParsed;
			this._nodeEditors.CodeApplied += this.OnNodeCodeApplied;
			this._nodeEditors.EditorsChanged += this.OnEditorsChanged;
			this._languageSubscription = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.OnLanguageChanged());
		}

		/// <summary>Gets the script the flowchart shows, or <see langword="null"/> before <see cref="Attach"/>.</summary>
		public DocumentViewModel? Script => this._script;

		/// <summary>Gets the canvas the flowchart is drawn on, or <see langword="null"/> before <see cref="Attach"/>.</summary>
		public DiagramCanvas? Canvas => this._canvas;

		/// <summary>Gets the path of the flowchart file that is saved to, or <see langword="null"/> while there is none.</summary>
		public String? FilePath => this._fcdPath;

		/// <summary>Gets the nodes of the script as the last build found them.</summary>
		public FlowchartModel Model => this._model;

		/// <summary>
		/// Gets a value indicating whether the script changed since the nodes were built, so the chart is out of date.
		/// </summary>
		public Boolean NeedUpdate { get; private set; }

		/// <summary>Gets or sets the text of the window title.</summary>
		[ObservableProperty]
		public partial String Title { get; set; } = Loc.Get("Str.Flowchart.Title");

		/// <summary>Gets or sets the zoom of the canvas in percent.</summary>
		[ObservableProperty]
		public partial Int32 ZoomPercent { get; set; } = 100;

		/// <summary>Gets or sets whether the code lines are shown in the nodes that have no setting of their own.</summary>
		[ObservableProperty]
		public partial Boolean ShowAllCode { get; set; } = true;

		/// <summary>Gets or sets whether commented code is shown in the nodes.</summary>
		[ObservableProperty]
		public partial Boolean ShowCommentCode { get; set; }

		/// <summary>Gets or sets whether only the links of the selected nodes are drawn.</summary>
		[ObservableProperty]
		public partial Boolean LinksOnlyForSelection { get; set; }

		/// <summary>Gets or sets whether the nodes are updated when the window is activated after the script changed.</summary>
		[ObservableProperty]
		public partial Boolean AutoUpdate { get; set; }

		/// <summary>Gets or sets whether the layout is saved when the window closes.</summary>
		[ObservableProperty]
		public partial Boolean AutoSaveChart { get; set; }

		/// <summary>Gets or sets whether a new flowchart is arranged automatically.</summary>
		[ObservableProperty]
		public partial Boolean AutoArrange { get; set; }

		/// <summary>Gets or sets whether the exit node is left out of the chart.</summary>
		[ObservableProperty]
		public partial Boolean WithoutExitNode { get; set; }

		/// <summary>Gets or sets whether unused nodes are hidden when a flowchart is created.</summary>
		[ObservableProperty]
		public partial Boolean AutoHideNodes { get; set; }

		/// <summary>Gets or sets whether shadows and smooth edges are drawn.</summary>
		[ObservableProperty]
		public partial Boolean HighQuality { get; set; } = true;

		/// <summary>Gets or sets whether the nodes are drawn with reduced detail.</summary>
		[ObservableProperty]
		public partial Boolean LowDetails { get; set; }

		/// <summary>Gets or sets the message shown over the canvas.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsHintVisible))]
		public partial String Hint { get; set; } = String.Empty;

		/// <summary>Gets a value indicating whether a hint is shown.</summary>
		public Boolean IsHintVisible => this.Hint.Length > 0;

		/// <summary>Gets the percent text next to the zoom slider.</summary>
		public String ZoomText => this.ZoomPercent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

		/// <summary>
		/// Connects the view model to its script and canvas. Call <see cref="LoadAsync"/> afterwards.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="canvas">The canvas.</param>
		public void Attach(DocumentViewModel script, DiagramCanvas canvas)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(canvas);

			this._script = script;
			this._canvas = canvas;
			this.RefreshTitle();
			canvas.HighQuality = this.HighQuality;
			canvas.LowDetail = this.LowDetails;
			canvas.LinksOnlyForSelection = this.LinksOnlyForSelection;
			canvas.ZoomChanged += this.OnCanvasZoomChanged;
			this.SyncZoomFromCanvas();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this._workspace.DocumentParsed -= this.OnDocumentParsed;
			this._nodeEditors.CodeApplied -= this.OnNodeCodeApplied;
			this._nodeEditors.EditorsChanged -= this.OnEditorsChanged;
			this._languageSubscription.Dispose();
			if (this._canvas is not null)
			{
				this._canvas.ZoomChanged -= this.OnCanvasZoomChanged;
			}
		}

		/// <summary>Shows a message over the canvas for a few seconds.</summary>
		/// <param name="text">The message.</param>
		/// <param name="seconds">How long it stays.</param>
		public void ShowHint(String text, Int32 seconds)
		{
			ArgumentNullException.ThrowIfNull(text);
			this._hintVersion++;
			this.Hint = text;
			_ = this.HideHintLaterAsync(this._hintVersion, seconds);
		}

		/// <summary>Hides the hint.</summary>
		[RelayCommand]
		public void DismissHint()
		{
			this._hintVersion++;
			this.Hint = String.Empty;
		}

		partial void OnZoomPercentChanged(Int32 value)
		{
			this.OnPropertyChanged(nameof(this.ZoomText));
			if (this._canvas is not null && !this._syncingZoom)
			{
				this._canvas.Zoom = value / 100.0;
			}
		}

		partial void OnShowAllCodeChanged(Boolean value)
		{
			if (this._canvas is null)
			{
				return;
			}

			foreach (DiagramNode node in this._canvas.Nodes.Where(n => !n.HasUserShowCodeSetting))
			{
				node.ShowCodeRows = value;
			}
		}

		partial void OnShowCommentCodeChanged(Boolean value)
		{
			if (this._script is not null)
			{
				_ = this.UpdateAsync();
			}
		}

		partial void OnLinksOnlyForSelectionChanged(Boolean value)
		{
			if (this._canvas is not null)
			{
				this._canvas.LinksOnlyForSelection = value;
			}
		}

		partial void OnHighQualityChanged(Boolean value)
		{
			if (this._canvas is not null)
			{
				this._canvas.HighQuality = value;
			}
		}

		partial void OnLowDetailsChanged(Boolean value)
		{
			if (this._canvas is not null)
			{
				this._canvas.LowDetail = value;
			}
		}

		partial void OnAutoUpdateChanged(Boolean value)
		{
			this.StoreOption(settings => settings.AutoUpdate = value);
		}

		partial void OnAutoSaveChartChanged(Boolean value)
		{
			this.StoreOption(settings => settings.AutoSaveChart = value);
		}

		partial void OnAutoArrangeChanged(Boolean value)
		{
			this.StoreOption(settings => settings.AutoArrange = value);
		}

		partial void OnWithoutExitNodeChanged(Boolean value)
		{
			this.StoreOption(settings => settings.WithoutExitNode = value);
		}

		partial void OnAutoHideNodesChanged(Boolean value)
		{
			this.StoreOption(settings => settings.AutoHideNodes = value);
		}

		private void LoadOptions()
		{
			FlowchartSettings options = this._settings.Settings.Flowchart;
			this._loadingOptions = true;
			try
			{
				this.AutoUpdate = options.AutoUpdate;
				this.AutoSaveChart = options.AutoSaveChart;
				this.AutoArrange = options.AutoArrange;
				this.WithoutExitNode = options.WithoutExitNode;
				this.AutoHideNodes = options.AutoHideNodes;
			}
			finally
			{
				this._loadingOptions = false;
			}
		}

		private void StoreOption(Action<FlowchartSettings> change)
		{
			if (!this._loadingOptions)
			{
				this._settings.Update(settings => change(settings.Flowchart));
			}
		}

		private async Task HideHintLaterAsync(Int32 version, Int32 seconds)
		{
			await Task.Delay(TimeSpan.FromSeconds(seconds));
			if (version == this._hintVersion)
			{
				this.Hint = String.Empty;
			}
		}

		private static String CreateFilter(String descriptionKey, String pattern)
		{
			return Loc.Get(descriptionKey) + "|" + pattern;
		}

		private void RefreshTitle()
		{
			this.Title = this._script is null ? Loc.Get("Str.Flowchart.Title") : Loc.Format("Str.Flowchart.TitleFor", this._script.FileName);
		}

		private void OnLanguageChanged()
		{
			this.RefreshTitle();
			if (this._canvas is not null)
			{
				foreach (DiagramNode node in this._canvas.Nodes)
				{
					FlowchartDiagramMapper.RefreshCaption(node);
				}
			}
		}

		private DocumentViewModel RequireScript()
		{
			return this._script ?? throw new InvalidOperationException("The view model is not attached to a script.");
		}

		private DiagramCanvas RequireCanvas()
		{
			return this._canvas ?? throw new InvalidOperationException("The view model is not attached to a canvas.");
		}

		private void SyncZoomFromCanvas()
		{
			if (this._canvas is null)
			{
				return;
			}

			this._syncingZoom = true;
			try
			{
				this.ZoomPercent = (Int32)Math.Round(this._canvas.Zoom * 100.0);
			}
			finally
			{
				this._syncingZoom = false;
			}
		}

		private void OnCanvasZoomChanged(Object? sender, EventArgs e)
		{
			this.SyncZoomFromCanvas();
		}

		private void OnDocumentParsed(Object? sender, DocumentViewModel document)
		{
			if (ReferenceEquals(document, this._script) && this._selfChange == 0)
			{
				this.NeedUpdate = true;
			}
		}

		private void OnNodeCodeApplied(Object? sender, NodeEditorEventArgs e)
		{
			if (ReferenceEquals(e.Script, this._script))
			{
				_ = this.UpdateAsync();
			}
		}

		private void OnEditorsChanged(Object? sender, NodeEditorEventArgs e)
		{
			if (ReferenceEquals(e.Script, this._script))
			{
				this.RefreshEditingMarks();
			}
		}

		private void RefreshEditingMarks()
		{
			if (this._canvas is null || this._script is null)
			{
				return;
			}

			foreach (DiagramNode node in this._canvas.Nodes)
			{
				node.IsEditing = this._nodeEditors.IsEditing(this._script, node.Name);
			}
		}
	}
}
