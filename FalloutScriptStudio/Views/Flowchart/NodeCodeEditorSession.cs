namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.ComponentModel;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Controllers;
	using FalloutScriptStudio.Editor.Text;

	/// <summary>
	/// Makes an AvalonEdit control the code editor of one dialogue node: SSL highlighting with the names of the script, brackets,
	/// tips, auto-completion, keyword expansion and smart indentation, using the controllers of the script editor. The code lives
	/// in a private document that is never parsed by the workspace; it only borrows the parse result of the script it belongs to.
	/// </summary>
	internal sealed class NodeCodeEditorSession : IDisposable
	{
		private readonly TextEditor _editor;
		private readonly DocumentViewModel _script;
		private readonly DocumentViewModel _host;
		private readonly EditorContext _context;
		private readonly AppearanceController _appearance;
		private readonly HighlightingController _highlighting;
		private readonly BracketController _brackets;
		private readonly DiagnosticsController _diagnostics;
		private readonly TipController _tips;
		private readonly CompletionController _completion;
		private readonly SslIndentationStrategy _indentation;
		private readonly InputController _input;
		private Boolean _started;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeCodeEditorSession"/> class.
		/// </summary>
		/// <param name="editor">The control.</param>
		/// <param name="code">The document with the code of the node.</param>
		/// <param name="script">The script the node belongs to; its parse result is used for the highlighting and the tips.</param>
		/// <param name="services">The shared services of the script editor.</param>
		public NodeCodeEditorSession(TextEditor editor, TextDocument code, DocumentViewModel script, ScriptEditorServices services)
		{
			this._editor = editor;
			this._script = script;
			this._host = new DocumentViewModel(script.FilePath, DocumentKind.Script, code) { ProgramInfo = script.ProgramInfo };
			this._context = new EditorContext(editor, this._host, services);
			this._appearance = new AppearanceController(this._context);
			this._highlighting = new HighlightingController(this._context);
			this._brackets = new BracketController(this._context);
			this._diagnostics = new DiagnosticsController(this._context);
			this._tips = new TipController(this._context, this._diagnostics);
			this._completion = new CompletionController(this._context);
			this._indentation = new SslIndentationStrategy(() => this._context.IndentUnit);
			this._input = new InputController(this._context, this._completion, this._tips, this._indentation);
		}

		/// <summary>Starts the controllers; the control must have been created.</summary>
		public void Start()
		{
			if (this._started)
			{
				return;
			}

			this._started = true;
			this._editor.Document = this._host.TextDocument;
			this._editor.TextArea.IndentationStrategy = this._indentation;
			this._appearance.Applied += this.OnAppearanceApplied;
			this._appearance.Attach();
			this._highlighting.Attach();
			this._diagnostics.Attach();
			this._brackets.Attach();
			this._tips.Attach();
			this._completion.Attach();
			this._input.Attach();
			this._script.PropertyChanged += this.OnScriptPropertyChanged;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!this._started)
			{
				return;
			}

			this._started = false;
			this._script.PropertyChanged -= this.OnScriptPropertyChanged;
			this._appearance.Applied -= this.OnAppearanceApplied;
			this._input.Dispose();
			this._completion.Dispose();
			this._tips.Dispose();
			this._brackets.Dispose();
			this._diagnostics.Dispose();
			this._highlighting.Dispose();
			this._appearance.Dispose();
			this._context.Dispose();
			this._editor.Document = new TextDocument();
		}

		private void OnAppearanceApplied(Object? sender, EventArgs e)
		{
			this._highlighting.Update(true);
			this._diagnostics.RefreshPalette();
			this._brackets.RefreshPalette();
		}

		private void OnScriptPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(DocumentViewModel.ProgramInfo))
			{
				this._host.ProgramInfo = this._script.ProgramInfo;
				this._highlighting.Update();
			}
		}
	}
}