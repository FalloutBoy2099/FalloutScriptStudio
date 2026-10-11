namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Editor.Rendering;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>Underlines the parser errors and warnings of the document (<c>DocumentViewModel.ParserErrors</c>).</summary>
	public sealed class DiagnosticsController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly DiagnosticRenderer _renderer;
		private readonly List<TextView> _views = new List<TextView>();

		public DiagnosticsController(EditorContext context)
		{
			this._context = context;
			this._renderer = new DiagnosticRenderer(context.Document, context.Palette);
		}

		/// <summary>Gets the renderer (for hit testing).</summary>
		public DiagnosticRenderer Renderer => this._renderer;

		public void Attach()
		{
			this.AddView(this._context.TextView);
			this.Update();
		}

		public void Dispose()
		{
			foreach (TextView view in this._views)
			{
				view.BackgroundRenderers.Remove(this._renderer);
			}

			this._views.Clear();
			this._renderer.Clear();
		}

		/// <summary>Shows the marks in one more text view over the same document (the second pane of a split view).</summary>
		/// <param name="view">The text view.</param>
		public void AddView(TextView view)
		{
			if (!this._views.Contains(view))
			{
				this._views.Add(view);
				view.BackgroundRenderers.Add(this._renderer);
				view.InvalidateLayer(KnownLayer.Selection);
			}
		}

		/// <summary>Stops showing the marks in a text view.</summary>
		/// <param name="view">The text view.</param>
		public void RemoveView(TextView view)
		{
			if (this._views.Remove(view))
			{
				view.BackgroundRenderers.Remove(this._renderer);
			}
		}

		/// <summary>Applies a new palette after a theme change.</summary>
		public void RefreshPalette()
		{
			this._renderer.SetPalette(this._context.Palette);
			this.InvalidateViews();
		}

		/// <summary>Rebuilds the marks from the parser errors of the document.</summary>
		public void Update()
		{
			this._renderer.Clear();
			if (!this._context.Options.HighlightParseErrors)
			{
				this.InvalidateViews();
				return;
			}

			TextDocument document = this._context.Document;
			String documentName = this._context.DocumentModel.FileName;
			Boolean isMessage = this._context.IsMessage;

			// The "error in an included header" notes come last: like in the legacy editor a line that has a mark already keeps it.
			List<BuildError> includeNotes = new List<BuildError>();
			foreach (BuildError error in this._context.DocumentModel.ParserErrors)
			{
				MarkerSeverity? severity = GetSeverity(error.Type, isMessage);
				if (severity is null || error.Line <= 0 || !AppliesTo(error, documentName))
				{
					continue;
				}

				if (severity == MarkerSeverity.IncludeError)
				{
					includeNotes.Add(error);
					continue;
				}

				this.AddMarker(document, error, severity.Value);
			}

			foreach (BuildError note in includeNotes)
			{
				DocumentLine line = document.GetLineByNumber(Math.Min(note.Line, document.LineCount));
				if (!this._renderer.HasMarkerOnLine(line))
				{
					this.AddMarker(document, note, MarkerSeverity.IncludeError);
				}
			}

			this.InvalidateViews();
		}

		private void AddMarker(TextDocument document, BuildError error, MarkerSeverity severity)
		{
			DocumentLine line = document.GetLineByNumber(Math.Min(error.Line, document.LineCount));
			GetRange(document, line, error, out Int32 start, out Int32 length);
			this._renderer.Add(new DiagnosticMarker(start, length, severity, error.Message));
		}

		private void InvalidateViews()
		{
			foreach (TextView view in this._views)
			{
				view.InvalidateLayer(KnownLayer.Selection);
			}
		}

		private static MarkerSeverity? GetSeverity(BuildErrorType type, Boolean isMessage)
		{
			return type switch
			{
				BuildErrorType.Error => MarkerSeverity.Error,
				BuildErrorType.Parser => MarkerSeverity.IncludeError,
				BuildErrorType.Warning => MarkerSeverity.Warning,
				BuildErrorType.None when isMessage => MarkerSeverity.Hint,
				_ => null,
			};
		}

		private static Boolean AppliesTo(BuildError error, String documentName)
		{
			if (String.IsNullOrEmpty(error.FileName) || String.Equals(error.FileName, BuildError.NoFile, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return String.Equals(Path.GetFileName(error.FileName), documentName, StringComparison.OrdinalIgnoreCase);
		}

		private static void GetRange(TextDocument document, DocumentLine line, BuildError error, out Int32 start, out Int32 length)
		{
			if (error.Column > 0 && error.Length > 0)
			{
				start = Math.Min(line.Offset + error.Column - 1, line.EndOffset);
				length = Math.Min(error.Length, line.EndOffset - start);
				if (length > 0)
				{
					return;
				}
			}

			String text = document.GetText(line);
			String trimmed = text.Trim();
			Int32 leading = trimmed.Length == 0 ? 0 : text.IndexOf(trimmed, StringComparison.Ordinal);
			start = line.Offset + leading;
			length = Math.Max(trimmed.Length, 1);
			length = Math.Min(length, line.EndOffset - start);
			if (length < 0)
			{
				length = 0;
			}
		}

		/// <summary>Gets the distinct messages of the marks that cover an offset.</summary>
		/// <param name="offset">The offset.</param>
		/// <returns>The marks, most severe first.</returns>
		public IReadOnlyList<DiagnosticMarker> GetMarkersAt(Int32 offset)
		{
			return this._renderer.GetMarkersAt(offset);
		}
	}
}
