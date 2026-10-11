namespace FalloutScriptStudio.Views.Editor
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Localization;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Editing;
	using ICSharpCode.AvalonEdit.Rendering;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The script text editor: a Fluent themed AvalonEdit control for one <see cref="DocumentViewModel"/> (the
	/// <see cref="FrameworkElement.DataContext"/>) with syntax highlighting, folding, tooltips, auto-completion,
	/// code templates, navigation and the editor commands. The view can be destroyed and created again (tab
	/// virtualization) without losing the caret, scroll position, folds or undo history.
	/// </summary>
	public partial class ScriptEditorView : UserControl, IDisposable
	{
		/// <summary>
		/// Identifies the inherited attached property that supplies the shared editor services to views created without
		/// constructor arguments (for example from a <c>DataTemplate</c>). Set it once on a parent element, for example the main window.
		/// </summary>
		public static readonly DependencyProperty ServicesProperty = DependencyProperty.RegisterAttached(
			"Services",
			typeof(ScriptEditorServices),
			typeof(ScriptEditorView),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

		private readonly ScriptEditorServices? _services;
		private ScriptEditorServices? _activeServices;
		private EditorSession? _session;
		private EditorSurface? _surface;
		private DocumentViewModel? _boundDocument;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptEditorView"/> class that takes its services from the
		/// inherited <see cref="ServicesProperty"/>; used by XAML data templates.
		/// </summary>
		public ScriptEditorView()
			: this(null)
		{
		}

		/// <summary>Initializes a new instance of the <see cref="ScriptEditorView"/> class.</summary>
		/// <param name="services">The shared editor services (resolve them from the service provider).</param>
		public ScriptEditorView(ScriptEditorServices? services)
		{
			this._services = services;
			this.InitializeComponent();
			this.RegisterCommandBindings();

			this.Loaded += this.OnLoaded;
			this.Unloaded += this.OnUnloaded;
			this.DataContextChanged += this.OnDataContextChanged;

			this.PrimaryEditor.PreviewMouseWheel += this.OnPreviewMouseWheel;
			this.PrimaryEditor.TextArea.PreviewMouseDown += this.OnTextAreaPreviewMouseDown;
			this.PrimaryEditor.TextArea.PreviewMouseRightButtonDown += this.OnTextAreaPreviewMouseRightButtonDown;
			this.SecondaryEditor.PreviewMouseWheel += this.OnPreviewMouseWheel;
			this.SecondaryEditor.TextArea.PreviewMouseDown += this.OnTextAreaPreviewMouseDown;
			this.SecondaryEditor.TextArea.PreviewMouseRightButtonDown += this.OnTextAreaPreviewMouseRightButtonDown;
		}

		/// <summary>Gets the editor services that were set on an element (inherited by its descendants).</summary>
		/// <param name="element">The element.</param>
		/// <returns>The services, or <see langword="null"/>.</returns>
		public static ScriptEditorServices? GetServices(DependencyObject element)
		{
			return (ScriptEditorServices?)element.GetValue(ServicesProperty);
		}

		/// <summary>Sets the editor services for an element and all editors below it.</summary>
		/// <param name="element">The element, for example the main window.</param>
		/// <param name="value">The services.</param>
		public static void SetServices(DependencyObject element, ScriptEditorServices? value)
		{
			element.SetValue(ServicesProperty, value);
		}

		/// <summary>Gets the editor surface of the shown document, or <see langword="null"/> while the view is not loaded.</summary>
		public IEditorSurface? Surface => this._surface;

		/// <summary>Gets the AvalonEdit control (for tests and diagnostics).</summary>
		public TextEditor Editor => this.PrimaryEditor;

		/// <summary>Gets the running editor session (for tests and diagnostics), or <see langword="null"/>.</summary>
		public EditorSession? Session => this._session;

		private void RegisterCommandBindings()
		{
			foreach (System.Collections.Generic.KeyValuePair<EditorCommand, RoutedUICommand> pair in EditorCommands.All)
			{
				EditorCommand command = pair.Key;
				this.CommandBindings.Add(new CommandBinding(
					pair.Value,
					(_, _) => this._surface?.Execute(command),
					(_, e) =>
					{
						e.CanExecute = this._surface?.CanExecute(command) ?? false;
						e.Handled = true;
					}));
			}

			this.CommandBindings.Add(new CommandBinding(
				EditorCommands.NavigateBack,
				(_, _) => this._surface?.NavigateBack(),
				(_, e) => e.CanExecute = this._surface?.CanNavigateBack ?? false));
			this.CommandBindings.Add(new CommandBinding(
				EditorCommands.NavigateForward,
				(_, _) => this._surface?.NavigateForward(),
				(_, e) => e.CanExecute = this._surface?.CanNavigateForward ?? false));
		}

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.StartSession();
		}

		/// <summary>Ends the editing session of the view: detaches it from the document and releases the controllers.</summary>
		public void Dispose()
		{
			this.StopSession();
			GC.SuppressFinalize(this);
		}

		private void OnUnloaded(Object sender, RoutedEventArgs e)
		{
			this.StopSession();
		}

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			this.StopSession();
			if (this.IsLoaded)
			{
				this.StartSession();
			}
		}

		private void StartSession()
		{
			if (this._session is not null || this.DataContext is not DocumentViewModel document)
			{
				return;
			}

			this._boundDocument = document;
			ScriptEditorServices services = this._services ?? GetServices(this)
				?? throw new InvalidOperationException("ScriptEditorView needs ScriptEditorServices: pass them to the constructor or set ScriptEditorView.Services on a parent element.");
			this._activeServices = services;
			this._session = new EditorSession(this.PrimaryEditor, document, services, this.ToggleSplit);
			this._surface = new EditorSurface(this._session);
			this._session.Start();
			this.PrimaryEditor.ContextMenu = this.BuildContextMenu(this.PrimaryEditor);
			document.Editor = this._surface;
			if (document.ViewState.IsSplit)
			{
				this.ShowSplit();
			}

			// The scroll position can only be restored once the control has been measured.
			Dispatcher.BeginInvoke(
				System.Windows.Threading.DispatcherPriority.Loaded,
				new Action(() =>
				{
					this._session?.Navigation.CompleteRestoreAfterLayout();
					this._session?.CompleteSecondaryRestore();
					this.FocusUnlessTypingElsewhere();
				}));
		}

		/// <summary>Gives the focus to the editor (legacy: selecting a tab focused its text area) unless the user is typing in another text box.</summary>
		private void FocusUnlessTypingElsewhere()
		{
			if (this._session is null || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.PasswordBox or System.Windows.Controls.ComboBox)
			{
				return;
			}

			this.PrimaryEditor.TextArea.Focus();
		}

		private void StopSession()
		{
			if (this._session is null)
			{
				return;
			}

			// The split stays in the view state, so the next view of the document shows it again.
			this.CollapseSplit(true);
			if (this._boundDocument is not null && Object.ReferenceEquals(this._boundDocument.Editor, this._surface))
			{
				this._boundDocument.Editor = null;
			}

			this._session.Dispose();
			this._session = null;
			this._surface = null;
			this._boundDocument = null;
			this.PrimaryEditor.ContextMenu = null;
			this.SecondaryEditor.ContextMenu = null;
		}

		private void OnPreviewMouseWheel(Object sender, MouseWheelEventArgs e)
		{
			if (Keyboard.Modifiers != ModifierKeys.Control)
			{
				return;
			}

			Int32 step = e.Delta > 0 ? 10 : -10;
			if (this._activeServices is not null)
			{
				this._activeServices.ZoomPercent += step;
			}

			e.Handled = true;
		}

		/// <summary>Finds the pane of an editor session that a text area of this view belongs to.</summary>
		private EditorPane? FindPane(Object sender)
		{
			TextEditor? editor = sender switch
			{
				TextArea area when Object.ReferenceEquals(area, this.PrimaryEditor.TextArea) => this.PrimaryEditor,
				TextArea area when Object.ReferenceEquals(area, this.SecondaryEditor.TextArea) => this.SecondaryEditor,
				TextEditor owner => owner,
				_ => null,
			};
			return editor is null ? null : this._session?.FindPane(editor);
		}

		private void OnTextAreaPreviewMouseDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton != MouseButton.Middle || this._session is null || this.FindPane(sender) is not EditorPane pane)
			{
				return;
			}

			TextView view = pane.Context.TextView;
			TextViewPosition? position = view.GetPositionFloor(e.GetPosition(view) + view.ScrollOffset);
			this._session.Activate(pane);
			this._session.Occurrences.ToggleHighlight(position is null ? -1 : this._session.Context.Document.GetOffset(position.Value.Location), pane.Context);
			e.Handled = true;
		}

		private void OnTextAreaPreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			if (this._session is null || this.FindPane(sender) is not EditorPane pane)
			{
				return;
			}

			TextView view = pane.Context.TextView;
			TextViewPosition? position = view.GetPositionFloor(e.GetPosition(view) + view.ScrollOffset);
			if (position is null)
			{
				return;
			}

			TextEditor editor = pane.Context.Editor;
			Int32 offset = this._session.Context.Document.GetOffset(position.Value.Location);
			Int32 start = editor.SelectionStart;
			Boolean insideSelection = editor.SelectionLength > 0 && offset >= start && offset <= start + editor.SelectionLength;
			if (!insideSelection)
			{
				editor.TextArea.ClearSelection();
				editor.TextArea.Caret.Offset = offset;
			}

			// The commands of the menu act on the pane that has the focus.
			this._session.Activate(pane);
			editor.TextArea.Focus();
		}

		private ContextMenu BuildContextMenu(TextEditor owner)
		{
			ContextMenu menu = new ContextMenu();
			System.Windows.Controls.MenuItem highlightItem = this.CreateItem(EditorCommand.HighlightWord, SymbolRegular.Highlight24);
			System.Windows.Controls.MenuItem renameItem = this.CreateItem(EditorCommand.RenameSymbol, SymbolRegular.Rename24);
			menu.Items.Add(highlightItem);
			menu.Items.Add(renameItem);
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateItem(EditorCommand.GoToDeclaration, SymbolRegular.ArrowRight24));
			menu.Items.Add(this.CreateItem(EditorCommand.GoToDefinition, SymbolRegular.Code24));
			menu.Items.Add(this.CreateItem(EditorCommand.FindReferences, SymbolRegular.SearchSquare24));
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateItem(EditorCommand.OpenInclude, SymbolRegular.FolderOpen24));
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateItem(EditorCommand.ConvertHexDec, SymbolRegular.NumberSymbol24));
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateItem(EditorCommand.UpperCase, SymbolRegular.TextCaseUppercase24));
			menu.Items.Add(this.CreateItem(EditorCommand.LowerCase, SymbolRegular.TextCaseLowercase24));
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateClipboardItem(ApplicationCommands.Cut, SymbolRegular.Cut24, "Str.Common.Cut", owner));
			menu.Items.Add(this.CreateClipboardItem(ApplicationCommands.Copy, SymbolRegular.Copy24, "Str.Common.Copy", owner));
			menu.Items.Add(this.CreateClipboardItem(ApplicationCommands.Paste, SymbolRegular.ClipboardPaste24, "Str.Common.Paste", owner));
			menu.Items.Add(new Separator());
			menu.Items.Add(this.CreateItem(EditorCommand.CommentSelection, SymbolRegular.CommentAdd24));
			menu.Items.Add(this.CreateItem(EditorCommand.UncommentSelection, SymbolRegular.CommentOff24));
			menu.Items.Add(this.CreateItem(EditorCommand.AlignSelectionLeft, SymbolRegular.TextAlignLeft24));
			menu.Items.Add(this.CreateItem(EditorCommand.FormatSelection, SymbolRegular.TextEditStyle24));
			menu.Opened += (_, _) => this.UpdateContextMenu(owner, highlightItem, renameItem);
			return menu;
		}

		private System.Windows.Controls.MenuItem CreateItem(EditorCommand command, SymbolRegular symbol)
		{
			RoutedUICommand routed = EditorCommands.Get(command);
			System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
			{
				Command = routed,
				CommandTarget = this,
				Icon = new SymbolIcon(symbol),
			};
			item.SetResourceReference(HeaderedItemsControl.HeaderProperty, EditorCommands.GetTextKey(routed));
			return item;
		}

		private System.Windows.Controls.MenuItem CreateClipboardItem(RoutedUICommand command, SymbolRegular symbol, String headerKey, TextEditor owner)
		{
			System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
			{
				Command = command,
				CommandTarget = owner.TextArea,
				Icon = new SymbolIcon(symbol),
			};
			item.SetResourceReference(HeaderedItemsControl.HeaderProperty, headerKey);
			return item;
		}

		private void UpdateContextMenu(TextEditor owner, System.Windows.Controls.MenuItem highlightItem, System.Windows.Controls.MenuItem renameItem)
		{
			if (this._session?.FindPane(owner) is not EditorPane pane)
			{
				return;
			}

			Boolean selected = owner.SelectionLength > 0;
			highlightItem.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
			renameItem.Visibility = selected ? Visibility.Collapsed : Visibility.Visible;

			String key = "Str.Editor.Cmd.RenameSymbol";
			IScriptSymbol? symbol = pane.Navigation.FindSymbol(owner.TextArea.Caret.Offset, out _);
			if (symbol is not null)
			{
				key = symbol.Kind switch
				{
					SymbolKind.Procedure => "Str.Editor.Menu.RenameProcedure",
					SymbolKind.LocalVariable => symbol is ScriptVariable { IsArgument: true } ? "Str.Editor.Menu.RenameArgument" : "Str.Editor.Menu.RenameLocal",
					SymbolKind.GlobalVariable => "Str.Editor.Menu.RenameScriptVariable",
					SymbolKind.Macro => "Str.Editor.Menu.RenameMacro",
					_ => key,
				};
			}
			else if (this._session.Context.IsScript)
			{
				key = this._session.Context.ProgramInfo is null ? "Str.Editor.Menu.RenameOutOfData" : "Str.Editor.Menu.RenameNone";
			}

			renameItem.Header = Loc.Get(key);
		}

		private void ToggleSplit()
		{
			if (this._session is null)
			{
				return;
			}

			if (this._session.IsSplit)
			{
				this.HideSplit();
			}
			else
			{
				this.ShowSplit();
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => this._session?.CompleteSecondaryRestore()));
			}
		}

		/// <summary>Shows the second pane: the session attaches the controllers to it, the view lays it out under the first one.</summary>
		private void ShowSplit()
		{
			if (this._session is null)
			{
				return;
			}

			this._session.ShowSecondary(this.SecondaryEditor);
			this.SecondaryEditor.ContextMenu = this.BuildContextMenu(this.SecondaryEditor);
			Double ratio = Math.Clamp(this._session.Context.ViewState.SplitRatio, 0.1, 0.9);
			this.PrimaryRow.Height = new GridLength(ratio, GridUnitType.Star);
			this.SecondaryRow.Height = new GridLength(1 - ratio, GridUnitType.Star);
			this.SplitSplitter.Visibility = Visibility.Visible;
			this.SecondaryEditor.Visibility = Visibility.Visible;
		}

		/// <summary>Closes the second pane for good (the user toggled the split off).</summary>
		private void HideSplit()
		{
			this._session?.HideSecondary();
			this.CollapseSplit(false);
		}

		/// <summary>Takes the second pane off the layout; the session is not told, so the split state of the document is kept.</summary>
		/// <param name="rememberRatio">Whether the share of the first pane is stored for the next view of the document.</param>
		private void CollapseSplit(Boolean rememberRatio)
		{
			if (rememberRatio && this._session is { IsSplit: true } session)
			{
				Double total = this.PrimaryEditor.ActualHeight + this.SecondaryEditor.ActualHeight;
				if (total > 0)
				{
					session.Context.ViewState.SplitRatio = this.PrimaryEditor.ActualHeight / total;
				}
			}

			this.SecondaryEditor.Visibility = Visibility.Collapsed;
			this.SplitSplitter.Visibility = Visibility.Collapsed;
			this.PrimaryRow.Height = new GridLength(1, GridUnitType.Star);
			this.SecondaryRow.Height = new GridLength(0);
			this.SecondaryEditor.ContextMenu = null;
		}
	}
}
