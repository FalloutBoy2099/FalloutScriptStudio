namespace FalloutScriptStudio.Views.ScriptTools.Dialogs
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Media;
	using Wpf.Ui.Controls;
	using FalloutScriptStudio.Editor.Highlighting;
	using Services;
	using ViewModels.ScriptTools.Dialogs;

	/// <summary>
	/// The add / edit user function dialog. The code box is an AvalonEdit editor with the SSL syntax highlighting.
	/// </summary>
	public partial class UserFunctionWindow : FluentWindow
	{
		private readonly IThemeService _theme;
		private readonly HighlightingProvider _highlighting;
		private Boolean _syncing;

		/// <summary>
		/// Initializes a new instance of the <see cref="UserFunctionWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="theme">Provides the editor colors of the current theme.</param>
		/// <param name="highlighting">Builds the syntax highlighting definition.</param>
		public UserFunctionWindow(UserFunctionViewModel viewModel, IThemeService theme, HighlightingProvider highlighting)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(theme);
			ArgumentNullException.ThrowIfNull(highlighting);

			this.ViewModel = viewModel;
			this._theme = theme;
			this._highlighting = highlighting;
			this.InitializeComponent();
			DialogueWindowHelper.Attach(this, viewModel);

			this.CodeEditor.Text = viewModel.Code;
			this.ApplyEditorTheme();
			this.CodeEditor.TextChanged += this.OnEditorTextChanged;
			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
			theme.ThemeChanged += this.OnThemeChanged;
			this.Loaded += this.OnLoaded;
			this.Closed += this.OnClosed;
		}

		/// <summary>Gets the view model.</summary>
		public UserFunctionViewModel ViewModel { get; }

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (this.ViewModel.FunctionName.Length == 0)
			{
				this.NameBox.Focus();
			}
			else
			{
				this.CodeEditor.Focus();
			}
		}

		private void OnClosed(Object? sender, EventArgs e)
		{
			this.ViewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
			this._theme.ThemeChanged -= this.OnThemeChanged;
			this.CodeEditor.TextChanged -= this.OnEditorTextChanged;
		}

		private void OnEditorTextChanged(Object? sender, EventArgs e)
		{
			if (this._syncing)
			{
				return;
			}

			this._syncing = true;
			try
			{
				this.ViewModel.Code = this.CodeEditor.Text;
			}
			finally
			{
				this._syncing = false;
			}
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(UserFunctionViewModel.Code) || this._syncing || this.CodeEditor.Text == this.ViewModel.Code)
			{
				return;
			}

			this._syncing = true;
			try
			{
				this.CodeEditor.Text = this.ViewModel.Code;
			}
			finally
			{
				this._syncing = false;
			}
		}

		private void OnThemeChanged(Object? sender, EventArgs e)
		{
			this.Dispatcher.BeginInvoke(new Action(this.ApplyEditorTheme));
		}

		private void ApplyEditorTheme()
		{
			SyntaxTheme theme = this._theme.SyntaxTheme;
			this.CodeEditor.SyntaxHighlighting = this._highlighting.Create(HighlightingKind.Ssl, theme);
			this.CodeEditor.Background = CreateBrush(theme.Background);
			this.CodeEditor.Foreground = CreateBrush(theme.Foreground);
			this.CodeEditor.LineNumbersForeground = CreateBrush(theme.LineNumbers);
			this.CodeEditor.TextArea.SelectionBrush = CreateBrush(theme.Selection);
			this.CodeEditor.TextArea.SelectionForeground = null;
			this.CodeEditor.TextArea.SelectionBorder = null;
			this.CodeEditor.TextArea.TextView.CurrentLineBackground = CreateBrush(theme.CaretLine);
			this.CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
			this.CodeEditor.TextArea.TextView.NonPrintableCharacterBrush = CreateBrush(theme.WhitespaceMarker);
		}

		private static SolidColorBrush CreateBrush(String color)
		{
			SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(color));
			brush.Freeze();
			return brush;
		}
	}
}