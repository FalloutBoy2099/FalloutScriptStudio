namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class HelpPanelViewModelTests
	{
		private const String Script = "procedure start begin\r\n   wait(5);\r\n   set_sfall_global(\"abcdefgh\", 1);\r\nend\r\n";

		private static async Task<(DocumentViewModel Document, FakeEditorSurface Editor)> OpenAsync(ShellHarness harness)
		{
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
			return (document, harness.AttachEditor(document));
		}

		[Fact]
		public void Without_a_document_the_panel_is_empty()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.False(harness.Help.HasCurrent);
			Assert.True(harness.Help.IsEmpty);
			Assert.False(harness.Help.ShowResults);
		}

		[Fact]
		public void The_panel_shows_the_function_under_the_caret_now_when_asked()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				editor.WordAtCaret = "wait";

				harness.Help.Update();

				Assert.NotNull(harness.Help.Current);
				Assert.Equal("void wait(int time)", harness.Help.Current!.SignatureText);
				Assert.Equal(new[] { "Pauses the game for the specified time." }, harness.Help.Current.Paragraphs);
				Assert.Equal("Group", harness.Help.Current.Categories);
				Assert.False(harness.Help.IsEmpty);
			});
		}

		[Fact]
		public void The_panel_follows_the_caret_after_a_pause_and_forgets_a_word_without_help()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				editor.WordAtCaret = "wait";
				editor.GoTo(2);
				await Eventually.TrueAsync(() => harness.Help.Current?.Name == "wait");

				editor.WordAtCaret = "set_sfall_global";
				editor.GoTo(3);
				await Eventually.TrueAsync(() => harness.Help.Current?.Name == "set_sfall_global");
				Assert.True(harness.Help.Current!.Topic.IsSfall);

				editor.WordAtCaret = "nothing_here";
				editor.GoTo(1);
				await Eventually.TrueAsync(() => harness.Help.Current is null);
				Assert.True(harness.Help.IsEmpty);
			});
		}

		[Fact]
		public void Moving_the_caret_several_times_quickly_updates_the_panel_once()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				editor.WordAtCaret = "wait";
				Int32 changes = 0;
				((INotifyPropertyChanged)harness.Help).PropertyChanged += (_, e) => changes += e.PropertyName == nameof(harness.Help.Current) ? 1 : 0;

				for (Int32 i = 0; i < 6; i++)
				{
					editor.GoTo(2);
				}

				await Eventually.TrueAsync(() => harness.Help.Current is not null);
				await Task.Delay(150);
				Assert.Equal(1, changes);
			});
		}

		[Fact]
		public void A_selection_that_is_not_one_word_shows_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				editor.WordAtCaret = "wait(5);";

				harness.Help.Update();

				Assert.Null(harness.Help.Current);
			});
		}

		[Fact]
		public void The_filter_lists_the_functions_and_shows_the_first_one()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Help.FilterText = "sfall";

			Assert.True(harness.Help.ShowResults);
			Assert.Equal(new[] { "set_sfall_global" }, harness.Help.Results.Select(static t => t.Name).ToArray());
			Assert.Same(harness.Help.Results[0], harness.Help.SelectedResult);
			Assert.Equal("set_sfall_global", harness.Help.Current?.Name);
			Assert.False(harness.Help.HasNoResults);
		}

		[Fact]
		public void Choosing_another_result_shows_it()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Help.FilterText = "w";
			HelpTopic wait = harness.Help.Results.Single(static t => t.Name == "wait");

			harness.Help.SelectedResult = wait;

			Assert.Equal("wait", harness.Help.Current?.Name);
		}

		[Fact]
		public void While_a_filter_is_typed_the_caret_does_not_change_the_panel_and_clearing_it_follows_the_caret_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				harness.Help.FilterText = "sfall";
				editor.WordAtCaret = "wait";

				editor.GoTo(2);
				await Task.Delay(200);
				Assert.Equal("set_sfall_global", harness.Help.Current?.Name);

				harness.Help.FilterText = String.Empty;

				Assert.Empty(harness.Help.Results);
				Assert.Equal("wait", harness.Help.Current?.Name);
				Assert.False(harness.Help.ShowResults);
			});
		}

		[Fact]
		public void A_filter_without_a_match_says_so()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Help.FilterText = "qqqqzzzz";

			Assert.Empty(harness.Help.Results);
			Assert.True(harness.Help.HasNoResults);
			Assert.Null(harness.Help.Current);
			Assert.False(harness.Help.IsEmpty);
		}

		[Fact]
		public void Copying_the_signature_puts_it_on_the_clipboard_and_in_the_status()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Help.FilterText = "wait";

			harness.Help.Current!.CopySignatureCommand.Execute(null);

			Assert.Equal("void wait(int time)", harness.Shell.Clipboard);
		}

		[Fact]
		public void Insert_call_puts_the_code_of_the_functions_tree_at_the_caret()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("b.ssl", "   x := ;\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.CaretOffset = 3;
				harness.Help.FilterText = "wait";
				Assert.True(harness.Help.Current!.CanInsert);

				harness.Help.Current.InsertCallCommand.Execute(null);

				Assert.Equal("   wait({int time}); x := ;\r\n", document.TextDocument.Text);
				Assert.Equal("{int time}".Length, editor.SelectionLength);
			});
		}

		[Fact]
		public void A_function_that_is_not_in_the_tree_cannot_be_inserted()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Help.FilterText = "sfall";

			Assert.False(harness.Help.Current!.CanInsert);
		}

		[Fact]
		public void The_documentation_button_opens_the_page_in_the_shell()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Help.FilterText = "sfall";

			Assert.True(harness.Help.Current!.CanOpenDocumentation);
			harness.Help.Current.OpenDocumentationCommand.Execute(null);

			Assert.Equal(new[] { "https://sfall-team.github.io/sfall/global-variables/#set_sfall_global" }, harness.Shell.Opened);
		}

		[Fact]
		public void When_the_browser_cannot_be_started_the_user_is_told_the_address()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Shell.OpenWithShellResult = false;
				harness.Help.FilterText = "sfall";

				harness.Help.Current!.OpenDocumentationCommand.Execute(null);
				await Eventually.TrueAsync(() => harness.Dialogs.Messages.Count > 0);

				Assert.Contains("https://sfall-team.github.io/sfall/global-variables/#set_sfall_global", harness.Dialogs.Messages.Single(), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_labels_are_recomputed_when_the_language_changes()
		{
			LanguageCollection.CollectGarbage();
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Help.FilterText = "wait";
				FalloutScriptStudio.ViewModels.Help.HelpTopicViewModel before = harness.Help.Current!;
				try
				{
					Loc.SetLanguage("fr");

					Assert.NotSame(before, harness.Help.Current);
					Assert.Equal("wait", harness.Help.Current!.Name);
					Assert.Equal(Loc.Get("Str.Panels.HelpKind.Function"), harness.Help.Current.KindText);
				}
				finally
				{
					Loc.SetLanguage(Loc.DefaultLanguage);
				}

				await Task.CompletedTask;
			});
		}

		[Fact]
		public void The_menu_command_shows_the_tab()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Main.ShowRightBrowser = false;

			harness.Main.ShowHelpPanelCommand.Execute(null);

			Assert.True(harness.Main.ShowRightBrowser);
			Assert.Equal(MainViewModel.HelpTabIndex, harness.Main.RightTabIndex);
			Assert.Same(harness.Help, harness.Main.Help);
		}

		[Fact]
		public void A_disposed_panel_stops_following()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				(_, FakeEditorSurface editor) = await OpenAsync(harness);
				harness.Help.Dispose();
				editor.WordAtCaret = "wait";

				editor.GoTo(2);
				await Task.Delay(150);

				Assert.Null(harness.Help.Current);
			});
		}
	}
}