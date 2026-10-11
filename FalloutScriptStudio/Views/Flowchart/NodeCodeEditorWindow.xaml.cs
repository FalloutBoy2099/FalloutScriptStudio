namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.ComponentModel;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;
	using Wpf.Ui.Controls;
	using MenuItem = System.Windows.Controls.MenuItem;

	/// <summary>
	/// The window that edits the code of one dialogue node: a code editor with the highlighting of the script, and the grid of the
	/// messages the code uses.
	/// </summary>
	public partial class NodeCodeEditorWindow : FluentWindow, IDisposable
	{
		private static readonly Regex PlaceholderPattern = new Regex(@"\{[^{}:,\r\n]+\}", RegexOptions.Compiled);

		private readonly ScriptEditorServices _editorServices;
		private NodeCodeEditorSession? _session;
		private Window? _ownerCandidate;
		private Boolean _closeApproved;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeCodeEditorWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="editorServices">The shared services of the script editor, used for the look of the code editor.</param>
		public NodeCodeEditorWindow(NodeCodeEditorViewModel viewModel, ScriptEditorServices editorServices)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(editorServices);

			this.ViewModel = viewModel;
			this._editorServices = editorServices;
			this.DataContext = viewModel;
			this.InitializeComponent();
			viewModel.Owner = this;
			viewModel.CloseRequested += this.OnCloseRequested;
			viewModel.InsertTextRequested += this.OnInsertTextRequested;
			viewModel.SelectRangeRequested += this.OnSelectRangeRequested;
			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;

			ContextMenu menu = (ContextMenu)this.FindResource("CodeMenu");
			menu.DataContext = viewModel;
			menu.Opened += this.OnCodeMenuOpened;
			this.CodeEditor.ContextMenu = menu;
			foreach (MenuItem item in menu.Items.OfType<MenuItem>().Where(i => i.Command is RoutedCommand))
			{
				item.CommandTarget = this.CodeEditor.TextArea;
			}

			this.Closing += this.OnClosing;
			this.Closed += this.OnClosed;
		}

		/// <summary>Gets the view model.</summary>
		public NodeCodeEditorViewModel ViewModel { get; }

		/// <summary>Gets or sets a value indicating whether the window must close, so closing it never has a Cancel choice.</summary>
		public Boolean ForceClose { get; set; }

		/// <summary>Opens the code of a node in the editor.</summary>
		/// <param name="script">The script.</param>
		/// <param name="nodeName">The node procedure name.</param>
		/// <param name="ownerCandidate">The window the editor stays above while "Window On Top" is on.</param>
		/// <returns>A task that completes when the code is shown.</returns>
		public async Task InitializeAsync(DocumentViewModel script, String nodeName, Window? ownerCandidate)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);

			this._ownerCandidate = ownerCandidate;
			await this.ViewModel.InitializeAsync(script, nodeName);
			this._session = new NodeCodeEditorSession(this.CodeEditor, this.ViewModel.Code, script, this._editorServices);
			this._session.Start();
		}

		/// <summary>
		/// Closes the window for good: asks whether to apply unsaved changes (Yes / No only) and waits for the answer. The window closes
		/// even when the changes could not be applied, because the script it belongs to is going away.
		/// </summary>
		/// <returns>A task that completes when the window is closed.</returns>
		public async Task CloseAsync()
		{
			this.ForceClose = true;
			if (this.ViewModel.HasChanges)
			{
				if (!this.IsActive)
				{
					this.Activate();
				}

				_ = await this.ViewModel.ConfirmCloseAsync(true);
			}

			this._closeApproved = true;
			this.Close();
		}

		private void OnNodeListDropDownClosed(Object? sender, EventArgs e)
		{
			this.CodeEditor.Focus();
			this.ViewModel.NodeChoiceCommitted(!this.CodeEditor.TextArea.Selection.IsEmpty);
		}

		private void OnCloseRequested(Object? sender, Boolean accepted)
		{
			this._closeApproved = true;
			this.Close();
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (this._closeApproved || !this.ViewModel.HasChanges)
			{
				return;
			}

			// The question is asked after this event finished: a window cannot be closed again while it is closing.
			e.Cancel = true;
			_ = this.Dispatcher.InvokeAsync(this.ConfirmAndCloseAsync);
		}

		private async Task ConfirmAndCloseAsync()
		{
			if (!this.IsActive)
			{
				this.Activate();
			}

			if (await this.ViewModel.ConfirmCloseAsync(this.ForceClose))
			{
				this._closeApproved = true;
				this.Close();
			}
		}

		private void OnClosed(Object? sender, EventArgs e)
		{
			this.ViewModel.CloseRequested -= this.OnCloseRequested;
			this.ViewModel.InsertTextRequested -= this.OnInsertTextRequested;
			this.ViewModel.SelectRangeRequested -= this.OnSelectRangeRequested;
			this.ViewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
			this.Dispose();
		}

		/// <summary>Ends the editing session of the code editor.</summary>
		public void Dispose()
		{
			this._session?.Dispose();
			this._session = null;
			GC.SuppressFinalize(this);
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(NodeCodeEditorViewModel.IsOnTop))
			{
				this.Owner = this.ViewModel.IsOnTop ? this._ownerCandidate : null;
			}
		}

		private void OnInsertTextRequested(Object? sender, String text)
		{
			TextArea area = this.CodeEditor.TextArea;
			TextDocument document = this.CodeEditor.Document;
			Int32 offset = area.Caret.Offset;
			using (document.RunUpdate())
			{
				if (!area.Selection.IsEmpty)
				{
					ISegment selection = area.Selection.SurroundingSegment;
					offset = selection.Offset;
					document.Remove(selection.Offset, selection.Length);
				}

				document.Insert(offset, text);
			}

			area.Caret.Offset = offset + text.Length;
			Match placeholder = PlaceholderPattern.Match(text);
			if (placeholder.Success)
			{
				area.Caret.Offset = offset + placeholder.Index;
				area.Selection = Selection.Create(area, offset + placeholder.Index, offset + placeholder.Index + placeholder.Length);
			}

			this.CodeEditor.Focus();
		}

		private void OnSelectRangeRequested(Object? sender, (Int32 Offset, Int32 Length) range)
		{
			Int32 offset = Math.Min(range.Offset, this.CodeEditor.Document.TextLength);
			Int32 length = Math.Min(range.Length, this.CodeEditor.Document.TextLength - offset);
			this.CodeEditor.Select(offset, length);
			this.CodeEditor.ScrollToLine(this.CodeEditor.Document.GetLineByOffset(offset).LineNumber);
		}

		private void OnCodeMenuOpened(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = (ContextMenu)sender;
			MenuItem? store = menu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Tag, "store"));
			if (store is not null)
			{
				store.IsEnabled = this.ViewModel.CanStoreSnippets && !this.CodeEditor.TextArea.Selection.IsEmpty;
			}

			MenuItem? macros = menu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Tag, "macros"));
			if (macros is not null)
			{
				this.FillMacroMenu(macros);
			}
		}

		private void FillMacroMenu(MenuItem menu)
		{
			menu.Items.Clear();
			MenuItem custom = new MenuItem { Header = Loc.Get("Str.Flowchart.Menu.Custom"), IsEnabled = this.ViewModel.Snippets.Count > 0 };
			foreach (NodeCodeSnippet snippet in this.ViewModel.Snippets)
			{
				custom.Items.Add(new MenuItem { Header = snippet.Name, Command = this.ViewModel.InsertTextCommand, CommandParameter = snippet.Code });
			}

			menu.Items.Add(custom);
			foreach (NodeEditorMacro macro in NodeEditorMacro.All)
			{
				if (macro.StartsGroup || menu.Items.Count == 1)
				{
					menu.Items.Add(new Separator());
				}

				menu.Items.Add(new MenuItem { Header = macro.Header, Command = this.ViewModel.InsertTextCommand, CommandParameter = macro.Template });
			}
		}

		private async void OnStoreSnippetClick(Object sender, RoutedEventArgs e)
		{
			await this.ViewModel.StoreSnippetAsync(this.CodeEditor.SelectedText);
		}
	}
}
