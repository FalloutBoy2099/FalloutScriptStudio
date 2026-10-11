namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The headers picker: a searchable list of the header files. Like the popup of the old editor it remembers its size
	/// and closes when it loses the focus.
	/// </summary>
	public partial class HeadersWindow : FluentWindow
	{
		private readonly HeadersViewModel _viewModel;
		private readonly ISettingsService _settingsService;
		private Boolean _closing;

		/// <summary>
		/// Initializes a new instance of the <see cref="HeadersWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="settingsService">Stores the window size.</param>
		public HeadersWindow(HeadersViewModel viewModel, ISettingsService settingsService)
		{
			ArgumentNullException.ThrowIfNull(settingsService);

			this._viewModel = viewModel;
			this._settingsService = settingsService;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
			ToolWindowHelper.ApplySize(this, settingsService.Settings.Window.GetPlacement(WindowKind.Headers));
		}

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.FilterBox.Focus();
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			this._closing = true;
			WindowPlacement placement = ToolWindowHelper.Capture(this);
			this._settingsService.Update(settings => settings.Window.SetPlacement(WindowKind.Headers, placement));
		}

		private void OnDeactivated(Object? sender, EventArgs e)
		{
			// The old popup closed as soon as it lost the focus.
			if (this.IsLoaded && !this._closing && this.IsVisible)
			{
				this._closing = true;
				ToolWindowHelper.Close(this, false);
			}
		}

		private async void OnListDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			// Ctrl keeps the window open (legacy behaviour), see HeadersViewModel.ChooseAsync.
			await this._viewModel.ChooseAsync((Keyboard.Modifiers & ModifierKeys.Control) != 0);
		}

		private async void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && this._viewModel.KeepOpenHandler is not null)
			{
				e.Handled = true;
				await this._viewModel.ChooseAsync(true);
				return;
			}

			if (e.Key == Key.Down || e.Key == Key.Up)
			{
				// Let the arrow keys move the selection while the search box has the focus.
				if (this.FilterBox.IsKeyboardFocusWithin)
				{
					this._viewModel.MoveSelection(e.Key == Key.Down ? 1 : -1);
					if (this._viewModel.SelectedItem != null)
					{
						this.HeaderList.ScrollIntoView(this._viewModel.SelectedItem);
					}

					e.Handled = true;
				}
			}
		}
	}
}
