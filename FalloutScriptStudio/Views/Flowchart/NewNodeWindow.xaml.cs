namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The dialog that asks for the name and the starting code of a new dialogue node.
	/// </summary>
	public partial class NewNodeWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NewNodeWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public NewNodeWindow(NewNodeViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			viewModel.CloseRequested += this.OnCloseRequested;
			this.Loaded += this.OnLoaded;
		}

		/// <summary>Gets the view model.</summary>
		public NewNodeViewModel ViewModel { get; }

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.NameBox.Focus();
			Int32 hash = this.NameBox.Text.IndexOf('#', StringComparison.Ordinal);
			if (hash >= 0 && String.Equals(this.NameBox.Text, DialogueNodeTemplate.DefaultNodeName, StringComparison.Ordinal))
			{
				this.NameBox.Select(hash, this.NameBox.Text.Length - hash);
			}
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
