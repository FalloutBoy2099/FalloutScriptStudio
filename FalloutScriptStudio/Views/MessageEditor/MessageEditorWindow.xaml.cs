namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Threading;
	using Wpf.Ui.Controls;
	using TextBox = System.Windows.Controls.TextBox;
	using Core.Configuration;
	using FalloutScriptStudio.Editor.Highlighting;
	using Localization;
	using Services;
	using Services.MessageEditor;
	using ViewModels.MessageEditor;

	/// <summary>
	/// The Message Editor window: a grid of the messages of a <c>.msg</c> file with inline editing.
	/// The code-behind only connects the controls to the view model and to the helpers that drive them
	/// (<see cref="MessageGridController"/>, <see cref="MessageEditorMenus"/>, <see cref="MessageDetailsPane"/>), handles the window-level
	/// keys and closing, and takes care of window placement and theme dependent colors.
	/// </summary>
	public partial class MessageEditorWindow : FluentWindow
	{
		private readonly MessageEditorViewModel _viewModel;
		private readonly ISettingsService _settings;
		private readonly IThemeService _theme;
		private readonly MessageGridController _grid;
		private readonly MessageEditorMenus _menus;
		private readonly MessageDetailsPane _details;

		private MessageEditorOpenOptions _options = new();
		private Boolean _closeApproved;
		private Boolean _closing;

		/// <summary>
		/// Initializes a new instance of the <see cref="MessageEditorWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model of the window.</param>
		/// <param name="settings">The user settings, used for the window placement.</param>
		/// <param name="theme">The theme service, used for colors that depend on light or dark mode.</param>
		/// <param name="highlighting">Creates the syntax definition of the details editor.</param>
		public MessageEditorWindow(MessageEditorViewModel viewModel, ISettingsService settings, IThemeService theme, HighlightingProvider highlighting)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(theme);
			ArgumentNullException.ThrowIfNull(highlighting);
			this._viewModel = viewModel;
			this._settings = settings;
			this._theme = theme;

			this.InitializeComponent();
			this.DataContext = viewModel;
			this._grid = new MessageGridController(
				this.MessageGrid,
				new DataGridColumn[] { this.LineColumn, this.PlayerColumn, this.TextColumn, this.CommentColumn, this.SoundColumn },
				viewModel);
			this._menus = new MessageEditorMenus(this.Resources, viewModel, this.RecentButton, this.OpenWithButton, this.OptionsButton);
			this._details = new MessageDetailsPane(this.DetailsEditor, this.DetailsCaption, viewModel, theme, highlighting);

			this._grid.Attach();
			this._menus.Attach();
			this.UpdateLocalizedTexts();
			this._grid.ApplyOptions();
			this.UpdateCommentBrushes();

			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
			viewModel.RowFocusRequested += this.OnRowFocusRequested;
			viewModel.EditCommitRequested += this.OnEditCommitRequested;
			viewModel.CloseRequested += this.OnCloseRequested;
			theme.ThemeChanged += this.OnThemeChanged;
			Loc.LanguageChanged += this.OnLanguageChanged;
			this.Activated += this.OnActivated;
			this.Closed += this.OnClosedCore;
		}

		/// <summary>Gets the view model of the window.</summary>
		public MessageEditorViewModel ViewModel => this._viewModel;

		/// <summary>
		/// Stores how the window is opened. The file is loaded as soon as the window is shown.
		/// </summary>
		/// <param name="options">The options.</param>
		public void Prepare(MessageEditorOpenOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			this._options = options;
			MessageEditorPlacement.Restore(this, this._settings);
			if (options.StartMaximized)
			{
				this.WindowState = WindowState.Maximized;
			}

			this.Loaded += this.OnFirstLoaded;
		}

		/// <summary>
		/// Brings an already open window to the front for a new request.
		/// </summary>
		/// <param name="options">The request; its target replaces the current one.</param>
		public void Reveal(MessageEditorOpenOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			if (options.Target is not null)
			{
				this._options = options;
				this._viewModel.AttachTarget(options.Target, options.CloseOnSend);
				this.UpdateOwner();
			}

			if (this.WindowState == WindowState.Minimized)
			{
				this.WindowState = WindowState.Normal;
			}

			this.Activate();
			if (options.SelectMessageId is Int32 id)
			{
				this._viewModel.SelectMessage(id);
			}
		}

		/// <inheritdoc/>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			base.OnPreviewKeyDown(e);
			if (e.Handled)
			{
				return;
			}

			Boolean control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
			if (e.Key == Key.S && control)
			{
				// An edit in progress becomes part of the document before Save checks whether there is anything to save.
				this._viewModel.CommitPendingEdit();
			}

			if (e.Key == Key.F && control)
			{
				this.SearchBox.Focus();
				this.SearchBox.SelectAll();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape && !this._viewModel.IsCellEditing)
			{
				if (this.SearchBox.IsKeyboardFocusWithin)
				{
					if (this._viewModel.SearchText.Length > 0)
					{
						this._viewModel.SearchText = String.Empty;
					}
					else
					{
						this.MessageGrid.Focus();
					}
				}
				else if (this._details.HasFocus)
				{
					this._details.Commit();
					this.MessageGrid.Focus();
				}
				else if (Keyboard.FocusedElement is not TextBox)
				{
					this.Close();
				}
				else
				{
					return;
				}

				e.Handled = true;
			}
		}

		/// <inheritdoc/>
		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			if (this._closeApproved || e.Cancel)
			{
				return;
			}

			this._viewModel.CommitPendingEdit();
			if (!this._viewModel.NeedsClosePrompt)
			{
				MessageEditorPlacement.Store(this, this._settings);
				this._closeApproved = true;
				return;
			}

			// The question about unsaved changes is asynchronous, so the close is cancelled now and repeated once the user has answered.
			e.Cancel = true;
			if (!this._closing)
			{
				this._closing = true;
				this.CloseAfterConfirmation();
			}
		}

		/// <summary>Called by the cell editor style in XAML: forwards the key to the grid controller.</summary>
		private void OnCellEditorPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			this._grid.HandleCellEditorKey(sender, e);
		}

		private async void CloseAfterConfirmation()
		{
			try
			{
				if (await this._viewModel.CanCloseAsync())
				{
					MessageEditorPlacement.Store(this, this._settings);
					this._closeApproved = true;
					_ = this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.Close));
				}
			}
			finally
			{
				this._closing = false;
			}
		}

		private async void OnFirstLoaded(Object sender, RoutedEventArgs e)
		{
			this.Loaded -= this.OnFirstLoaded;
			this.UpdateOwner();
			await this._viewModel.InitializeAsync(this._options);
			this.UpdateOwner();
			this.MessageGrid.Focus();
		}

		private void OnClosedCore(Object? sender, EventArgs e)
		{
			this._theme.ThemeChanged -= this.OnThemeChanged;
			Loc.LanguageChanged -= this.OnLanguageChanged;
			this._viewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
			this._viewModel.RowFocusRequested -= this.OnRowFocusRequested;
			this._viewModel.EditCommitRequested -= this.OnEditCommitRequested;
			this._viewModel.CloseRequested -= this.OnCloseRequested;
			this._details.Detach();
			this._viewModel.PersistSettings();
			this._viewModel.Dispose();
		}

		private void OnActivated(Object? sender, EventArgs e)
		{
			this._viewModel.CheckExternalChange();
			this._viewModel.RefreshTargetState();
		}

		private void OnCloseRequested(Object? sender, EventArgs e)
		{
			this.Close();
		}

		private void OnLanguageChanged(Object? sender, EventArgs e)
		{
			this.UpdateLocalizedTexts();
		}

		/// <summary>
		/// Sets the texts that cannot follow a <c>DynamicResource</c>: the headers of the grid columns (they are not part of the element tree),
		/// the items made in code and the caption of the details pane.
		/// </summary>
		private void UpdateLocalizedTexts()
		{
			this._menus.RefreshTexts();
			this._grid.RefreshHeaders();
			if (this._viewModel.IsDetailsVisible)
			{
				this._details.Load();
			}
		}

		private void OnThemeChanged(Object? sender, EventArgs e)
		{
			this.UpdateCommentBrushes();
			if (this._viewModel.IsDetailsVisible)
			{
				this._details.UpdateTheme();
			}
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(MessageEditorViewModel.IsWindowOnTop):
					this.UpdateOwner();
					break;
				case nameof(MessageEditorViewModel.ShowLipColumn):
				case nameof(MessageEditorViewModel.LineColumnWidth):
					this._grid.ApplyOptions();
					break;
				case nameof(MessageEditorViewModel.CommentColor):
					this.UpdateCommentBrushes();
					break;
				case nameof(MessageEditorViewModel.FontSizeIndex):
					this._grid.ApplyOptions();
					this._details.UpdateFontSize();
					break;
				case nameof(MessageEditorViewModel.SelectedRow):
					this._details.Attach(this._viewModel.SelectedRow);
					break;
				case nameof(MessageEditorViewModel.IsDetailsVisible):
					if (this._viewModel.IsDetailsVisible)
					{
						this._details.UpdateTheme();
					}

					this._details.Attach(this._viewModel.SelectedRow);
					break;
				default:
					break;
			}
		}

		private void UpdateCommentBrushes()
		{
			MessageEditorPalette.Apply(this.Resources, this._viewModel.CommentColor, this._theme.IsDark);
		}

		private void UpdateOwner()
		{
			Window? wanted = null;
			if (this._viewModel.IsWindowOnTop)
			{
				wanted = this._options.Owner ?? (this._viewModel.HasTarget ? Application.Current?.MainWindow : null);
				if (wanted is null || ReferenceEquals(wanted, this) || !wanted.IsLoaded)
				{
					wanted = null;
				}
			}

			if (!ReferenceEquals(this.Owner, wanted))
			{
				try
				{
					this.Owner = wanted;
				}
				catch (InvalidOperationException)
				{
					// The owner cannot be changed while the window is shown as a dialog.
				}
			}
		}

		private void OnRowFocusRequested(Object? sender, RowFocusRequestedEventArgs e)
		{
			// The rows may have just been added; wait until the grid has created their containers.
			this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => this._grid.ApplyFocus(e, this.SearchBox.IsKeyboardFocusWithin)));
		}

		private void OnEditCommitRequested(Object? sender, EventArgs e)
		{
			this._grid.CommitEdit();
			this._details.Commit();
		}
	}
}