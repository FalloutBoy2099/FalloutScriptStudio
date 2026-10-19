namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Controllers;

	/// <summary>
	/// Gives an AvalonEdit control the look of the script editor (font, colors and SSL highlighting) so that the body of a snippet is
	/// edited like code. The text lives in a private document that is never parsed.
	/// </summary>
	internal sealed class SnippetBodyEditorSession : IDisposable
	{
		private readonly TextEditor _editor;
		private readonly EditorContext _context;
		private readonly AppearanceController _appearance;
		private readonly HighlightingController _highlighting;
		private Boolean _started;

		/// <summary>Initializes a new instance of the <see cref="SnippetBodyEditorSession"/> class.</summary>
		/// <param name="editor">The control.</param>
		/// <param name="services">The shared services of the script editor.</param>
		public SnippetBodyEditorSession(TextEditor editor, ScriptEditorServices services)
		{
			this._editor = editor;
			this.Host = new DocumentViewModel(null, DocumentKind.Script, new TextDocument());
			this._context = new EditorContext(editor, this.Host, services);
			this._appearance = new AppearanceController(this._context);
			this._highlighting = new HighlightingController(this._context);
		}

		/// <summary>Gets the document the control shows.</summary>
		public DocumentViewModel Host { get; }

		/// <summary>Starts following the options and the theme of the editor.</summary>
		public void Start()
		{
			if (this._started)
			{
				return;
			}

			this._started = true;
			this._editor.Document = this.Host.TextDocument;
			this._appearance.Applied += this.OnAppearanceApplied;
			this._appearance.Attach();
			this._highlighting.Attach();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!this._started)
			{
				return;
			}

			this._started = false;
			this._appearance.Applied -= this.OnAppearanceApplied;
			this._highlighting.Dispose();
			this._appearance.Dispose();
			this._context.Dispose();
		}

		private void OnAppearanceApplied(Object? sender, EventArgs e)
		{
			this._highlighting.Update(true);
		}
	}
}