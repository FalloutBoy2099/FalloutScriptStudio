namespace FalloutScriptStudio.Views.Shell
{
	using System;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows;
	using System.Windows.Automation;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media.Animation;
	using Wpf.Ui.Controls;
	using MenuItem = System.Windows.Controls.MenuItem;
	using Accessibility;
	using Core.Configuration;
	using Core.Parsing;
	using Localization;
	using Services;
	using ViewModels.Main;

	/// <summary>The main window: menu, toolbar, document area, side panels, output panel and status bar.</summary>
	public partial class MainWindow : FluentWindow
	{
		private const Double DefaultRightWidth = 300;
		private const Double DefaultLeftWidth = 240;
		private const Double DefaultOutputHeight = 220;
		private const Double MinimizedOutputHeight = 40;

		private readonly MainViewModel _viewModel;
		private readonly ISettingsService _settings;
		private Double _rightWidth = DefaultRightWidth;
		private Double _leftWidth = DefaultLeftWidth;
		private Double _outputHeight = DefaultOutputHeight;
		private Boolean _closeApproved;
		private Boolean _closing;

		public MainWindow(MainViewModel viewModel, IShellNotificationService notifications, ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(notifications);
			ArgumentNullException.ThrowIfNull(settings);
			this._viewModel = viewModel;
			this._settings = settings;
			this.DataContext = viewModel;
			this.InitializeComponent();
			this.OutputPanel.DataContext = viewModel;
			viewModel.GestureSource = new WindowGestureSource(this.InputBindings, this.MainMenu);
			this.RightPanel.DataContext = viewModel;

			this.RestorePlacement();
			this._rightWidth = settings.Settings.Window.SecondarySplitterPosition > 0 ? settings.Settings.Window.SecondarySplitterPosition : DefaultRightWidth;
			this._outputHeight = settings.Settings.Window.SplitterPosition > 0 ? settings.Settings.Window.SplitterPosition : DefaultOutputHeight;
			this._leftWidth = viewModel.FunctionTreeWidth > 60 ? viewModel.FunctionTreeWidth : DefaultLeftWidth;

			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
			viewModel.ExitRequested += (_, _) => this.Close();
			notifications.NotificationRaised += (_, notification) => this.Dispatcher.BeginInvoke(new Action(() => this.ShowToast(notification)));
			this.SizeChanged += (_, _) => this.ClampOutputHeight();
			this.Activated += async (_, _) => await viewModel.CheckExternalChangesAsync();
			this.Loaded += async (_, _) =>
			{
				this.ApplyLayout();
				await viewModel.RunFirstRunAsync();
			};
		}

		protected override async void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			if (this._closeApproved)
			{
				this.SaveWindowState();
				return;
			}

			e.Cancel = true;
			if (this._closing)
			{
				return;
			}

			this._closing = true;
			try
			{
				if (await this._viewModel.CanCloseAsync())
				{
					// Close from a fresh dispatcher turn: when CanCloseAsync completes synchronously this code still runs
					// inside the original Closing event, where Window.Close() is illegal.
					this._closeApproved = true;
					_ = this.Dispatcher.BeginInvoke(new Action(this.Close));
				}
			}
			finally
			{
				this._closing = false;
			}
		}

		private static WindowPlacement CreatePlacement(Window window)
		{
			Rect bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
			return new WindowPlacement
			{
				IsMaximized = window.WindowState == WindowState.Maximized,
				X = (Int32)bounds.Left,
				Y = (Int32)bounds.Top,
				Width = (Int32)bounds.Width,
				Height = (Int32)bounds.Height,
			};
		}

		private void RestorePlacement()
		{
			WindowPlacement? placement = this._settings.Settings.Window.GetPlacement(WindowKind.Main);
			if (placement is null)
			{
				this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				this.WindowState = this._settings.Settings.IsFirstRunCompleted ? WindowState.Normal : WindowState.Maximized;
				return;
			}

			Rect screen = new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
			Rect bounds = new(placement.X, placement.Y, Math.Max(placement.Width, this.MinWidth), Math.Max(placement.Height, this.MinHeight));
			if (!screen.IntersectsWith(bounds) || bounds.Left + 60 > screen.Right || bounds.Top + 30 > screen.Bottom)
			{
				this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				return;
			}

			this.WindowStartupLocation = WindowStartupLocation.Manual;
			this.Left = bounds.Left;
			this.Top = bounds.Top;
			this.Width = bounds.Width;
			this.Height = bounds.Height;
			if (placement.IsMaximized)
			{
				this.WindowState = WindowState.Maximized;
			}
		}

		private void SaveWindowState()
		{
			this.CaptureSizes();
			WindowPlacement placement = CreatePlacement(this);
			this._viewModel.FunctionTreeWidth = this._leftWidth;
			this._settings.Update(settings =>
			{
				settings.Window.SetPlacement(WindowKind.Main, placement);
				settings.Window.SplitterPosition = (Int32)this._outputHeight;
				settings.Window.SecondarySplitterPosition = (Int32)this._rightWidth;
			});
			this._viewModel.SaveLayout();
		}

		private void CaptureSizes()
		{
			if (this.RightColumn.ActualWidth > 60)
			{
				this._rightWidth = this.RightColumn.ActualWidth;
			}

			if (this.LeftColumn.ActualWidth > 60)
			{
				this._leftWidth = this.LeftColumn.ActualWidth;
			}

			if (this.OutputRow.ActualHeight > MinimizedOutputHeight + 10 && this.OutputPanel.Visibility == Visibility.Visible)
			{
				this._outputHeight = this.OutputRow.ActualHeight;
			}
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(MainViewModel.IsRightBrowserVisible):
				case nameof(MainViewModel.ShowFunctionTree):
				case nameof(MainViewModel.ShowLog):
				case nameof(MainViewModel.IsOutputMinimized):
					this.ApplyLayout();
					break;
			}
		}

		private void ApplyLayout()
		{
			this.CaptureSizes();

			Boolean right = this._viewModel.IsRightBrowserVisible;
			this.RightPanel.Visibility = right ? Visibility.Visible : Visibility.Collapsed;
			this.RightSplitter.Visibility = this.RightPanel.Visibility;
			this.RightColumn.MinWidth = right ? 180 : 0;
			this.RightColumn.Width = right ? new GridLength(this._rightWidth) : new GridLength(0);

			Boolean left = this._viewModel.ShowFunctionTree;
			this.LeftPanel.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
			this.LeftSplitter.Visibility = this.LeftPanel.Visibility;
			this.LeftColumn.MinWidth = left ? 150 : 0;
			this.LeftColumn.Width = left ? new GridLength(this._leftWidth) : new GridLength(0);

			Boolean log = this._viewModel.ShowLog;
			Boolean minimized = this._viewModel.IsOutputMinimized;
			this.OutputPanel.Visibility = log ? Visibility.Visible : Visibility.Collapsed;
			this.OutputSplitter.Visibility = log && !minimized ? Visibility.Visible : Visibility.Collapsed;
			this.OutputRow.MinHeight = log && !minimized ? 60 : 0;
			Double maxOutputHeight = this.BodyGrid.ActualHeight > 0 ? Math.Max(100, this.BodyGrid.ActualHeight * 0.4) : this._outputHeight;
			this.OutputRow.Height = !log ? new GridLength(0) : minimized ? new GridLength(MinimizedOutputHeight) : new GridLength(Math.Min(this._outputHeight, maxOutputHeight));
		}

		private void ClampOutputHeight()
		{
			if (!this._viewModel.ShowLog || this._viewModel.IsOutputMinimized || this.BodyGrid.ActualHeight <= 0)
			{
				return;
			}

			Double limit = Math.Max(100, this.BodyGrid.ActualHeight * 0.45);
			if (this.OutputRow.ActualHeight > limit)
			{
				this.OutputRow.Height = new GridLength(limit);
			}
		}

		private void ShowToast(ShellNotification notification)
		{
			this.ToastTitle.Text = notification.Title;
			this.ToastMessage.Text = notification.Message;
			String announcement = String.IsNullOrEmpty(notification.Message) ? notification.Title : notification.Title + ". " + notification.Message;
			AutomationProperties.SetName(this.Toast, announcement);
			LiveRegion.SetText(this.Toast, announcement);
			switch (notification.Kind)
			{
				case NotificationKind.Failure:
					this.ToastIcon.Symbol = SymbolRegular.DismissCircle24;
					this.ToastIcon.SetResourceReference(Control.ForegroundProperty, "SystemFillColorCriticalBrush");
					break;
				case NotificationKind.Info:
					this.ToastIcon.Symbol = SymbolRegular.Info24;
					this.ToastIcon.SetResourceReference(Control.ForegroundProperty, "SystemFillColorAttentionBrush");
					break;
				default:
					this.ToastIcon.Symbol = SymbolRegular.CheckmarkCircle24;
					this.ToastIcon.SetResourceReference(Control.ForegroundProperty, "SystemFillColorSuccessBrush");
					break;
			}

			DoubleAnimationUsingKeyFrames animation = new();
			animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
			animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
			animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1400))));
			animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2100))));
			this.Toast.BeginAnimation(OpacityProperty, animation);
		}

		private void OnWindowDragOver(Object sender, DragEventArgs e)
		{
			e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		}

		private async void OnWindowDrop(Object sender, DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is String[] files)
			{
				await this._viewModel.OpenFilesAsync(files);
				this.Activate();
			}
		}

		private void OnThemeButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(this.ThemeButton);
			foreach (AppTheme theme in new[] { AppTheme.System, AppTheme.Light, AppTheme.Dark })
			{
				MenuItem item = CreateCheckableItem(Loc.Get("Str.Common.Theme" + theme), theme == this._viewModel.CurrentTheme);
				item.Click += (_, _) => this._viewModel.SetTheme(theme);
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private void OnLanguageButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(this.LanguageButton);
			foreach (LanguageInfo language in Loc.Languages)
			{
				MenuItem item = CreateCheckableItem(language.NativeName, language.Code == Loc.Language);
				item.Click += (_, _) => this._viewModel.ChangeLanguage(language.Code);
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private static ContextMenu CreateMenu(FrameworkElement target)
		{
			return new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
		}

		private static MenuItem CreateCheckableItem(String header, Boolean isCurrent)
		{
			return new MenuItem
			{
				Header = header,
				Icon = isCurrent ? new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Checkmark24 } : null,
			};
		}

		private void OnLanguageMenuOpened(Object sender, RoutedEventArgs e)
		{
			if (!ReferenceEquals(e.OriginalSource, this.LanguageMenu))
			{
				return;
			}

			this.LanguageMenu.Items.Clear();
			foreach (LanguageInfo language in Loc.Languages)
			{
				MenuItem item = CreateCheckableItem(language.NativeName, language.Code == Loc.Language);
				item.Click += (_, _) => this._viewModel.ChangeLanguage(language.Code);
				this.LanguageMenu.Items.Add(item);
			}
		}

		private void OnRecentMenuOpened(Object sender, RoutedEventArgs e)
		{
			if (!ReferenceEquals(e.OriginalSource, this.RecentMenu))
			{
				return;
			}

			this.RecentMenu.Items.Clear();
			if (this._viewModel.RecentFiles.Count == 0)
			{
				this.RecentMenu.Items.Add(new MenuItem { Header = Loc.Get("Str.Shell.Menu.Empty"), IsEnabled = false });
				return;
			}

			foreach (String path in this._viewModel.RecentFiles.Take(20))
			{
				MenuItem item = new()
				{ Header = path.Replace("_", "__", StringComparison.Ordinal), ToolTip = path };
				item.Click += (_, _) => this._viewModel.OpenRecentCommand.Execute(path);
				this.RecentMenu.Items.Add(item);
			}

			this.RecentMenu.Items.Add(new Separator());
			MenuItem clear = new()
			{ Header = Loc.Get("Str.Shell.Menu.ClearRecent") };
			clear.Click += (_, _) => this._viewModel.ClearRecentCommand.Execute(null);
			this.RecentMenu.Items.Add(clear);
		}

		private void OnRecentProjectsMenuOpened(Object sender, RoutedEventArgs e)
		{
			if (!ReferenceEquals(e.OriginalSource, this.RecentProjectsMenu))
			{
				return;
			}

			this.RecentProjectsMenu.Items.Clear();
			if (this._viewModel.RecentProjects.Count == 0)
			{
				this.RecentProjectsMenu.Items.Add(new MenuItem { Header = Loc.Get("Str.Shell.Menu.Empty"), IsEnabled = false });
				return;
			}

			foreach (String folder in this._viewModel.RecentProjects)
			{
				MenuItem item = new()
				{ Header = folder.Replace("_", "__", StringComparison.Ordinal), ToolTip = folder };
				item.Click += (_, _) => this._viewModel.OpenRecentProjectCommand.Execute(folder);
				this.RecentProjectsMenu.Items.Add(item);
			}

			this.RecentProjectsMenu.Items.Add(new Separator());
			MenuItem clear = new()
			{ Header = Loc.Get("Str.Shell.Menu.ClearRecentProjects") };
			clear.Click += (_, _) => this._viewModel.ClearRecentProjectsCommand.Execute(null);
			this.RecentProjectsMenu.Items.Add(clear);
		}

		private void OnTemplatesMenuOpened(Object sender, RoutedEventArgs e)
		{
			if (!ReferenceEquals(e.OriginalSource, this.TemplatesMenu))
			{
				return;
			}

			this.TemplatesMenu.Items.Clear();
			if (this._viewModel.Templates.Count == 0)
			{
				this.TemplatesMenu.Items.Add(new MenuItem { Header = Loc.Get("Str.Shell.Menu.NoTemplates"), IsEnabled = false });
				return;
			}

			foreach (TemplateItem template in this._viewModel.Templates)
			{
				MenuItem item = new()
				{ Header = template.Name.Replace("_", "__", StringComparison.Ordinal), ToolTip = template.Path };
				item.Click += (_, _) => this._viewModel.NewFromTemplateCommand.Execute(template);
				this.TemplatesMenu.Items.Add(item);
			}
		}

		private void OnZoomClick(Object sender, RoutedEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.Control)
			{
				this._viewModel.ZoomOutCommand.Execute(null);
			}
			else
			{
				this._viewModel.ZoomInCommand.Execute(null);
			}
		}

		private void OnZoomPreviewMouseUp(Object sender, MouseButtonEventArgs e)
		{
			// The second click of a double click resets the zoom, and it must not zoom in once more.
			if (e.ClickCount >= 2)
			{
				this._viewModel.ResetZoomCommand.Execute(null);
				e.Handled = true;
			}
		}

		private void OnGoToProcedureOpened(Object? sender, EventArgs e)
		{
			this.ProcedureFilterBox.Focus();
			this.ProcedureFilterBox.SelectAll();
		}

		private void OnProcedureFilterKeyDown(Object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Down when this.ProcedureList.Items.Count > 0:
					this.ProcedureList.SelectedIndex = Math.Max(this.ProcedureList.SelectedIndex, 0);
					(this.ProcedureList.ItemContainerGenerator.ContainerFromIndex(this.ProcedureList.SelectedIndex) as ListBoxItem)?.Focus();
					e.Handled = true;
					break;
				case Key.Enter:
					this.GoToSelectedProcedure(this.ProcedureList.SelectedItem ?? this.ProcedureList.Items.Cast<Object>().FirstOrDefault());
					e.Handled = true;
					break;
				case Key.Escape:
					this._viewModel.IsGoToProcedureOpen = false;
					e.Handled = true;
					break;
			}
		}

		private void OnProcedureListKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				this.GoToSelectedProcedure(this.ProcedureList.SelectedItem);
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				this._viewModel.IsGoToProcedureOpen = false;
				e.Handled = true;
			}
		}

		private void OnProcedureListDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			this.GoToSelectedProcedure(this.ProcedureList.SelectedItem);
		}

		private void GoToSelectedProcedure(Object? item)
		{
			if (item is ScriptProcedure procedure)
			{
				this._viewModel.GoToProcedureItemCommand.Execute(procedure);
			}
		}
	}
}