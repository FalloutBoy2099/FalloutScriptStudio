namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.SplitViewTestSupport;

	/// <summary>The split view hosted by the editor view: the split command and the state kept across views.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.SplitViewTests")]
	public sealed class SplitViewTests
	{
		[Fact]
		public void View_SplitCommand_TogglesTheSecondPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);

				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Assert.True(view.Session!.IsSplit);
				TextEditor second = view.Session.Secondary!.Context.Editor;
				Assert.Equal(Visibility.Visible, second.Visibility);
				Assert.NotNull(second.ContextMenu);
				Assert.NotNull(view.Editor.ContextMenu);

				view.Surface.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Assert.False(view.Session.IsSplit);
				Assert.Equal(Visibility.Collapsed, second.Visibility);
				Assert.False(document.ViewState.IsSplit);
				window.Close();
			});
		}

		[Fact]
		public void View_SplitStaysWhenAnotherTabIsSelectedAndTheViewIsCreatedAgain()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView first = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(first);
				first.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Int32 caret = document.TextDocument.GetLineByNumber(8).Offset + 2;
				first.Session!.Secondary!.Context.TextArea.Caret.Offset = caret;

				window.Content = null;
				UiThread.Flush();
				Assert.Null(document.Editor);

				ScriptEditorView second = new ScriptEditorView(env.Services) { DataContext = document };
				window.Content = second;
				UiThread.Flush();

				Assert.True(second.Session!.IsSplit);
				EditorPane secondary = second.Session.Secondary!;
				Assert.Equal(Visibility.Visible, secondary.Context.Editor.Visibility);
				Assert.Equal(caret, secondary.Context.TextArea.Caret.Offset);
				window.Close();
			});
		}

		[Fact]
		public void View_ClosedSplit_StaysClosedWhenTheViewIsCreatedAgain()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView first = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(first);
				first.Surface!.Execute(EditorCommand.SplitDocument);
				first.Surface.Execute(EditorCommand.SplitDocument);
				window.Content = null;
				UiThread.Flush();

				ScriptEditorView second = new ScriptEditorView(env.Services) { DataContext = document };
				window.Content = second;
				UiThread.Flush();

				Assert.False(second.Session!.IsSplit);
				window.Close();
			});
		}

		[Fact]
		public void View_ContextMenuOfTheSecondPane_UsesItsOwnClipboardTarget()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);
				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();

				EditorPane secondary = view.Session!.Secondary!;
				MenuItem copy = secondary.Context.Editor.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Command == ApplicationCommands.Copy);
				MenuItem primaryCopy = view.Editor.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Command == ApplicationCommands.Copy);

				Assert.Same(secondary.Context.TextArea, copy.CommandTarget);
				Assert.Same(view.Editor.TextArea, primaryCopy.CommandTarget);
				window.Close();
			});
		}

		[Fact]
		public void View_StoppingTheSessionWhileSplit_ReleasesTheSecondEditor()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);
				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				TextEditor second = view.Session!.Secondary!.Context.Editor;

				window.Content = null;
				UiThread.Flush();

				Assert.NotSame(document.TextDocument, second.Document);
				Assert.Equal(Visibility.Collapsed, second.Visibility);
				window.Close();
			});
		}
	}
}
