namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.ComponentModel;
	using System.Threading.Tasks;
	using System.Windows.Threading;
	using Wpf.Ui.Controls;
	using FalloutScriptStudio.Editor;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The snippet manager: the list of the built-in, user and project snippets on the left, the trigger, the description and the body of
	/// the selected snippet on the right. The body is edited in a code editor with the highlighting of the scripts.
	/// </summary>
	public partial class SnippetManagerWindow : FluentWindow
	{
		private readonly SnippetManagerViewModel _viewModel;
		private readonly SnippetBodyEditorSession? _session;
		private SnippetRowViewModel? _row;
		private Boolean _updatingEditor;
		private Boolean _asking;

		/// <summary>Initializes a new instance of the <see cref="SnippetManagerWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="editorServices">The shared services of the script editor, used for the look of the body editor; none keeps the plain look.</param>
		public SnippetManagerWindow(SnippetManagerViewModel viewModel, ScriptEditorServices? editorServices = null)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
			if (editorServices is not null)
			{
				this._session = new SnippetBodyEditorSession(this.BodyEditor, editorServices);
				this._session.Start();
			}

			this.BodyEditor.TextChanged += this.OnBodyTextChanged;
			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
			this.ShowRow(viewModel.SelectedRow);
			this.Closing += this.OnClosing;
			this.Closed += this.OnClosed;
		}

		/// <summary>The close button (and Alt+F4) asks what to do with unsaved changes, like the Cancel button and Esc do.</summary>
		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (this._asking)
			{
				e.Cancel = true;
				return;
			}

			if (this._viewModel.NeedsCloseConfirmation)
			{
				// The question is asynchronous: the close is cancelled now and repeated once the user agreed to drop the changes.
				e.Cancel = true;
				_ = this.ConfirmAndCloseAsync();
			}
		}

		private async Task ConfirmAndCloseAsync()
		{
			this._asking = true;
			try
			{
				if (await this._viewModel.ConfirmCloseAsync())
				{
					// Not inside the Closing event of the close that asked the question (a window cannot be closed while it is closing).
					_ = this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.Close));
				}
			}
			finally
			{
				this._asking = false;
			}
		}

		private void OnClosed(Object? sender, EventArgs e)
		{
			this._viewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
			this.BodyEditor.TextChanged -= this.OnBodyTextChanged;
			if (this._row is not null)
			{
				this._row.PropertyChanged -= this.OnRowPropertyChanged;
				this._row = null;
			}

			this._session?.Dispose();
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(SnippetManagerViewModel.SelectedRow))
			{
				this.ShowRow(this._viewModel.SelectedRow);
			}
		}

		private void OnRowPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(SnippetRowViewModel.Body) && this._row is not null && !String.Equals(this.BodyEditor.Text, this._row.Body, StringComparison.Ordinal))
			{
				this.SetEditorText(this._row.Body);
			}
		}

		private void ShowRow(SnippetRowViewModel? row)
		{
			if (this._row is not null)
			{
				this._row.PropertyChanged -= this.OnRowPropertyChanged;
			}

			this._row = row;
			if (row is not null)
			{
				row.PropertyChanged += this.OnRowPropertyChanged;
			}

			this.BodyEditor.IsReadOnly = row is not { IsEditable: true };
			this.SetEditorText(row?.Body ?? String.Empty);
		}

		private void SetEditorText(String text)
		{
			this._updatingEditor = true;
			try
			{
				this.BodyEditor.Text = text;
			}
			finally
			{
				this._updatingEditor = false;
			}
		}

		private void OnBodyTextChanged(Object? sender, EventArgs e)
		{
			if (!this._updatingEditor && this._row is { IsEditable: true } row)
			{
				row.Body = this.BodyEditor.Text;
			}
		}
	}
}