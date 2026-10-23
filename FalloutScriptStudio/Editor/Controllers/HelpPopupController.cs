namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Effects;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.Help;
	using FalloutScriptStudio.Views.Help;
	using FalloutScriptStudio.Workspace.Help;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// The help popup of the editor (Ctrl+F1): the documentation of the function, opcode, macro or procedure under the caret, with a button
	/// that opens the page of the sfall documentation. It is built like the other popups of the editor and opened through the
	/// <see cref="IEditorPopupPresenter"/>, so the tests never open a window.
	/// </summary>
	public sealed class HelpPopupController : IDisposable
	{
		private const Double ShadowMargin = 10;
		private readonly EditorContext _context;
		private Popup? _popup;
		private Boolean _shown;

		public HelpPopupController(EditorContext context)
		{
			this._context = context;
		}

		/// <summary>Gets the popup that was opened last (for tests), or <see langword="null"/>.</summary>
		public Popup? Popup => this._popup;

		/// <summary>Gets a value indicating whether the popup is open.</summary>
		public Boolean IsOpen => this._popup is not null && this._shown;

		/// <summary>Gets the text of the shortcut that shows the help ("Ctrl+F1"), as the gesture of the command writes it.</summary>
		public static String GestureText
		{
			get
			{
				KeyGesture? gesture = EditorCommands.Get(EditorCommand.ShowHelp).InputGestures.OfType<KeyGesture>().FirstOrDefault();
				return gesture?.GetDisplayStringForCulture(CultureInfo.InvariantCulture) ?? "Ctrl+F1";
			}
		}

		/// <summary>Finds the help of the word at an offset.</summary>
		/// <param name="offset">The offset in the document.</param>
		/// <returns>The topic, or <see langword="null"/> when the word is not code or nothing is known about it.</returns>
		public HelpTopic? Resolve(Int32 offset)
		{
			IFunctionHelpService? help = this._context.Services.Help;
			if (help is null || !this._context.IsScript || offset < 0 || offset > this._context.Document.TextLength)
			{
				return null;
			}

			if (!WordFinder.TryGetWordRangeNearCaret(this._context.Document, offset, out Int32 start, out Int32 length) || this._context.Lexical.Current.GetKindAt(start) != LexicalKind.Code)
			{
				return null;
			}

			String word = this._context.Document.GetText(start, length);
			Int32 line = this._context.Document.GetLineByOffset(start).LineNumber;
			return help.Find(word, this._context.ProgramInfo, this._context.DocumentModel.FilePath, line);
		}

		/// <summary>Determines whether there is help for the word at the caret.</summary>
		/// <returns><see langword="true"/> when the popup can be shown.</returns>
		public Boolean CanShow()
		{
			return this.Resolve(this._context.TextArea.Caret.Offset) is not null;
		}

		/// <summary>Shows the help of the word at the caret under the word.</summary>
		public void Show()
		{
			Int32 caret = this._context.TextArea.Caret.Offset;
			HelpTopic? topic = this.Resolve(caret);
			TextView view = this._context.TextView;
			if (topic is null || view.ActualHeight <= 0)
			{
				return;
			}

			this.Hide();
			view.EnsureVisualLines();
			Int32 anchor = WordFinder.TryGetWordRangeNearCaret(this._context.Document, caret, out Int32 start, out _) ? start : caret;
			Point bottom = view.GetVisualPosition(new TextViewPosition(this._context.Document.GetLocation(anchor)), VisualYPosition.LineBottom) - view.ScrollOffset;

			HelpTopicViewModel model = new(topic, this.OpenDocumentation);
			HelpTopicView content = new() { DataContext = model };
			ScrollViewer scroll = new()
			{
				MaxWidth = 640,
				MaxHeight = 380,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				Content = content,
			};
			System.Windows.Automation.AutomationProperties.SetName(scroll, topic.SignatureText);
			Border chrome = new()
			{
				Margin = new Thickness(ShadowMargin),
				Padding = new Thickness(12, 8, 12, 8),
				CornerRadius = new CornerRadius(6),
				BorderThickness = new Thickness(1),
				Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black },
				Child = scroll,
			};
			chrome.SetResourceReference(Border.BackgroundProperty, "ControlSolidFillColorDefaultBrush");
			chrome.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");

			this._popup = new Popup
			{
				PlacementTarget = view,
				Placement = PlacementMode.Relative,
				AllowsTransparency = true,
				StaysOpen = false,
				HorizontalOffset = bottom.X,
				VerticalOffset = bottom.Y + 2,
				Child = chrome,
			};
			this._popup.Closed += this.OnPopupClosed;
			this._context.Services.Popups.Show(this._popup);
			this._shown = true;
		}

		/// <summary>Closes the popup.</summary>
		public void Hide()
		{
			this._shown = false;
			if (this._popup is not null)
			{
				this._popup.Closed -= this.OnPopupClosed;
				this._popup.IsOpen = false;
			}
		}

		public void Dispose()
		{
			this.Hide();
			this._popup = null;
		}

		private void OnPopupClosed(Object? sender, EventArgs e)
		{
			this._shown = false;
		}

		private void OpenDocumentation(HelpTopic topic)
		{
			IFunctionHelpService? help = this._context.Services.Help;
			if (help is null)
			{
				return;
			}

			Uri? opened = help.OpenDocumentation(topic);
			if (opened is null)
			{
				Uri address = SfallDocumentation.GetUri(topic) ?? SfallDocumentation.BaseUri;
				this._context.Services.Workspace.ReportStatus(Loc.Format("Str.Shell.Dlg.DocsNotFound", address.AbsoluteUri));
			}

			this.Hide();
		}
	}
}