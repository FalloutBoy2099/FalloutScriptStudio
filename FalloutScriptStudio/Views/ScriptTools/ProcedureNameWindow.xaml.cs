namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.Windows;
	using Wpf.Ui.Controls;
	using ViewModels.ScriptTools;

	/// <summary>
	/// The procedure name dialog.
	/// </summary>
	public partial class ProcedureNameWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProcedureNameWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public ProcedureNameWindow(ProcedureNameViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			ScriptToolWindowHelper.CloseWhenRequested(this, viewModel);
			this.Loaded += this.OnLoaded;
		}

		/// <summary>Gets the view model.</summary>
		public ProcedureNameViewModel ViewModel { get; }

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.NameBox.Focus();
			if (this.ViewModel.NameReadOnly)
			{
				return;
			}

			this.NameBox.SelectionStart = this.NameBox.Text.Length;
		}
	}
}