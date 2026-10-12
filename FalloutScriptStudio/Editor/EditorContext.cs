namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows.Media;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// Everything the feature controllers of one editor view share: the AvalonEdit control, the document view model,
	/// the application services and a few derived facts.
	/// </summary>
	public sealed class EditorContext : IDisposable
	{
		private readonly Boolean _ownsLexical;

		public EditorContext(TextEditor editor, DocumentViewModel document, ScriptEditorServices services)
			: this(editor, document, services, null)
		{
		}

		private EditorContext(TextEditor editor, DocumentViewModel document, ScriptEditorServices services, DocumentLexicalMap? sharedLexical)
		{
			this.Editor = editor;
			this.DocumentModel = document;
			this.Services = services;
			this._ownsLexical = sharedLexical is null;
			this.Lexical = sharedLexical ?? new DocumentLexicalMap(document.TextDocument);
		}

		public TextEditor Editor { get; }

		/// <summary>Gets the other editor controls that show the same document (the second pane of a split view).</summary>
		public List<TextEditor> LinkedEditors { get; } = new List<TextEditor>();

		/// <summary>Gets the editor and all linked editors.</summary>
		public IEnumerable<TextEditor> AllEditors => new[] { this.Editor }.Concat(this.LinkedEditors);

		/// <summary>
		/// Creates the context of another editor control that shows the same document (the second pane of a split view). It shares the
		/// document, the services and the lexical map with this context, which stays the owner of the map.
		/// </summary>
		/// <param name="editor">The other control.</param>
		/// <returns>The context of the other pane.</returns>
		public EditorContext CreatePaneContext(TextEditor editor)
		{
			return new EditorContext(editor, this.DocumentModel, this.Services, this.Lexical);
		}

		public DocumentViewModel DocumentModel { get; }

		public ScriptEditorServices Services { get; }

		/// <summary>Gets the lazily rebuilt lexical map of the document.</summary>
		public DocumentLexicalMap Lexical { get; }

		public TextArea TextArea => this.Editor.TextArea;

		public TextView TextView => this.Editor.TextArea.TextView;

		public TextDocument Document => this.DocumentModel.TextDocument;

		public EditorOptions Options => this.Services.Options;

		public EditorPalette Palette => this.Services.Palette;

		public EditorViewState ViewState => this.DocumentModel.ViewState;

		public DocumentKind Kind => this.DocumentModel.Kind;

		/// <summary>Gets a value indicating whether the document is an SSL script or header (parsed, folded, completed).</summary>
		public Boolean IsScript => this.DocumentModel.IsParsable;

		public Boolean IsMessage => this.DocumentModel.Kind == DocumentKind.Message;

		public ProgramInfo? ProgramInfo => this.DocumentModel.ProgramInfo;

		/// <summary>Gets the name used to identify the script in the collapsed-procedure store.</summary>
		public String ScriptName => this.DocumentModel.FileName;

		/// <summary>Gets one indentation level as configured in the editor options.</summary>
		public String IndentUnit => this.Editor.Options.IndentationString;

		/// <summary>Gets the line break sequence of the document.</summary>
		public String NewLine => this.Document.LineCount > 0
			? TextUtilities.GetNewLineFromDocument(this.Document, Math.Min(this.TextArea.Caret.Line, this.Document.LineCount))
			: Environment.NewLine;

		/// <summary>Gets the editor font size in device independent pixels.</summary>
		public Double FontSize => this.Editor.FontSize;

		public FontFamily FontFamily => this.Editor.FontFamily;

		/// <summary>Runs a workspace operation without waiting for it; failures are reported in the status bar and the list of problems.</summary>
		/// <param name="operation">The operation.</param>
		public async void RunInBackground(Func<Task> operation)
		{
			try
			{
				await operation();
			}
			catch (Exception ex)
			{
				this.Services.Workspace.ReportFailure(ex.Message);
			}
		}

		public void Dispose()
		{
			if (this._ownsLexical)
			{
				this.Lexical.Dispose();
			}
		}
	}
}
