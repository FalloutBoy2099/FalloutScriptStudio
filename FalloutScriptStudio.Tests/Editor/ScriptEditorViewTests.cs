namespace FalloutScriptStudio.Tests.Editor
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Views.Editor;
	using Xunit;

	public sealed class ScriptEditorViewTests
	{
		private const String Script =
			"procedure start;\r\n"
			+ "procedure other;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "   other;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure other begin\r\n"
			+ "   display_msg(\"x\");\r\n"
			+ "end\r\n";

		/// <summary>Shows a view in an off-screen window so that it is loaded and laid out.</summary>
		private static Window Show(FrameworkElement view)
		{
			Window window = OffscreenWindow.Show(view, 800, 500);
			UiThread.Flush();
			return window;
		}

		[Fact]
		public void View_WhenLoaded_AttachesTheSurfaceToTheDocument_AndDetachesWhenUnloaded()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Assert.Null(document.Editor);

				Window window = Show(view);
				Assert.NotNull(document.Editor);
				Assert.Same(view.Surface, document.Editor);
				Assert.True(document.ViewState.HasBeenShown);

				window.Content = null;
				UiThread.Flush();
				Assert.Null(document.Editor);
				window.Close();
			});
		}

		[Fact]
		public void View_ServicesCanComeFromTheInheritedAttachedProperty()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView { DataContext = document };
				Window window = OffscreenWindow.Create(view, 400, 300);
				ScriptEditorView.SetServices(window, env.Services);

				OffscreenWindow.Show(window);
				UiThread.Flush();

				Assert.NotNull(view.Surface);
				window.Close();
			});
		}

		[Fact]
		public void View_WithoutServices_ThrowsAHelpfulException()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				ScriptEditorView view = new ScriptEditorView { DataContext = env.CreateDocument(Script) };
				Window window = OffscreenWindow.Create(view, 400, 300);

				Exception? captured = null;
				System.Windows.Threading.DispatcherUnhandledExceptionEventHandler handler = (_, e) =>
				{
					captured = e.Exception;
					e.Handled = true;
				};
				window.Dispatcher.UnhandledException += handler;

				OffscreenWindow.Show(window);
				UiThread.Flush();
				window.Dispatcher.UnhandledException -= handler;
				window.Close();

				InvalidOperationException exception = Assert.IsType<InvalidOperationException>(captured);
				Assert.Contains("ScriptEditorServices", exception.Message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void View_RecreatedForTheSameDocument_KeepsCaretFoldsAndUndo()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				Int32 caret;

				ScriptEditorView first = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(first);
				document.TextDocument.Insert(0, "// edit\r\n");
				first.Session!.Folding.ProcedureSections.Last().IsFolded = true;
				caret = document.TextDocument.GetLineByNumber(5).Offset + 2;
				first.Surface!.CaretOffset = caret;
				window.Content = null;
				UiThread.Flush();

				ScriptEditorView second = new ScriptEditorView(env.Services) { DataContext = document };
				window.Content = second;
				UiThread.Flush();

				Assert.Equal(caret, second.Surface!.CaretOffset);
				Assert.True(second.Session!.Folding.ProcedureSections.Last().IsFolded);
				Assert.True(second.Surface.CanUndo);
				window.Close();
			});
		}

		[Fact]
		public void View_RoutedCommands_RunTheEditorCommands()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("a;\r\nb;");
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);

				Assert.True(EditorCommands.Get(EditorCommand.CommentSelection).CanExecute(null, view));
				EditorCommands.Get(EditorCommand.CommentSelection).Execute(null, view);

				Assert.Equal("//a;\r\nb;", document.TextDocument.Text);
				window.Close();
			});
		}

		[Fact]
		public void View_SplitCommand_ShowsAndHidesTheSecondPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);

				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				ICSharpCode.AvalonEdit.TextEditor? secondary = (ICSharpCode.AvalonEdit.TextEditor?)view.FindName("SecondaryEditor");
				Assert.NotNull(secondary);
				Assert.Equal(Visibility.Visible, secondary.Visibility);
				Assert.Same(document.TextDocument, secondary.Document);

				view.Surface.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Assert.Equal(Visibility.Collapsed, secondary.Visibility);
				window.Close();
			});
		}

		[Fact]
		public void View_ContextMenu_NamesTheSymbolKindInTheRenameItem()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);
				view.Surface!.CaretOffset = document.TextDocument.GetLineByNumber(5).Offset + 5;

				ContextMenu menu = view.Editor.ContextMenu!;
				menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
				UiThread.Flush();
				MenuItem rename = menu.Items.OfType<MenuItem>().First(item => item.Header?.ToString()?.StartsWith("Rename", StringComparison.Ordinal) == true);

				Assert.Equal("Rename: Procedure", rename.Header);
				Assert.Equal(Visibility.Visible, rename.Visibility);
				window.Close();
			});
		}
	}
}