namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The dialog that edits the text of one message.
	/// </summary>
	public partial class MessageTextWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MessageTextWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public MessageTextWindow(MessageTextViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			viewModel.CloseRequested += this.OnCloseRequested;
			this.Loaded += this.OnLoaded;
		}

		/// <summary>Gets the view model.</summary>
		public MessageTextViewModel ViewModel { get; }

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.TextBox.Focus();
			this.TextBox.SelectAll();
		}

		private void OnCloseRequested(Object? sender, Boolean accepted)
		{
			try
			{
				this.DialogResult = accepted;
			}
			catch (InvalidOperationException)
			{
				// The window was not shown with ShowDialog; closing it is enough.
			}

			if (this.IsVisible)
			{
				this.Close();
			}
		}
	}
}
