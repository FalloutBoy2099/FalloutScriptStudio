namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Folding;
	using ICSharpCode.AvalonEdit.Rendering;
	using Editor.Rendering;

	/// <summary>
	/// Applies the editor options, the color theme, the font and the zoom to the AvalonEdit control and keeps them
	/// up to date while the view is loaded.
	/// </summary>
	public sealed class AppearanceController : IDisposable
	{
		private const String MessageFontFamily = "Verdana";
		private const Double MessageBasePoints = 10;
		private const Double PointsToPixels = 96.0 / 72.0;

		private readonly EditorContext _context;
		private readonly Dictionary<TextView, TabMarkerRenderer> _tabMarkers = [];
		private Boolean _attached;
		private Boolean _applyPending;

		public AppearanceController(EditorContext context)
		{
			this._context = context;
		}

		/// <summary>Raised after the appearance was applied (the highlighting and renderers refresh themselves).</summary>
		public event EventHandler? Applied;

		public void Attach()
		{
			if (this._attached)
			{
				return;
			}

			this._attached = true;
			this.Apply();
			this._context.Services.OptionsChanged += this.OnServicesChanged;
			this._context.Services.ThemeChanged += this.OnServicesChanged;
			this._context.Services.ZoomChanged += this.OnServicesChanged;
		}

		/// <summary>
		/// Detaches the controller and removes its renderers from the editor controls. It can be called more than once; an
		/// <see cref="Apply"/> that was queued before is dropped (the controls may belong to another session by then).
		/// </summary>
		public void Dispose()
		{
			this._attached = false;
			this._applyPending = false;
			this._context.Services.OptionsChanged -= this.OnServicesChanged;
			this._context.Services.ThemeChanged -= this.OnServicesChanged;
			this._context.Services.ZoomChanged -= this.OnServicesChanged;
			foreach (KeyValuePair<TextView, TabMarkerRenderer> pair in this._tabMarkers)
			{
				pair.Key.BackgroundRenderers.Remove(pair.Value);
			}

			this._tabMarkers.Clear();
		}

		/// <summary>Removes what the controller added to an editor control that is no longer shown (the second pane of a split view).</summary>
		/// <param name="editor">The control.</param>
		public void Release(TextEditor editor)
		{
			TextView textView = editor.TextArea.TextView;
			if (this._tabMarkers.Remove(textView, out TabMarkerRenderer? renderer))
			{
				textView.BackgroundRenderers.Remove(renderer);
			}
		}

		/// <summary>Applies options, theme, font and zoom to the editor and its linked (split view) editors.</summary>
		public void Apply()
		{
			if (!this._attached)
			{
				return;
			}

			foreach (TextEditor editor in this._context.AllEditors)
			{
				this.ApplyTo(editor);
			}

			this.Applied?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Applies options, theme, font and zoom to one editor control.</summary>
		/// <param name="editor">The control.</param>
		public void ApplyTo(TextEditor editor)
		{
			if (!this._attached)
			{
				return;
			}

			EditorOptions options = this._context.Options;
			EditorPalette palette = this._context.Palette;
			Boolean isMessage = this._context.IsMessage;
			TextEditorOptions editorOptions = editor.Options;

			editor.Background = palette.Background;
			editor.Foreground = palette.Foreground;
			editor.LineNumbersForeground = palette.LineNumbers;
			editor.ShowLineNumbers = !isMessage && options.ShowLineNumbers;
			editor.WordWrap = false;
			editor.IsReadOnly = false;

			this.ApplyFont(editor, options, isMessage);

			editorOptions.ConvertTabsToSpaces = !isMessage && options.TabsToSpaces;
			editorOptions.IndentationSize = options.TabSize;

			// The tab arrows are drawn by the TabMarkerRenderer in their own color; AvalonEdit draws tabs and spaces in the same one.
			editorOptions.ShowTabs = false;
			editorOptions.ShowSpaces = !isMessage && options.ShowTabCharacters;
			editorOptions.ShowEndOfLine = false;
			editorOptions.ShowBoxForControlCharacters = true;
			editorOptions.ColumnRulerPosition = options.TabSize;
			editorOptions.HighlightCurrentLine = true;
			editorOptions.EnableHyperlinks = false;
			editorOptions.EnableEmailHyperlinks = false;
			editorOptions.EnableImeSupport = true;
			editorOptions.EnableTextDragDrop = true;
			editorOptions.AllowScrollBelowDocument = false;

			// Legacy AllowCaretBeyondEOL: the caret can be put behind the end of a line of a script (not of a message file).
			editorOptions.EnableVirtualSpace = !isMessage;
			editorOptions.InheritWordWrapIndentation = false;
			editorOptions.RequireControlModifierForHyperlinkClick = true;

			TextView textView = editor.TextArea.TextView;
			textView.CurrentLineBackground = palette.CaretLine;
			textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
			textView.NonPrintableCharacterBrush = palette.WhitespaceMarker;
			textView.ColumnRulerPen = palette.VerticalRuler;
			this.ApplyTabMarkers(textView, palette, options.ShowTabCharacters);

			// Assigning the ruler pen draws the ruler whatever ShowColumnRuler says, so the option is toggled afterwards.
			Boolean showRuler = !isMessage && options.ShowVerticalRuler;
			editorOptions.ShowColumnRuler = !showRuler;
			editorOptions.ShowColumnRuler = showRuler;

			editor.TextArea.SelectionBrush = palette.Selection;
			editor.TextArea.SelectionForeground = null;
			editor.TextArea.SelectionBorder = null;
			editor.TextArea.SelectionCornerRadius = 0;

			editor.SetValue(FoldingMargin.FoldingMarkerBrushProperty, palette.FoldingMarker);
			editor.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, palette.SelectedFoldingMarker);
			editor.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, palette.Background);
			editor.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, palette.Background);
			FoldingElementGenerator.TextBrush = palette.FoldingMarker;
		}

		private void ApplyTabMarkers(TextView textView, EditorPalette palette, Boolean show)
		{
			if (!this._tabMarkers.TryGetValue(textView, out TabMarkerRenderer? renderer))
			{
				renderer = new TabMarkerRenderer();
				this._tabMarkers.Add(textView, renderer);
				textView.BackgroundRenderers.Add(renderer);
			}

			renderer.SetBrush(palette.TabMarker);
			renderer.IsEnabled = show;
			textView.InvalidateLayer(KnownLayer.Selection);
		}

		private static Double ToPixels(Double points, Int32 zoomPercent)
		{
			return Math.Max(points, 4) * PointsToPixels * zoomPercent / 100.0;
		}

		private void ApplyFont(TextEditor editor, EditorOptions options, Boolean isMessage)
		{
			Int32 zoom = this._context.Services.ZoomPercent;
			if (isMessage)
			{
				editor.FontFamily = new FontFamily(MessageFontFamily);
				editor.FontSize = ToPixels(MessageBasePoints + options.FontSizeAdjustment, zoom);
			}
			else
			{
				(FontFamily family, Double basePoints) = this._context.Services.Fonts.Resolve(options.FontIndex);
				editor.FontFamily = family;
				editor.FontSize = ToPixels(basePoints + options.FontSizeAdjustment, zoom);
			}

			System.Windows.Media.TextOptions.SetTextFormattingMode(editor, TextFormattingMode.Display);
			System.Windows.Media.TextOptions.SetTextRenderingMode(editor, TextRenderingMode.ClearType);
		}

		private void OnServicesChanged(Object? sender, EventArgs e)
		{
			// Options, theme and zoom often change together: one deferred Apply covers all of them.
			if (!this._attached || this._applyPending)
			{
				return;
			}

			this._applyPending = true;
			this._context.Editor.Dispatcher.BeginInvoke(new Action(this.ApplyDeferred));
		}

		private void ApplyDeferred()
		{
			if (!this._attached || !this._applyPending)
			{
				return;
			}

			this._applyPending = false;
			this.Apply();
		}
	}
}