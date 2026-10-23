namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Support;
	using FalloutScriptStudio.ViewModels.Help;
	using FalloutScriptStudio.Views.Editor;
	using FalloutScriptStudio.Views.Help;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>The help of the editor (Ctrl+F1): the popup for the word at the caret, its button that opens the sfall documentation, the hint of the hover tips and the context menu item.</summary>
	public sealed class EditorHelpTests
	{
		private const String Script =
			"#define MY_MAX(a, b) (((a) > (b)) ? (a) : (b))\r\n" +
			"// Heals the whole party.\r\n" +
			"// Call it from the map script.\r\n" +
			"procedure heal_party begin\r\n" +
			"   variable r := random(1, 3);\r\n" +
			"   set_sfall_global(\"abcdefgh\", MY_MAX(1, 2));\r\n" +
			"   display_msg(\"random\"); // random\r\n" +
			"   unknown_thing(5);\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   call heal_party;\r\n" +
			"end\r\n";

		private static Int32 OffsetOf(DocumentViewModel document, String text, Int32 skip = 0)
		{
			Int32 index = -1;
			for (Int32 i = 0; i <= skip; i++)
			{
				index = document.TextDocument.Text.IndexOf(text, index + 1, StringComparison.Ordinal);
			}

			return index;
		}

		private static DocumentViewModel CreateDocument(EditorTestEnvironment env)
		{
			DocumentViewModel document = env.CreateDocument(Script);
			System.Collections.Generic.SortedDictionary<String, FalloutScriptStudio.Core.Parsing.ScriptMacro> macros = new FalloutScriptStudio.Core.Parsing.MacroCollector(new FalloutScriptStudio.Core.Parsing.IncludeResolver(null, null)).CollectFromText(Script, document.FilePath, false);
			document.ProgramInfo = document.ProgramInfo!.WithScriptMacros(macros);
			return document;
		}

		private static EditorSession Start(EditorTestEnvironment env, DocumentViewModel document, out Window window)
		{
			TextEditor editor = new();
			window = OffscreenWindow.Show(editor, 800, 500);
			UiThread.Flush();
			EditorSession session = new(editor, document, env.Services, () => { });
			session.Start();
			UiThread.Flush();
			return session;
		}

		private static HelpTopicViewModel ShownTopic(EditorTestEnvironment env)
		{
			Popup popup = env.Popups.Popups.Last();
			ScrollViewer scroll = Assert.IsType<ScrollViewer>(((Border)popup.Child).Child);
			HelpTopicView view = Assert.IsType<HelpTopicView>(scroll.Content);
			return Assert.IsType<HelpTopicViewModel>(view.DataContext);
		}

		[Fact]
		public void Show_help_on_a_function_opens_a_popup_with_its_signature_and_description_through_the_presenter()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "random") + 2;

				Assert.True(session.Commands.CanExecute(EditorCommand.ShowHelp));
				session.Commands.Execute(EditorCommand.ShowHelp);

				Popup popup = Assert.Single(env.Popups.Popups);
				Assert.False(popup.IsOpen);
				Assert.True(session.Tips.Help.IsOpen);
				HelpTopicViewModel model = ShownTopic(env);
				Assert.Equal("int random(int min, int max)", model.SignatureText);
				Assert.Equal(new[] { "Returns a random number in the range (min...max) inclusive." }, model.Paragraphs);
				Assert.True(model.CanOpenDocumentation);
				Assert.False(model.CanInsert);
				window.Close();
			});
		}

		[Fact]
		public void The_popup_closes_with_the_tips_and_when_a_key_is_pressed()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "random") + 2;
				session.Commands.Execute(EditorCommand.ShowHelp);
				Assert.True(session.Tips.Help.IsOpen);

				session.Tips.HideAll();

				Assert.False(session.Tips.Help.IsOpen);
				window.Close();
			});
		}

		[Fact]
		public void Help_on_a_procedure_shows_the_comment_block_above_it()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				env.Recorder.OpenTexts[document.FilePath!] = document.TextDocument.Text;
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "heal_party", 1) + 3;

				session.Commands.Execute(EditorCommand.ShowHelp);

				HelpTopicViewModel model = ShownTopic(env);
				Assert.Equal("procedure heal_party", model.SignatureText);
				Assert.Equal(new[] { "Heals the whole party.\nCall it from the map script." }, model.Paragraphs);
				Assert.False(model.CanOpenDocumentation);
				Assert.Equal("test.ssl:4", model.Source);
				window.Close();
			});
		}

		[Fact]
		public void Help_on_a_macro_shows_its_definition()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "MY_MAX", 1) + 2;

				session.Commands.Execute(EditorCommand.ShowHelp);

				HelpTopicViewModel model = ShownTopic(env);
				Assert.Equal("MY_MAX(a, b)", model.SignatureText);
				Assert.Equal("#define MY_MAX(a, b) (((a) > (b)) ? (a) : (b))", model.Definition);
				Assert.False(model.CanOpenDocumentation);
				window.Close();
			});
		}

		[Theory]
		[InlineData("unknown_thing", 3)]
		[InlineData("\"random\"", 3)]
		[InlineData("// random", 5)]
		[InlineData("1, 3", 0)]
		public void There_is_no_help_for_unknown_words_strings_comments_and_numbers(String where, Int32 delta)
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, where) + delta;

				Assert.False(session.Commands.CanExecute(EditorCommand.ShowHelp));
				session.Commands.Execute(EditorCommand.ShowHelp);

				Assert.Empty(env.Popups.Popups);
				Assert.False(session.Tips.Help.IsOpen);
				window.Close();
			});
		}

		[Fact]
		public void Without_a_help_service_the_command_is_not_available()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "random") + 2;

				Assert.False(session.Commands.CanExecute(EditorCommand.ShowHelp));
				window.Close();
			});
		}

		[Fact]
		public void The_documentation_button_opens_the_page_of_the_function_in_the_shell_and_closes_the_popup()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "set_sfall_global") + 3;
				session.Commands.Execute(EditorCommand.ShowHelp);

				ShownTopic(env).OpenDocumentationCommand.Execute(null);

				Assert.Equal(new[] { "https://sfall-team.github.io/sfall/global-variables/#set_sfall_global" }, env.HelpShell!.Opened);
				Assert.False(session.Tips.Help.IsOpen);
				window.Close();
			});
		}

		[Fact]
		public void A_function_without_a_page_opens_the_index_of_the_documentation()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "random") + 2;
				session.Commands.Execute(EditorCommand.ShowHelp);

				ShownTopic(env).OpenDocumentationCommand.Execute(null);

				Assert.Equal(new[] { "https://sfall-team.github.io/sfall/" }, env.HelpShell!.Opened);
				window.Close();
			});
		}

		[Fact]
		public void A_browser_that_cannot_be_started_is_reported_with_the_address()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				env.HelpShell!.OpenWithShellResult = false;
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = Start(env, document, out Window window);
				session.Context.TextArea.Caret.Offset = OffsetOf(document, "random") + 2;
				session.Commands.Execute(EditorCommand.ShowHelp);

				ShownTopic(env).OpenDocumentationCommand.Execute(null);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("ReportStatus(", StringComparison.Ordinal) && call.Contains("https://sfall-team.github.io/sfall/", StringComparison.Ordinal));
				window.Close();
			});
		}

		[Fact]
		public void The_hover_tip_of_a_function_says_which_key_shows_the_help()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = env.StartSession(document);

				TipInfo tip = Assert.Single(session.Tips.BuildTips(OffsetOf(document, "random") + 2));

				Assert.Equal("Ctrl+F1 for help", tip.Hint);
			});
		}

		[Fact]
		public void The_hint_is_only_shown_when_there_is_help()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment plain = new EditorTestEnvironment();
				DocumentViewModel document = CreateDocument(plain);
				using EditorSession session = plain.StartSession(document);
				Assert.All(session.Tips.BuildTips(OffsetOf(document, "MY_MAX", 1) + 2), tip => Assert.Equal(String.Empty, tip.Hint));

				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel other = CreateDocument(env);
				using EditorSession helped = env.StartSession(other);
				Assert.Empty(helped.Tips.BuildTips(OffsetOf(other, "unknown_thing") + 2));
				Assert.Equal("Ctrl+F1 for help", Assert.Single(helped.Tips.BuildTips(OffsetOf(other, "MY_MAX", 1) + 2)).Hint);
			});
		}

		[Fact]
		public void The_hover_tip_view_shows_the_hint_line_only_when_there_is_one()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				using EditorSession session = env.StartSession(document);

				Assert.True(session.Tips.ShowHoverTip(OffsetOf(document, "random") + 2, new Point(5, 5)));
				EditorTipView view = Assert.IsType<EditorTipView>(env.Popups.ToolTips.Last().Content);
				view.Measure(new Size(600, 400));
				view.Arrange(new Rect(0, 0, 600, 400));
				view.UpdateLayout();

				TextBlock hint = FindTextBlock(view, "Ctrl+F1 for help");
				Assert.Equal(Visibility.Visible, hint.Visibility);
				Assert.True(hint.FontSize >= 11);
			});
		}

		[Fact]
		public void The_command_has_the_gesture_ctrl_f1_and_a_localized_name()
		{
			System.Windows.Input.RoutedUICommand command = EditorCommands.Get(EditorCommand.ShowHelp);

			System.Windows.Input.KeyGesture gesture = Assert.IsType<System.Windows.Input.KeyGesture>(Assert.Single(command.InputGestures));
			Assert.Equal(System.Windows.Input.Key.F1, gesture.Key);
			Assert.Equal(System.Windows.Input.ModifierKeys.Control, gesture.Modifiers);
			Assert.Equal("Function Help", command.Text);
		}

		[Fact]
		public void The_context_menu_offers_the_help_only_on_a_word_that_has_help()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(withHelp: true);
				DocumentViewModel document = CreateDocument(env);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = OffscreenWindow.Show(view, 800, 500);
				UiThread.Flush();
				ContextMenu menu = view.Editor.ContextMenu!;
				MenuItem item = menu.Items.OfType<MenuItem>().Single(candidate => candidate.Command == EditorCommands.Get(EditorCommand.ShowHelp));

				view.Surface!.CaretOffset = OffsetOf(document, "random") + 2;
				menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
				UiThread.Flush();
				Assert.Equal(Visibility.Visible, item.Visibility);

				view.Surface.CaretOffset = OffsetOf(document, "unknown_thing") + 2;
				menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
				UiThread.Flush();
				Assert.Equal(Visibility.Collapsed, item.Visibility);
				window.Close();
			});
		}

		[Fact]
		public void The_signature_text_colors_each_part_with_a_theme_brush()
		{
			UiThread.Run(() =>
			{
				HelpSignatureText text = new()
				{
					Segments = FalloutScriptStudio.Core.Help.FunctionSignatureFormatter.Format(new FalloutScriptStudio.Core.Analysis.FunctionSignature("random", "int", [new FalloutScriptStudio.Core.Analysis.FunctionParameter("int", "min", false, false)], false)),
				};

				String[] texts = text.Inlines.OfType<System.Windows.Documents.Run>().Select(static r => r.Text).ToArray();
				Assert.Equal("int random(int min)", String.Concat(texts));
				Assert.Equal(8, texts.Length);
				Assert.Equal("AccentTextFillColorPrimaryBrush", HelpSignatureText.GetBrushKey(FalloutScriptStudio.Core.Help.SignatureSegmentKind.Name));
				Assert.NotEqual(HelpSignatureText.GetBrushKey(FalloutScriptStudio.Core.Help.SignatureSegmentKind.ParameterType), HelpSignatureText.GetBrushKey(FalloutScriptStudio.Core.Help.SignatureSegmentKind.ParameterName));

				text.Segments = null;
				Assert.Empty(text.Inlines);
			});
		}

		private static TextBlock FindTextBlock(DependencyObject root, String text)
		{
			for (Int32 i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
			{
				DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
				if (child is TextBlock block && block.Text == text)
				{
					return block;
				}

				TextBlock? found = TryFind(child, text);
				if (found is not null)
				{
					return found;
				}
			}

			throw new InvalidOperationException("No text block shows '" + text + "'.");
		}

		private static TextBlock? TryFind(DependencyObject root, String text)
		{
			try
			{
				return FindTextBlock(root, text);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}
	}
}