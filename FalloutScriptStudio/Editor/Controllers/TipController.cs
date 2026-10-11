namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Effects;
	using System.Windows.Threading;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.Editor;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// The tooltips of the editor: hover tips for functions, macros, procedures, variables, includes, message ids and
	/// parser diagnostics, and the code tips (parameter hints) that appear while typing.
	/// </summary>
	public sealed class TipController : IDisposable
	{
		private const Double CodeTipShadowMargin = 10;

		private readonly EditorContext _context;
		private readonly DiagnosticsController _diagnostics;
		private readonly DispatcherTimer _codeTipTimer = new DispatcherTimer();
		private ToolTip? _hoverTip;
		private Popup? _codeTip;
		private Int32 _codeTipAnchor = -1;
		private Window? _ownerWindow;

		public TipController(EditorContext context, DiagnosticsController diagnostics)
		{
			this._context = context;
			this._diagnostics = diagnostics;
			this._codeTipTimer.Tick += (_, _) => this.HideCodeTip();
		}

		/// <summary>Gets the popup of the code tip (for tests), or <see langword="null"/>.</summary>
		public Popup? CodeTipPopup => this._codeTip;

		/// <summary>Gets a value indicating whether a code tip is visible.</summary>
		public Boolean IsCodeTipVisible => this._codeTip is { IsOpen: true };

		/// <summary>Gets a value indicating whether a code tip bound to a parenthesis is visible.</summary>
		public Boolean IsParameterTipVisible => this.IsCodeTipVisible && this._codeTipAnchor >= 0;

		public void Attach()
		{
			TextView view = this._context.TextView;
			view.MouseHover += this.OnMouseHover;
			view.MouseHoverStopped += this.OnMouseHoverStopped;
			view.ScrollOffsetChanged += this.OnScrollChanged;
			view.MouseLeave += this.OnMouseLeave;
			this._context.TextArea.Caret.PositionChanged += this.OnCaretPositionChanged;
			this._ownerWindow = Window.GetWindow(this._context.Editor);
			if (this._ownerWindow is not null)
			{
				this._ownerWindow.Deactivated += this.OnWindowDeactivated;
			}
		}

		public void Dispose()
		{
			TextView view = this._context.TextView;
			view.MouseHover -= this.OnMouseHover;
			view.MouseHoverStopped -= this.OnMouseHoverStopped;
			view.ScrollOffsetChanged -= this.OnScrollChanged;
			view.MouseLeave -= this.OnMouseLeave;
			this._context.TextArea.Caret.PositionChanged -= this.OnCaretPositionChanged;
			if (this._ownerWindow is not null)
			{
				this._ownerWindow.Deactivated -= this.OnWindowDeactivated;
				this._ownerWindow = null;
			}

			this.HideAll();
		}

		/// <summary>Closes the hover tip and the code tip.</summary>
		public void HideAll()
		{
			this.HideHoverTip();
			this.HideCodeTip();
		}

		/// <summary>Closes the hover tip.</summary>
		public void HideHoverTip()
		{
			if (this._hoverTip is not null)
			{
				this._hoverTip.IsOpen = false;
			}
		}

		/// <summary>Closes the code tip.</summary>
		public void HideCodeTip()
		{
			this._codeTipTimer.Stop();
			this._codeTipAnchor = -1;
			if (this._codeTip is not null)
			{
				this._codeTip.IsOpen = false;
			}
		}

		/// <summary>Builds the tips that belong to a position of the text (diagnostics first, then the symbol).</summary>
		/// <param name="offset">The offset of the character under the pointer.</param>
		/// <returns>The tips; empty when there is nothing to show.</returns>
		public IReadOnlyList<TipInfo> BuildTips(Int32 offset)
		{
			List<TipInfo> tips = new List<TipInfo>();
			IReadOnlyList<DiagnosticMarker> markers = this._diagnostics.GetMarkersAt(offset);
			foreach (IGrouping<MarkerSeverity, DiagnosticMarker> group in markers.GroupBy(m => m.Severity))
			{
				String title = group.Key switch
				{
					MarkerSeverity.Error or MarkerSeverity.IncludeError => Loc.Get("Str.Common.Error"),
					MarkerSeverity.Warning => Loc.Get("Str.Common.Warning"),
					_ => Loc.Get("Str.Editor.Tip.Note"),
				};
				TipKind kind = group.Key switch
				{
					MarkerSeverity.Error or MarkerSeverity.IncludeError => TipKind.Error,
					MarkerSeverity.Warning => TipKind.Warning,
					_ => TipKind.Hint,
				};

				// The parser reports the include note in English; the tip says it in the current language.
				String text = group.Key == MarkerSeverity.IncludeError
					? Loc.Get("Str.Editor.Tip.IncludeError")
					: String.Join(Environment.NewLine, group.Select(m => m.Message).Distinct());
				tips.Add(new TipInfo(title, text, kind));
			}

			if (this._context.Options.ShowTips && this._context.IsScript)
			{
				TipInfo? symbolTip = this.BuildSymbolTip(offset);
				if (symbolTip is not null)
				{
					tips.Add(symbolTip);
				}
			}

			return tips;
		}

		/// <summary>Shows a parameter hint for the function or procedure called in front of a parenthesis.</summary>
		/// <param name="parenOffset">The offset of the opening parenthesis that was just typed.</param>
		public void ShowParameterTip(Int32 parenOffset)
		{
			if (!this._context.Options.ShowTips || !this._context.IsScript || parenOffset <= 0)
			{
				return;
			}

			if (!WordFinder.TryGetWordRange(this._context.Document, parenOffset - 1, out Int32 start, out Int32 length))
			{
				return;
			}

			String word = this._context.Document.GetText(start, length);
			TipInfo? tip = TipResolver.ResolveCodeTip(word, this._context.ProgramInfo, this._context.Services.GetOpcodeCatalog());
			if (tip is not null)
			{
				this.ShowCodeTip(tip, start, TimeSpan.FromSeconds(50), parenOffset);
			}
		}

		/// <summary>Shows a short hint under a word (for example "Press the TAB key to insert the autocode.").</summary>
		/// <param name="text">The hint.</param>
		/// <param name="anchorOffset">The offset the hint is placed at.</param>
		/// <param name="duration">How long the hint stays.</param>
		public void ShowHint(String text, Int32 anchorOffset, TimeSpan duration)
		{
			if (this._context.Options.ShowTips)
			{
				this.ShowCodeTip(new TipInfo(text, String.Empty, TipKind.Hint), anchorOffset, duration, -1);
			}
		}

		/// <summary>Handles a key press before the editor processes it: hides tips that no longer apply.</summary>
		/// <param name="e">The key event.</param>
		public void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (this.IsCodeTipVisible)
			{
				if (e.Key is Key.Escape or Key.Enter or Key.Up or Key.Down || this._codeTipAnchor < 0)
				{
					this.HideCodeTip();
				}
			}

			this.HideHoverTip();
		}

		private TipInfo? BuildSymbolTip(Int32 offset)
		{
			TextDocument document = this._context.Document;
			LexicalMap map = this._context.Lexical.Current;
			LexicalKind kind = map.GetKindAt(offset);
			DocumentLine line = document.GetLineByOffset(offset);

			if (kind == LexicalKind.String)
			{
				return TipResolver.ResolveInclude(
					document.GetText(line),
					this._context.Services.CreateIncludeResolver(),
					GetDirectory(this._context.DocumentModel.FilePath),
					File.Exists);
			}

			if (kind != LexicalKind.Code || !WordFinder.TryGetWordRange(document, offset, out Int32 start, out Int32 length))
			{
				return null;
			}

			String word = document.GetText(start, length);
			return TipResolver.ResolveWord(
				word,
				line.LineNumber,
				this._context.DocumentModel.FilePath,
				this._context.ProgramInfo,
				this._context.Services.GetOpcodeCatalog(),
				id => this._context.Services.Workspace.GetMessageText(this._context.DocumentModel, id));
		}

		private static String? GetDirectory(String? filePath)
		{
			return filePath is null ? null : Path.GetDirectoryName(filePath);
		}

		private void OnMouseHover(Object sender, MouseEventArgs e)
		{
			TextView view = this._context.TextView;
			Point point = e.GetPosition(view);
			TextViewPosition? position = view.GetPositionFloor(point + view.ScrollOffset);
			if (position is null || this._context.Document.LineCount == 0)
			{
				return;
			}

			Int32 offset = this._context.Document.GetOffset(position.Value.Location);
			if (offset >= this._context.Document.TextLength || !this.IsPointOverText(offset, point))
			{
				return;
			}

			if (this.ShowHoverTip(offset, point))
			{
				e.Handled = true;
			}
		}

		/// <summary>Shows the hover tip of a position of the text.</summary>
		/// <param name="offset">The offset of the character under the pointer.</param>
		/// <param name="point">The pointer position in the coordinates of the text view.</param>
		/// <returns><see langword="true"/> when a tip was shown.</returns>
		public Boolean ShowHoverTip(Int32 offset, Point point)
		{
			IReadOnlyList<TipInfo> tips = this.BuildTips(offset);
			if (tips.Count == 0)
			{
				return false;
			}

			TextView view = this._context.TextView;
			EditorTipViewModel model = EditorTipViewModel.Create(tips, this._context.Palette, this._context.FontFamily, this._context.FontSize);
			this._hoverTip ??= new ToolTip { PlacementTarget = view, Placement = PlacementMode.Relative };
			this._hoverTip.Content = new EditorTipView(model);
			this._hoverTip.PlacementTarget = view;
			this._hoverTip.HorizontalOffset = point.X;
			this._hoverTip.VerticalOffset = point.Y + 20;
			this._hoverTip.IsOpen = true;
			return true;
		}

		private Boolean IsPointOverText(Int32 offset, Point point)
		{
			TextView view = this._context.TextView;
			Int32 start = offset;
			Int32 length = 1;
			if (WordFinder.TryGetWordRange(this._context.Document, offset, out Int32 wordStart, out Int32 wordLength))
			{
				start = wordStart;
				length = wordLength;
			}
			else if (this._diagnostics.GetMarkersAt(offset).Count == 0)
			{
				// Strings (include paths) are hit-tested as a whole; everything else needs a word or a mark.
				if (!this._context.Lexical.Current.TryGetSpanAt(offset, out LexicalSpan span) || span.Kind != LexicalKind.String)
				{
					return false;
				}

				start = span.Start;
				length = span.Length;
			}

			foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(view, new TextSegment { StartOffset = start, Length = length }))
			{
				Rect inflated = rect;
				inflated.Inflate(1, 1);
				if (inflated.Contains(point))
				{
					return true;
				}
			}

			return false;
		}

		private void OnMouseHoverStopped(Object sender, MouseEventArgs e)
		{
			this.HideHoverTip();
		}

		private void OnMouseLeave(Object sender, MouseEventArgs e)
		{
			this.HideHoverTip();
		}

		private void OnScrollChanged(Object? sender, EventArgs e)
		{
			this.HideHoverTip();
			if (this.IsCodeTipVisible)
			{
				this.HideCodeTip();
			}
		}

		private void OnWindowDeactivated(Object? sender, EventArgs e)
		{
			this.HideAll();
		}

		private void OnCaretPositionChanged(Object? sender, EventArgs e)
		{
			if (this._codeTipAnchor < 0 || !this.IsCodeTipVisible)
			{
				return;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			TextDocument document = this._context.Document;
			if (this._codeTipAnchor >= document.TextLength || document.GetCharAt(this._codeTipAnchor) != '(' || caret <= this._codeTipAnchor)
			{
				this.HideCodeTip();
				return;
			}

			BracketPair? pair = BracketMatcher.FindPairOf(document, this._context.Lexical.Current, this._codeTipAnchor);
			if (pair is BracketPair closed && caret > closed.CloseOffset)
			{
				this.HideCodeTip();
			}
		}

		private void ShowCodeTip(TipInfo tip, Int32 textOffset, TimeSpan duration, Int32 parenOffset)
		{
			TextView view = this._context.TextView;
			if (view.ActualHeight <= 0)
			{
				return;
			}

			view.EnsureVisualLines();
			this.HideCodeTip();

			TextViewPosition location = new TextViewPosition(this._context.Document.GetLocation(textOffset));
			Point bottom = view.GetVisualPosition(location, VisualYPosition.LineBottom) - view.ScrollOffset;

			EditorTipViewModel model = EditorTipViewModel.Create(new[] { tip }, this._context.Palette, this._context.FontFamily, this._context.FontSize);
			Border chrome = new Border
			{
				Margin = new Thickness(CodeTipShadowMargin),
				Padding = new Thickness(10, 6, 10, 6),
				CornerRadius = new CornerRadius(6),
				BorderThickness = new Thickness(1),
				Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black },
				Child = new EditorTipView(model),
			};
			chrome.SetResourceReference(Border.BackgroundProperty, "ControlSolidFillColorDefaultBrush");
			chrome.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");

			this._codeTip = new Popup
			{
				PlacementTarget = view,
				Placement = PlacementMode.Relative,
				AllowsTransparency = true,
				StaysOpen = true,
				Focusable = false,
				HorizontalOffset = bottom.X,
				VerticalOffset = bottom.Y + 2,
				Child = chrome,
			};
			this._codeTipAnchor = parenOffset;
			this._codeTip.IsOpen = true;
			this._codeTipTimer.Interval = duration;
			this._codeTipTimer.Start();
		}
	}
}
