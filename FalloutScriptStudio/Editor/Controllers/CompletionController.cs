namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Effects;
	using System.Windows.Threading;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Editor.Completion;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit.CodeCompletion;

	/// <summary>
	/// The auto-completion popup for opcodes, procedures, variables and macros: opens while typing an identifier and on
	/// demand (Ctrl+Space), with kind icons and descriptions (legacy <c>AutoComplete</c>).
	/// </summary>
	public sealed class CompletionController : IDisposable
	{
		private const Double ShadowMargin = 10;
		private const Int32 VisibleItems = 12;

		private readonly EditorContext _context;
		private readonly EditorPopupResources _resources = new EditorPopupResources();
		private CompletionWindow? _window;
		private Window? _ownerWindow;
		private Boolean _attached;

		public CompletionController(EditorContext context)
		{
			this._context = context;
		}

		/// <summary>Gets a value indicating whether the completion list is open.</summary>
		public Boolean IsOpen => this._window is not null;

		/// <summary>Gets the open completion window (for tests), or <see langword="null"/>.</summary>
		public CompletionWindow? Window => this._window;

		public void Attach()
		{
			this._attached = true;
			this._context.TextArea.TextEntered += this.OnTextEntered;
			this._context.TextArea.TextEntering += this.OnTextEntering;
			this._context.TextArea.PreviewKeyDown += this.OnPreviewKeyDown;
			this._ownerWindow = System.Windows.Window.GetWindow(this._context.Editor);
			if (this._ownerWindow is not null)
			{
				this._ownerWindow.Deactivated += this.OnOwnerDeactivated;
			}
		}

		public void Dispose()
		{
			this._attached = false;
			this._context.TextArea.TextEntered -= this.OnTextEntered;
			this._context.TextArea.TextEntering -= this.OnTextEntering;
			this._context.TextArea.PreviewKeyDown -= this.OnPreviewKeyDown;
			if (this._ownerWindow is not null)
			{
				this._ownerWindow.Deactivated -= this.OnOwnerDeactivated;
				this._ownerWindow = null;
			}

			this.Close();
		}

		/// <summary>Closes the list.</summary>
		public void Close()
		{
			CompletionWindow? window = this._window;
			this._window = null;
			window?.Close();
		}

		/// <summary>Opens the list for the identifier before the caret.</summary>
		/// <param name="explicitRequest">True for the Ctrl+Space command: a one-letter prefix is enough and the list opens even when the setting is off.</param>
		/// <returns><see langword="true"/> when the list was opened.</returns>
		public Boolean Show(Boolean explicitRequest)
		{
			if (!this._context.IsScript || (!explicitRequest && !this._context.Options.Autocomplete))
			{
				return false;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			if (!this._context.Lexical.Current.IsCodeAtCaret(caret))
			{
				return false;
			}

			String prefix = CompletionSource.GetPrefix(this._context.Document, caret, out Int32 start);
			IReadOnlyList<AutosuggestItem> items = CompletionSource.GetItems(
				prefix,
				this._context.ProgramInfo,
				this._context.Services.GetOpcodeCatalog(),
				explicitRequest ? 1 : CompletionSource.MinPrefixLength);
			if (items.Count == 0)
			{
				return false;
			}

			this.Close();
			this.Open(items, prefix, start);
			return this._window is not null;
		}

		private void Open(IReadOnlyList<AutosuggestItem> items, String prefix, Int32 start)
		{
			EditorPalette palette = this._context.Palette;
			CompletionAppearance appearance = new CompletionAppearance(
				palette,
				this._context.Options.AutocompleteColor,
				this._context.FontFamily,
				this._context.FontSize);

			CompletionWindow window = new CompletionWindow(this._context.TextArea)
			{
				CloseAutomatically = true,
				StartOffset = start,
				WindowStyle = WindowStyle.None,
				AllowsTransparency = true,
				Background = Brushes.Transparent,
				ResizeMode = ResizeMode.NoResize,
			};

			CompletionList list = window.CompletionList;
			foreach (AutosuggestItem item in items)
			{
				list.CompletionData.Add(new ScriptCompletionData(item, appearance));
			}

			list.ApplyTemplate();
			list.Background = Brushes.Transparent;
			list.BorderThickness = new Thickness(0);
			list.FontFamily = this._context.FontFamily;
			list.FontSize = this._context.FontSize;
			if (list.ListBox is not null)
			{
				list.ListBox.Background = Brushes.Transparent;
				list.ListBox.BorderThickness = new Thickness(0);
				list.ListBox.Padding = new Thickness(0, 4, 0, 4);
				list.ListBox.ItemTemplate = (DataTemplate)this._resources["CompletionItemTemplate"];
				list.ListBox.ItemContainerStyle = (Style)this._resources["CompletionItemContainerStyle"];
			}

			list.SelectItem(prefix);

			window.Content = null;
			Border chrome = new Border
			{
				Margin = new Thickness(0, 0, ShadowMargin, ShadowMargin),
				CornerRadius = new CornerRadius(8),
				BorderThickness = new Thickness(1),
				Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black },
				Child = list,
			};
			chrome.SetResourceReference(Border.BackgroundProperty, "ControlSolidFillColorDefaultBrush");
			chrome.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
			window.Content = chrome;

			window.Width = this.MeasureWidth(items) + ShadowMargin;
			window.MaxHeight = (VisibleItems * 26) + 16 + ShadowMargin;
			window.Closed += this.OnWindowClosed;

			this._window = window;
			this._context.Services.Popups.Show(window);
		}

		private Double MeasureWidth(IReadOnlyList<AutosuggestItem> items)
		{
			String longest = items.Max(i => i.Name.Length) is Int32 length ? items.First(i => i.Name.Length == length).Name : String.Empty;
			Typeface typeface = new Typeface(this._context.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
			FormattedText text = new FormattedText(
				longest,
				CultureInfo.CurrentUICulture,
				FlowDirection.LeftToRight,
				typeface,
				this._context.FontSize,
				Brushes.Black,
				VisualTreeHelper.GetDpi(this._context.Editor).PixelsPerDip);
			Double scrollbar = items.Count > VisibleItems ? 20 : 0;
			return Math.Clamp(text.WidthIncludingTrailingWhitespace + 64 + scrollbar, 180, 520);
		}

		private void OnWindowClosed(Object? sender, EventArgs e)
		{
			if (Object.ReferenceEquals(sender, this._window))
			{
				this._window = null;
			}
		}

		private void OnOwnerDeactivated(Object? sender, EventArgs e)
		{
			this.Close();
		}

		private void OnTextEntering(Object sender, TextCompositionEventArgs e)
		{
			// Anything but an identifier character ends the word: close the list without inserting an entry (legacy behaviour).
			if (this._window is not null && e.Text.Length > 0 && !WordFinder.IsWordChar(e.Text[0]))
			{
				this.Close();
			}
		}

		private void OnTextEntered(Object sender, TextCompositionEventArgs e)
		{
			if (this._window is not null || e.Text.Length != 1 || !WordFinder.IsWordChar(e.Text[0]))
			{
				return;
			}

			if (!this._context.IsScript || !this._context.Options.Autocomplete)
			{
				return;
			}

			this.Show(false);
		}

		private void ShowAfterBackspace()
		{
			// The session may have been stopped while the callback was queued: the control then belongs to another session.
			if (this._attached)
			{
				this.Show(false);
			}
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Back || this._window is null)
			{
				return;
			}

			// The list narrows while typing but never widens: after a backspace it is rebuilt for the shorter prefix.
			this.Close();
			this._context.Editor.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(this.ShowAfterBackspace));
		}
	}
}
