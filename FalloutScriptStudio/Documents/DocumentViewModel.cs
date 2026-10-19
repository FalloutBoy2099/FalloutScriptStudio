namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using CommunityToolkit.Mvvm.ComponentModel;
	using ICSharpCode.AvalonEdit.Document;
	using Core.Analysis;
	using Core.Compilation;
	using Core.Parsing;

	/// <summary>
	/// State of one open editor tab (legacy <c>TabInfo</c>). The AvalonEdit <see cref="TextDocument"/> is the single
	/// source of truth for the text; the editor view binds to it.
	/// </summary>
	public sealed partial class DocumentViewModel : ObservableObject
	{
		/// <summary>The title of a document that was never saved.</summary>
		public const String UnsavedFileName = "unsaved.ssl";

		public DocumentViewModel(String? filePath, DocumentKind kind, TextDocument textDocument)
		{
			this.FilePath = filePath;
			this.Kind = kind;
			this.TextDocument = textDocument;
			this.TextDocument.UndoStack.PropertyChanged += (_, _) => this.TextDocumentUndoChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Raised when the undo / redo availability of the document may have changed.</summary>
		public event EventHandler? TextDocumentUndoChanged;

		/// <summary>Gets the text buffer.</summary>
		public TextDocument TextDocument { get; }

		/// <summary>Gets the view state of the editor (caret, scroll, folds, history) that survives the destruction of the editor view.</summary>
		public EditorViewState ViewState { get; } = new EditorViewState();

		/// <summary>Gets or sets the editor control of this document (set by the view when it is loaded).</summary>
		[ObservableProperty]
		public partial IEditorSurface? Editor { get; set; }

		/// <summary>Full path of the file; <see langword="null"/> for a new document that was never saved.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(FileName), nameof(Title), nameof(HasFile))]
		public partial String? FilePath { get; set; }

		[ObservableProperty]
		public partial DocumentKind Kind { get; set; }

		/// <summary>True when the text differs from the file on disk.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Title))]
		public partial Boolean IsModified { get; set; }

		/// <summary>Encoding used to read and to save the file.</summary>
		[ObservableProperty]
		public partial Encoding? Encoding { get; set; }

		/// <summary>Last write time of the file at load / save time, used to detect external changes.</summary>
		public DateTime LastWriteTimeUtc { get; set; }

		/// <summary>Result of the last successful or fall-back parse (scripts and headers only).</summary>
		[ObservableProperty]
		public partial ProgramInfo? ProgramInfo { get; set; }

		/// <summary>Short status of the parser for the status bar; its text is computed in the language in use when it is read.</summary>
		[ObservableProperty]
		public partial ParserStatus ParserStatus { get; set; } = ParserStatus.None;

		/// <summary>Full parser log text.</summary>
		[ObservableProperty]
		public partial String ParserLog { get; set; } = String.Empty;

		/// <summary>Errors / warnings found by the parser (shown in the Errors tab and underlined in the editor).</summary>
		[ObservableProperty]
		public partial IReadOnlyList<BuildError> ParserErrors { get; set; } = Array.Empty<BuildError>();

		/// <summary>
		/// Findings of the semantic analysis of the script (a wrong number of arguments, an unknown name...), refreshed in the background after
		/// each parse by <c>LiveDiagnosticsService</c>; underlined in the editor. Empty when the analysis is off or has not run yet.
		/// </summary>
		[ObservableProperty]
		public partial IReadOnlyList<ScriptDiagnostic> LiveDiagnostics { get; set; } = Array.Empty<ScriptDiagnostic>();

		/// <summary>Gets or sets the version of the text that <see cref="LiveDiagnostics"/> were made for; the places of the findings are only valid for that text.</summary>
		public ITextSourceVersion? LiveDiagnosticsVersion { get; set; }

		/// <summary>Full output of the last build of this document.</summary>
		[ObservableProperty]
		public partial String BuildLog { get; set; } = String.Empty;

		/// <summary>Errors / warnings of the last build.</summary>
		[ObservableProperty]
		public partial IReadOnlyList<BuildError> BuildErrors { get; set; } = Array.Empty<BuildError>();

		/// <summary>True while this document is the selected tab (maintained by the workspace; used to show only the active editor view).</summary>
		[ObservableProperty]
		public partial Boolean IsActive { get; set; }

		/// <summary>True when the text changed since the last parse and a new parse is pending.</summary>
		[ObservableProperty]
		public partial Boolean NeedsParse { get; set; }

		/// <summary>Path of the <c>.msg</c> file associated with this script, if any.</summary>
		[ObservableProperty]
		public partial String? MessageFilePath { get; set; }

		/// <summary>Gets the file name without directory, or <see cref="UnsavedFileName"/>.</summary>
		public String FileName => this.FilePath is null ? UnsavedFileName : Path.GetFileName(this.FilePath);

		/// <summary>Gets the tab title (file name, with a trailing <c>*</c> when modified).</summary>
		public String Title => this.IsModified ? this.FileName + " *" : this.FileName;

		public Boolean HasFile => this.FilePath is not null;

		/// <summary>True for files that are parsed for procedures / macros (scripts and headers).</summary>
		public Boolean IsParsable => this.Kind is DocumentKind.Script or DocumentKind.Header;

		public Boolean CanUndo => this.TextDocument.UndoStack.CanUndo;

		public Boolean CanRedo => this.TextDocument.UndoStack.CanRedo;

		partial void OnKindChanged(DocumentKind value)
		{
			this.OnPropertyChanged(nameof(this.IsParsable));
		}

		/// <summary>Determines the document kind from a file extension.</summary>
		public static DocumentKind KindFromPath(String? path)
		{
			return Path.GetExtension(path ?? UnsavedFileName).ToLowerInvariant() switch
			{
				".ssl" => DocumentKind.Script,
				".h" => DocumentKind.Header,
				".msg" => DocumentKind.Message,
				_ => DocumentKind.Other,
			};
		}
	}
}