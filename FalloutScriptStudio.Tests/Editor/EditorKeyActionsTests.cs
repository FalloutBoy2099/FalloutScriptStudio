namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Folding;
	using Xunit;

	/// <summary>The keyboard actions of the legacy editor that AvalonEdit does not have, and the editor appearance details.</summary>
	public sealed class EditorKeyActionsTests
	{
		[Fact]
		public void GoToMatchingBracket_JumpsBehindTheClosingBracketAndBack()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo(1 + (2));");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 8;

				Assert.True(session.Brackets.GoToMatchingBracket());
				Assert.Equal(17, session.Context.TextArea.Caret.Offset);

				Assert.True(session.Brackets.GoToMatchingBracket());
				Assert.Equal(9, session.Context.TextArea.Caret.Offset);

				Assert.True(session.Brackets.GoToMatchingBracket());
				Assert.Equal(17, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void GoToMatchingBracket_NestedPairAndCommand()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo(1 + (2));");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 13;

				Assert.True(session.Commands.CanExecute(EditorCommand.GoToMatchingBracket));
				session.Commands.Execute(EditorCommand.GoToMatchingBracket);

				Assert.Equal(16, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void GoToMatchingBracket_WithoutBracket_DoesNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo; // (a)");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 3;

				Assert.False(session.Brackets.GoToMatchingBracket());
				Assert.Equal(3, session.Context.TextArea.Caret.Offset);

				session.Context.TextArea.Caret.Offset = 13;
				Assert.False(session.Brackets.GoToMatchingBracket());
			});
		}

		[Fact]
		public void GoToMatchingBracket_InAMessageFile_IsNotAvailable()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("{100}{}{Hello (x)}\r\n", "C:\\text\\test.msg");
				using EditorSession session = env.StartSession(document);

				Assert.False(session.Commands.CanExecute(EditorCommand.GoToMatchingBracket));
			});
		}

		[Fact]
		public void DeleteToLineEnd_RemovesTheRestOfTheLineAndKeepsTheLineBreak()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc def\r\nxyz");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 4;

				session.Commands.Execute(EditorCommand.DeleteToLineEnd);

				Assert.Equal("abc \r\nxyz", document.TextDocument.Text);
				Assert.Equal(4, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void DeleteToLineEnd_AtTheEndOfTheLine_DoesNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc\r\nxyz");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 3;

				session.Commands.Execute(EditorCommand.DeleteToLineEnd);

				Assert.Equal("abc\r\nxyz", document.TextDocument.Text);
			});
		}

		[Fact]
		public void DeleteToLineEnd_IsOneUndoStep()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc def");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 3;

				session.Commands.Execute(EditorCommand.DeleteToLineEnd);
				document.TextDocument.UndoStack.Undo();

				Assert.Equal("abc def", document.TextDocument.Text);
			});
		}

		[Fact]
		public void AltBackspace_IsBoundToUndo()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc");
				using EditorSession session = env.StartSession(document);

				KeyBinding? binding = session.Context.TextArea.InputBindings.OfType<KeyBinding>()
					.FirstOrDefault(b => b.Key == Key.Back && b.Modifiers == ModifierKeys.Alt);
				Assert.NotNull(binding);
				Assert.Same(ApplicationCommands.Undo, binding.Command);

				document.TextDocument.Insert(3, "d");
				Assert.True(ApplicationCommands.Undo.CanExecute(null, session.Context.TextArea));
				ApplicationCommands.Undo.Execute(null, session.Context.TextArea);
				Assert.Equal("abc", document.TextDocument.Text);
			});
		}

		[Fact]
		public void AltBackspace_BindingIsRemovedWhenTheSessionStops()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc");
				EditorSession session = env.StartSession(document);
				InputBindingCollection bindings = session.Context.TextArea.InputBindings;

				session.Dispose();

				Assert.DoesNotContain(bindings.OfType<KeyBinding>(), b => b.Key == Key.Back && b.Modifiers == ModifierKeys.Alt);
			});
		}

		[Theory]
		[InlineData(EditorCommand.CapitalizeWord, Key.W, ModifierKeys.Control | ModifierKeys.Shift)]
		[InlineData(EditorCommand.UpperCase, Key.A, ModifierKeys.Control | ModifierKeys.Shift)]
		[InlineData(EditorCommand.LowerCase, Key.Z, ModifierKeys.Control | ModifierKeys.Shift)]
		[InlineData(EditorCommand.GoToMatchingBracket, Key.B, ModifierKeys.Control)]
		[InlineData(EditorCommand.DeleteToLineEnd, Key.D, ModifierKeys.Control | ModifierKeys.Shift)]
		[InlineData(EditorCommand.ShowCompletion, Key.Space, ModifierKeys.Control)]
		[InlineData(EditorCommand.ShowCompletion, Key.Space, ModifierKeys.Alt)]
		[InlineData(EditorCommand.SplitDocument, Key.S, ModifierKeys.Alt)]
		[InlineData(EditorCommand.ShowHelp, Key.F1, ModifierKeys.Control)]
		public void Commands_HaveTheLegacyGestures(EditorCommand command, Key key, ModifierKeys modifiers)
		{
			UiThread.Run(() =>
			{
				RoutedUICommand routed = EditorCommands.Get(command);

				Assert.Contains(routed.InputGestures.OfType<KeyGesture>(), gesture => gesture.Key == key && gesture.Modifiers == modifiers);
			});
		}

		[Fact]
		public void Scripts_AllowTheCaretBehindTheEndOfALine_MessagesDoNot()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession script = env.StartSession(env.CreateDocument("abc\r\n"));
				using EditorSession message = env.StartSession(env.CreateDocument("{100}{}{Hello}\r\n", "C:\\text\\test.msg"));

				Assert.True(script.Context.Editor.Options.EnableVirtualSpace);
				Assert.False(message.Context.Editor.Options.EnableVirtualSpace);
			});
		}

		[Fact]
		public void VirtualSpace_CaretBehindTheLineEnd_KeepsTheOffsetAtTheLineEnd()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc\r\nxyz\r\n");
				using EditorSession session = env.StartSession(document);

				session.Context.TextArea.Caret.Offset = 3;
				EditingCommands.MoveRightByCharacter.Execute(null, session.Context.TextArea);
				EditingCommands.MoveRightByCharacter.Execute(null, session.Context.TextArea);

				Assert.Equal(3, session.Context.TextArea.Caret.Offset);
				Assert.Equal(5, session.Context.TextArea.Caret.Position.VisualColumn);
				Assert.Equal(6, new EditorSurface(session).CaretColumn);
			});
		}

		[Fact]
		public void TabMarkers_AreDrawnByTheirOwnRendererInTheTabColor()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.ShowTabCharacters = true;
				env.SettingsService.Update(_ => { });
				using EditorSession session = env.StartSession(env.CreateDocument("\tabc\r\n"));

				TabMarkerRenderer renderer = Assert.Single(session.Context.TextView.BackgroundRenderers.OfType<TabMarkerRenderer>());
				Assert.True(renderer.IsEnabled);
				Assert.False(session.Context.Editor.Options.ShowTabs);
				Assert.True(session.Context.Editor.Options.ShowSpaces);
				Assert.Equal(((SolidColorBrush)env.Services.Palette.TabMarker).Color, ((SolidColorBrush)renderer.Brush).Color);
				Assert.NotEqual(((SolidColorBrush)env.Services.Palette.TabMarker).Color, ((SolidColorBrush)env.Services.Palette.WhitespaceMarker).Color);
			});
		}

		[Fact]
		public void TabMarkers_AreOffWhenTheOptionIsOff()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(env.CreateDocument("\tabc\r\n"));

				Assert.False(Assert.Single(session.Context.TextView.BackgroundRenderers.OfType<TabMarkerRenderer>()).IsEnabled);
			});
		}

		[Fact]
		public void TabMarkerRenderer_FindsTheTabsOfAText()
		{
			Assert.Equal(new[] { 10, 13 }, TabMarkerRenderer.FindTabs("\tab\tc", 10));
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void SelectedFoldLine_IsGoldInTheDarkThemeAndTheTextColorInTheLightOne(Boolean dark)
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(dark);
				using EditorSession session = env.StartSession(env.CreateDocument("procedure start begin\r\nend\r\n"));

				SolidColorBrush brush = (SolidColorBrush)session.Context.Editor.GetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty);

				if (dark)
				{
					Assert.Equal(Colors.Gold, brush.Color);
				}
				else
				{
					Assert.Equal(((SolidColorBrush)env.Services.Palette.Foreground).Color, brush.Color);
				}
			});
		}
	}
}