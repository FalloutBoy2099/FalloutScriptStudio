namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.ComponentModel;
	using FalloutScriptStudio.Documents;

	/// <summary>
	/// Follows the active document and the editor surface attached to it, and re-raises the events of the editor
	/// (caret moved, zoom changed, current procedure changed) so panels and the status bar need not care when an editor view is created.
	/// </summary>
	public sealed class ActiveEditorTracker : IDisposable
	{
		private readonly IWorkspace _workspace;
		private DocumentViewModel? _document;
		private IEditorSurface? _editor;

		/// <summary>Initializes the tracker and starts following the workspace.</summary>
		public ActiveEditorTracker(IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			this._workspace.ActiveDocumentChanged += this.OnActiveDocumentChanged;
			this.Attach(this._workspace.ActiveDocument);
		}

		/// <summary>Raised when the active document or its editor surface changed.</summary>
		public event EventHandler? EditorChanged;

		/// <summary>Raised when the caret of the active editor moved.</summary>
		public event EventHandler? CaretMoved;

		/// <summary>Raised when the zoom of the active editor changed.</summary>
		public event EventHandler? ZoomChanged;

		/// <summary>Raised when the procedure under the caret of the active editor changed.</summary>
		public event EventHandler? CurrentProcedureChanged;

		/// <summary>Gets the active document.</summary>
		public DocumentViewModel? Document => this._document;

		/// <summary>Gets the editor of the active document, or <see langword="null"/> while no view is shown.</summary>
		public IEditorSurface? Editor => this._editor;

		public void Dispose()
		{
			this._workspace.ActiveDocumentChanged -= this.OnActiveDocumentChanged;
			this.Detach();
		}

		private void OnActiveDocumentChanged(Object? sender, EventArgs e)
		{
			this.Detach();
			this.Attach(this._workspace.ActiveDocument);
			this.EditorChanged?.Invoke(this, EventArgs.Empty);
		}

		private void Attach(DocumentViewModel? document)
		{
			this._document = document;
			if (document is null)
			{
				return;
			}

			document.PropertyChanged += this.OnDocumentPropertyChanged;
			this.AttachEditor(document.Editor);
		}

		private void Detach()
		{
			this.DetachEditor();
			if (this._document is not null)
			{
				this._document.PropertyChanged -= this.OnDocumentPropertyChanged;
				this._document = null;
			}
		}

		private void AttachEditor(IEditorSurface? editor)
		{
			this._editor = editor;
			if (editor is null)
			{
				return;
			}

			editor.CaretMoved += this.OnCaretMoved;
			editor.ZoomChanged += this.OnZoomChanged;
			editor.CurrentProcedureChanged += this.OnCurrentProcedureChanged;
		}

		private void DetachEditor()
		{
			if (this._editor is null)
			{
				return;
			}

			this._editor.CaretMoved -= this.OnCaretMoved;
			this._editor.ZoomChanged -= this.OnZoomChanged;
			this._editor.CurrentProcedureChanged -= this.OnCurrentProcedureChanged;
			this._editor = null;
		}

		private void OnDocumentPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(DocumentViewModel.Editor) && this._document is not null)
			{
				this.DetachEditor();
				this.AttachEditor(this._document.Editor);
				this.EditorChanged?.Invoke(this, EventArgs.Empty);
				this.CaretMoved?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnCaretMoved(Object? sender, EventArgs e)
		{
			this.CaretMoved?.Invoke(this, EventArgs.Empty);
		}

		private void OnZoomChanged(Object? sender, EventArgs e)
		{
			this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		}

		private void OnCurrentProcedureChanged(Object? sender, EventArgs e)
		{
			this.CurrentProcedureChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
