namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class MainViewModelEditTests
	{
		private const String Script =
			"procedure start;\r\n" +
			"procedure helper;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   call helper;\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure helper begin\r\n" +
			"end\r\n";

		[Fact]
		public void Without_a_document_the_state_flags_are_off_and_the_title_is_plain()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.False(harness.Main.HasDocument);
			Assert.False(harness.Main.IsScript);
			Assert.False(harness.Main.CanUndo);
			Assert.False(harness.Main.IsRightBrowserVisible);
			Assert.Equal("Fallout Script Studio", harness.Main.Title);
			Assert.Equal("Parser: No file", harness.Main.ParserStatusText);
			Assert.Equal("Ln 1", harness.Main.LineText);
		}

		[Fact]
		public void The_active_document_drives_the_title_flags_and_status()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.GoTo(4);

				Assert.True(harness.Main.HasDocument);
				Assert.True(harness.Main.IsScript);
				Assert.True(harness.Main.IsParsable);
				Assert.True(harness.Main.IsRightBrowserVisible);
				Assert.Equal("Fallout Script Studio - " + document.FilePath, harness.Main.Title);
				Assert.Equal("Ln 4", harness.Main.LineText);
				Assert.Equal("Col 1", harness.Main.ColumnText);
				Assert.Equal("100%", harness.Main.ZoomText);
			});
		}

		[Fact]
		public void The_right_browser_is_hidden_for_message_files_and_follows_the_view_toggle()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel message = await harness.OpenFileAsync("a.msg", "{100}{}{x}\r\n");
				Assert.True(harness.Main.IsMessage);
				Assert.False(harness.Main.IsRightBrowserVisible);

				DocumentViewModel script = await harness.OpenFileAsync("a.ssl", Script);
				Assert.True(harness.Main.IsRightBrowserVisible);
				harness.Main.ShowRightBrowser = false;
				Assert.False(harness.Main.IsRightBrowserVisible);
				Assert.NotNull(message);
				Assert.NotNull(script);
			});
		}

		[Fact]
		public void Undo_and_redo_availability_follows_the_text()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Assert.False(harness.Main.CanUndo);

				document.TextDocument.Insert(0, "// x\r\n");
				Assert.True(harness.Main.CanUndo);
				document.TextDocument.UndoStack.Undo();
				Assert.False(harness.Main.CanUndo);
				Assert.True(harness.Main.CanRedo);
			});
		}

		[Fact]
		public void Editor_commands_run_through_the_editor_surface()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);

				harness.Main.EditCommand.Execute(EditorCommand.CommentSelection);
				harness.Main.CopyCommand.Execute(null);
				harness.Main.PasteCommand.Execute(null);
				harness.Main.SelectAllCommand.Execute(null);

				Assert.Equal(new[] { "execute:CommentSelection", "copy", "paste" }, editor.Calls);
				Assert.Equal(document.TextDocument.TextLength, editor.SelectionLength);
			});
		}

		[Fact]
		public void Back_and_forward_use_the_editor_history_and_report_their_state()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.CanNavigateBack = true;
				editor.GoTo(3);

				Assert.True(harness.Main.CanGoBack);
				Assert.False(harness.Main.CanGoForward);
				harness.Main.GoBackCommand.Execute(null);
				harness.Main.GoForwardCommand.Execute(null);

				Assert.Equal(new[] { "back", "forward" }, editor.Calls);
			});
		}

		[Fact]
		public void Go_to_line_asks_the_tool_window_and_moves_the_caret()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.ToolWindows.GoToLineAnswer = 5;

				await harness.Main.GoToLineAsync();

				Assert.Equal((5, 1, -1), editor.LastGoTo);
				Assert.Contains("goto:1/" + editor.LineCount, harness.ToolWindows.Calls);
			});
		}

		[Fact]
		public void Cancelling_go_to_line_does_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.ToolWindows.GoToLineAnswer = null;

				await harness.Main.GoToLineAsync();

				Assert.Null(editor.LastGoTo);
			});
		}

		[Fact]
		public void The_go_to_procedure_popup_lists_and_filters_procedures_and_navigates()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);

				harness.Main.GoToProcedureCommand.Execute(null);
				Assert.True(harness.Main.IsGoToProcedureOpen);
				Assert.Equal(new[] { "helper", "start" }, harness.Main.GoToProcedureItems.Select(p => p.Name));

				harness.Main.GoToProcedureFilter = "ST";
				Assert.Equal("start", Assert.Single(harness.Main.GoToProcedureItems).Name);

				await harness.Main.GoToProcedureItemAsync(harness.Main.GoToProcedureItems[0]);
				Assert.False(harness.Main.IsGoToProcedureOpen);
				Assert.Equal(4, editor.LastGoTo!.Value.Line);
			});
		}

		[Fact]
		public void Quick_find_takes_the_word_and_finds_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.WordAtCaret = "helper";

				harness.Main.QuickFindCommand.Execute(null);

				Assert.True(harness.Search.IsVisible);
				Assert.Equal("helper", harness.Search.SearchText);
				Assert.Equal("helper", editor.SelectedText);
			});
		}

		[Fact]
		public void Advanced_search_gets_the_word_at_the_caret()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.AttachEditor(document).WordAtCaret = "helper";

				await harness.Main.AdvancedSearchAsync();

				Assert.Equal("search:helper", Assert.Single(harness.ScriptTools.Calls));
			});
		}

		[Fact]
		public void Zoom_commands_change_the_editor_zoom_within_limits()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);

				harness.Main.ZoomInCommand.Execute(null);
				Assert.Equal(110, editor.ZoomPercent);
				Assert.Equal("110%", harness.Main.ZoomText);
				harness.Main.ZoomOutCommand.Execute(null);
				harness.Main.ZoomOutCommand.Execute(null);
				Assert.Equal(90, editor.ZoomPercent);
				for (Int32 i = 0; i < 20; i++)
				{
					harness.Main.ZoomOutCommand.Execute(null);
				}

				Assert.Equal(50, editor.ZoomPercent);
				harness.Main.ResetZoomCommand.Execute(null);
				Assert.Equal(100, editor.ZoomPercent);
			});
		}

		[Fact]
		public void Zoom_changes_made_in_the_editor_update_the_status_text()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				String? changed = null;
				harness.Main.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(harness.Main.ZoomText))
					{
						changed = harness.Main.ZoomText;
					}
				};

				editor.ZoomPercent = 150;
				editor.RaiseZoomChanged();

				Assert.Equal("150%", changed);
			});
		}

		[Fact]
		public void The_message_structure_check_lists_the_problems_of_a_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel message = await harness.OpenFileAsync("a.msg", "{100}{}{ok}\r\n{101}{}{broken\r\n");

				await harness.Main.CheckMessageStructureAsync();

				Assert.NotEmpty(message.ParserErrors);
				Assert.All(message.ParserErrors, e => Assert.Equal(message.FilePath, e.FileName));
				Assert.NotEmpty(harness.Output.Errors);
			});
		}

		[Fact]
		public void A_clean_message_file_reports_that_no_errors_were_found()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.msg", "{100}{}{ok}\r\n");

				await harness.Main.CheckMessageStructureAsync();

				Assert.Contains("No errors", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Parser_status_changes_are_forwarded()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Boolean raised = false;
				((INotifyPropertyChanged)harness.Main).PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(harness.Main.ParserStatusText);

				document.ParserStatus = new ParserStatus(ParserStatusKind.Done);

				Assert.True(raised);
				Assert.Equal("Parser: Done", harness.Main.ParserStatusText);
			});
		}
	}
}
