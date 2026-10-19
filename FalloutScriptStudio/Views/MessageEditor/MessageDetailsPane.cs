namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.ComponentModel;
	using System.Windows.Controls;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit;
	using FalloutScriptStudio.Editor.Highlighting;
	using Localization;
	using Services;
	using ViewModels.MessageEditor;

	/// <summary>Keeps the details editor below the grid in sync with the text of the selected row.</summary>
	internal sealed class MessageDetailsPane
	{
		private readonly TextEditor _editor;
		private readonly TextBlock _caption;
		private readonly MessageEditorViewModel _viewModel;
		private readonly IThemeService _theme;
		private readonly HighlightingProvider _highlighting;
		private MessageRowViewModel? _row;
		private Boolean _syncing;

		/// <summary>Initializes the pane.</summary>
		/// <param name="editor">The details editor.</param>
		/// <param name="caption">The caption above the editor.</param>
		/// <param name="viewModel">The view model of the window.</param>
		/// <param name="theme">The theme service, used for the colors and the syntax definition.</param>
		/// <param name="highlighting">Creates the syntax definition of the editor.</param>
		public MessageDetailsPane(TextEditor editor, TextBlock caption, MessageEditorViewModel viewModel, IThemeService theme, HighlightingProvider highlighting)
		{
			ArgumentNullException.ThrowIfNull(editor);
			ArgumentNullException.ThrowIfNull(caption);
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(theme);
			ArgumentNullException.ThrowIfNull(highlighting);
			this._editor = editor;
			this._caption = caption;
			this._viewModel = viewModel;
			this._theme = theme;
			this._highlighting = highlighting;
			this._editor.LostKeyboardFocus += (_, _) => this.Commit();
		}

		/// <summary>Gets a value indicating whether the details editor has the keyboard focus.</summary>
		public Boolean HasFocus => this._editor.IsKeyboardFocusWithin;

		/// <summary>Applies the font size of the view model to the editor.</summary>
		public void UpdateFontSize()
		{
			this._editor.FontSize = this._viewModel.FontSize;
		}

		/// <summary>Applies the syntax definition, colors and font size of the current theme.</summary>
		public void UpdateTheme()
		{
			SyntaxTheme theme = this._theme.SyntaxTheme;
			this._editor.SyntaxHighlighting = this._highlighting.Create(HighlightingKind.Message, theme);
			this._editor.Background = MessageEditorPalette.CreateBrush((Color)ColorConverter.ConvertFromString(theme.Background));
			this._editor.Foreground = MessageEditorPalette.CreateBrush((Color)ColorConverter.ConvertFromString(theme.Foreground));
			this.UpdateFontSize();
		}

		/// <summary>Shows the text of a row (the pane stays empty while it is hidden), after storing the pending edit of the previous row.</summary>
		/// <param name="row">The selected row, or <see langword="null"/>.</param>
		public void Attach(MessageRowViewModel? row)
		{
			this.Commit();
			this.Detach();
			if (!this._viewModel.IsDetailsVisible)
			{
				return;
			}

			this._row = row;
			if (row is not null)
			{
				row.PropertyChanged += this.OnRowPropertyChanged;
			}

			this.Load();
		}

		/// <summary>Stops following the current row.</summary>
		public void Detach()
		{
			if (this._row is not null)
			{
				this._row.PropertyChanged -= this.OnRowPropertyChanged;
				this._row = null;
			}
		}

		/// <summary>Shows the current text and caption of the row (also after a language change).</summary>
		public void Load()
		{
			this._syncing = true;
			try
			{
				MessageRowViewModel? row = this._row;
				this._editor.IsReadOnly = row is null;
				this._editor.Text = row?.Text ?? String.Empty;
				this._caption.Text = row is { IsMessage: true } ? Loc.Format("Str.MessageEditor.Details.Message", row.LineText) : Loc.Get("Str.MessageEditor.Details.Line");
			}
			finally
			{
				this._syncing = false;
			}
		}

		/// <summary>Writes the text of the editor back into the row when it was changed.</summary>
		public void Commit()
		{
			MessageRowViewModel? row = this._row;
			if (this._syncing || row is null || !this._viewModel.IsDetailsVisible || String.Equals(this._editor.Text, row.Text, StringComparison.Ordinal))
			{
				return;
			}

			row.Text = this._editor.Text;
			this.Load();
		}

		private void OnRowPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MessageRowViewModel.Text) && !this.HasFocus)
			{
				this.Load();
			}
		}
	}
}