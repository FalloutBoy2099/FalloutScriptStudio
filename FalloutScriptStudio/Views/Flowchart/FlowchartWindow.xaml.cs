namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using MenuItem = System.Windows.Controls.MenuItem;
	using Controls.Diagram;
	using Core.Configuration;
	using Core.Dialogue;
	using Documents;
	using Localization;
	using ViewModels.Flowchart;
	using ToolWindows;

	/// <summary>
	/// The dialogue flowchart window: a toolbar, the diagram canvas and the context menu of the nodes.
	/// </summary>
	public partial class FlowchartWindow : FluentWindow
	{
		private readonly ISettingsService _settings;
		private DocumentViewModel? _script;
		private Boolean _loaded;
		private Boolean _closing;

		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="settings">Stores the placement of the window.</param>
		public FlowchartWindow(FlowchartViewModel viewModel, ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(settings);

			this.ViewModel = viewModel;
			this._settings = settings;
			this.DataContext = viewModel;
			this.InitializeComponent();

			WindowPlacement? placement = settings.Settings.Window.GetPlacement(WindowKind.Flowchart);
			if (placement is null)
			{
				this.WindowState = WindowState.Maximized;
			}
			else
			{
				ToolWindowHelper.ApplySize(this, placement);
				if (placement.IsMaximized)
				{
					this.WindowState = WindowState.Maximized;
				}
			}

			ContextMenu nodeMenu = (ContextMenu)this.FindResource("NodeMenu");
			nodeMenu.DataContext = viewModel;
			this.Canvas.ContextMenu = nodeMenu;
			nodeMenu.Opened += this.OnNodeMenuOpened;
			this.Canvas.ContextMenuOpening += this.OnCanvasContextMenuOpening;
			this.Canvas.RowClicked += this.OnRowClicked;
			this.Canvas.DeleteRequested += this.OnDeleteRequested;
			this.Canvas.LayoutChanged += this.OnCanvasChanged;
			this.Canvas.ViewportChanged += this.OnCanvasChanged;

			this.Loaded += this.OnLoaded;
			this.Activated += this.OnActivated;
			this.Closing += this.OnClosing;
			this.Closed += this.OnClosed;
		}

		/// <summary>Gets the view model.</summary>
		public FlowchartViewModel ViewModel { get; }

		/// <summary>Gets the script the window shows.</summary>
		public DocumentViewModel? Script => this._script;

		/// <summary>Sets the script to show; call it before the window is shown.</summary>
		/// <param name="script">The script.</param>
		public void Initialize(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);
			this._script = script;
		}

		private static void ShowMenu(ContextMenu menu, FrameworkElement target, Object dataContext)
		{
			menu.DataContext = dataContext;
			menu.PlacementTarget = target;
			menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			menu.IsOpen = true;
		}

		private async void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (this._loaded || this._script is null)
			{
				return;
			}

			this._loaded = true;
			this.ViewModel.Attach(this._script, this.Canvas);
			await this.ViewModel.LoadAsync();
			this.Canvas.Focus();
		}

		private async void OnActivated(Object? sender, EventArgs e)
		{
			if (this._loaded)
			{
				await this.ViewModel.OnActivatedAsync();
			}
		}

		private void OnClosing(Object? sender, System.ComponentModel.CancelEventArgs e)
		{
			if (this._closing)
			{
				return;
			}

			this._closing = true;
			this.ViewModel.OnClosing();
			WindowPlacement placement = ToolWindowHelper.Capture(this);
			this._settings.Update(settings => settings.Window.SetPlacement(WindowKind.Flowchart, placement));
		}

		private void OnClosed(Object? sender, EventArgs e)
		{
			this.ViewModel.Dispose();
		}

		private void OnCloseClick(Object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void OnFileClick(Object sender, RoutedEventArgs e)
		{
			ShowMenu((ContextMenu)this.FindResource("FileMenu"), this.FileButton, this.ViewModel);
		}

		private void OnAddClick(Object sender, RoutedEventArgs e)
		{
			ShowMenu((ContextMenu)this.FindResource("AddMenu"), this.AddButton, this.ViewModel);
		}

		private void OnOptionsClick(Object sender, RoutedEventArgs e)
		{
			ShowMenu((ContextMenu)this.FindResource("OptionsMenu"), this.OptionsButton, this.ViewModel);
		}

		private void OnHelpClick(Object sender, RoutedEventArgs e)
		{
			this.HelpPopup.PlacementTarget = this.Canvas;
			this.HelpPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
			this.HelpPopup.HorizontalOffset = Math.Max(0, this.Canvas.ActualWidth - 320);
			this.HelpPopup.VerticalOffset = 6;
			this.HelpPopup.IsOpen = true;
		}

		private void OnCollapseClick(Object sender, RoutedEventArgs e)
		{
			this.ViewModel.CollapseAllCommand.Execute((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
		}

		private void OnExpandClick(Object sender, RoutedEventArgs e)
		{
			this.ViewModel.ExpandAllCommand.Execute((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
		}

		private void OnCanvasContextMenuOpening(Object sender, ContextMenuEventArgs e)
		{
			if (this._loaded)
			{
				this.ViewModel.PrepareContext(this.Canvas.HoveredItem);
			}
		}

		private void OnNodeMenuOpened(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = (ContextMenu)sender;
			this.FillJumpMenu(menu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Tag, "jump")));
			this.FillUnhideMenu(menu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Tag, "unhide")));
		}

		private void FillJumpMenu(MenuItem? menu)
		{
			if (menu is null)
			{
				return;
			}

			menu.Items.Clear();
			if (this.ViewModel.JumpToTargets.Count > 0)
			{
				menu.Items.Add(CreateCaption(Loc.Get("Str.Flowchart.Menu.Jump.LinkTo")));
				foreach (FlowchartJumpTarget target in this.ViewModel.JumpToTargets)
				{
					menu.Items.Add(this.CreateJumpItem(target, "SystemFillColorCautionBrush"));
				}
			}

			if (this.ViewModel.JumpFromTargets.Count > 0)
			{
				menu.Items.Add(CreateCaption(Loc.Get("Str.Flowchart.Menu.Jump.LinkFrom")));
				foreach (FlowchartJumpTarget target in this.ViewModel.JumpFromTargets)
				{
					menu.Items.Add(this.CreateJumpItem(target, "AccentTextFillColorPrimaryBrush"));
				}
			}

			if (menu.Items.Count == 0)
			{
				menu.Items.Add(CreateCaption(Loc.Get("Str.Flowchart.Menu.Jump.NoLinks")));
			}
		}

		private void FillUnhideMenu(MenuItem? menu)
		{
			if (menu is null)
			{
				return;
			}

			menu.Items.Clear();
			foreach (String name in this.ViewModel.HiddenNodeNames)
			{
				MenuItem item = new()
				{ Header = name, Command = this.ViewModel.UnhideNodeCommand, CommandParameter = name };
				menu.Items.Add(item);
			}

			menu.IsEnabled = menu.Items.Count > 0;
		}

		private static MenuItem CreateCaption(String text)
		{
			return new MenuItem { Header = text, IsEnabled = false };
		}

		private MenuItem CreateJumpItem(FlowchartJumpTarget target, String brushKey)
		{
			MenuItem item = new()
			{ Header = target.Name, Command = this.ViewModel.JumpToNodeCommand, CommandParameter = target.Name };
			item.SetResourceReference(Control.ForegroundProperty, target.IsHidden ? "TextFillColorDisabledBrush" : brushKey);
			return item;
		}

		private void OnRowClicked(Object? sender, DiagramRowEventArgs e)
		{
			if (e.Button != MouseButton.Left || e.Part != DiagramHitPart.Row)
			{
				return;
			}

			if ((e.Modifiers & ModifierKeys.Shift) != 0)
			{
				_ = this.ViewModel.QuickEditMessageAsync(e.Row);
			}
			else if (e.IsTextTrimmed && e.Row.Tag is FlowchartContentLine line && line.Opcode.HasMessage())
			{
				this.MessagePopupText.Text = e.Row.Text;
				this.MessagePopup.PlacementTarget = this.Canvas;
				this.MessagePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
				this.MessagePopup.HorizontalOffset = e.ViewportBounds.X;
				this.MessagePopup.VerticalOffset = e.ViewportBounds.Bottom;
				this.MessagePopup.IsOpen = true;
			}
		}

		private void OnCanvasChanged(Object? sender, EventArgs e)
		{
			this.MessagePopup.IsOpen = false;
			this.HelpPopup.IsOpen = false;
		}

		private void OnDeleteRequested(Object? sender, EventArgs e)
		{
			this.ViewModel.DeleteSelectionCommand.Execute(null);
		}
	}
}