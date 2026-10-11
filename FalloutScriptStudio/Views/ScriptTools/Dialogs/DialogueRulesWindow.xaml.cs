namespace FalloutScriptStudio.Views.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Specialized;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Threading;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The dialogue function rules window.
	/// </summary>
	public partial class DialogueRulesWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueRulesWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public DialogueRulesWindow(DialogueRulesViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.InitializeComponent();
			DialogueWindowHelper.Attach(this, viewModel);
			viewModel.Rules.CollectionChanged += this.OnRulesChanged;
			this.Closed += (_, _) => viewModel.Rules.CollectionChanged -= this.OnRulesChanged;
		}

		/// <summary>Gets the view model.</summary>
		public DialogueRulesViewModel ViewModel { get; }

		private void OnRulesChanged(Object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is not { Count: > 0 })
			{
				return;
			}

			Object added = e.NewItems[0]!;
			this.Dispatcher.BeginInvoke(
				DispatcherPriority.Loaded,
				new Action(() =>
				{
					this.RulesGrid.ScrollIntoView(added);
					this.RulesGrid.SelectedItem = added;
					this.RulesGrid.CurrentCell = new DataGridCellInfo(added, this.NameColumn);
					this.RulesGrid.Focus();
					this.RulesGrid.BeginEdit();
				}));
		}

		private void OnSaveClick(Object sender, RoutedEventArgs e)
		{
			this.RulesGrid.CommitEdit(DataGridEditingUnit.Row, true);
		}
	}
}
