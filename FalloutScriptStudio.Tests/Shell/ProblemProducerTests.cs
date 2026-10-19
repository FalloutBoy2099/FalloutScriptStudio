namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Failures that used to reach only a message box, the status bar or the log are problems with a hint (and an action when there is an obvious one).</summary>
	public sealed class ProblemProducerTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void A_file_that_cannot_be_saved_is_a_problem_of_its_document_until_it_is_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");
				Boolean fail = true;
				CallbackDocumentFileWriter writer = new CallbackDocumentFileWriter((path, bytes) =>
				{
					if (fail)
					{
						throw new IOException("disk full");
					}

					File.WriteAllBytes(path, bytes);
					return Task.CompletedTask;
				});
				DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs, null, writer, harness.Problems);

				Boolean saved = await files.WriteAsync(document, document.FilePath!);

				Assert.False(saved);
				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.File, problem.Source);
				Assert.Equal("a.ssl could not be saved: disk full", problem.Message);
				Assert.Same(document, problem.Owner);
				Assert.Equal(document.FilePath, problem.File);
				String dialog = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains("disk full", dialog, StringComparison.Ordinal);
				Assert.Contains(problem.Hint!, dialog, StringComparison.Ordinal);

				fail = false;
				Assert.True(await files.WriteAsync(document, document.FilePath!));
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_file_that_is_not_utf8_is_noted_with_the_code_page_it_was_read_with_and_the_note_goes_with_the_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Combine("legacy.ssl");
				File.WriteAllBytes(path, new Byte[] { 0x2F, 0x2F, 0x20, 0xE9, 0xE8, 0x0D, 0x0A });

				DocumentViewModel? document = await harness.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = false });

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSeverity.Info, problem.Severity);
				Assert.StartsWith("legacy.ssl is not valid UTF-8 and was read as ", problem.Message, StringComparison.Ordinal);
				Assert.Equal(SettingsPage.Editor, problem.Action!.Page);
				Assert.Equal(0, harness.Output.ProblemErrorCount + harness.Output.ProblemWarningCount);
				Assert.Equal(1, harness.Output.ProblemInfoCount);

				await harness.Workspace.CloseAsync(document!);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void Plain_ascii_and_utf8_files_raise_no_encoding_note()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("ascii.ssl", Script);
				String utf8 = harness.Folder.Combine("utf8.ssl");
				File.WriteAllBytes(utf8, new System.Text.UTF8Encoding(false).GetBytes("// café\r\n"));
				await harness.Workspace.OpenAsync(utf8, new OpenDocumentOptions { AddToRecent = false });

				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_file_that_does_not_exist_is_a_problem_unless_it_came_from_the_recent_list_prompt()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String missing = harness.Folder.Combine("missing.ssl");

				await harness.Workspace.OpenAsync(missing, new OpenDocumentOptions { AddToRecent = false });

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal("The file " + missing + " does not exist.", problem.Message);
				Assert.Equal(missing, problem.File);

				harness.Problems.Clear();
				await harness.Workspace.OpenAsync(missing, new OpenDocumentOptions { AddToRecent = false, FromRecentList = true });
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_file_that_is_locked_by_another_program_is_an_open_failure()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String path = harness.Folder.Write("locked.ssl", Script);
				using FileStream hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

				DocumentViewModel? document = await harness.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = false });

				Assert.Null(document);
				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.StartsWith("locked.ssl could not be opened: ", problem.Message, StringComparison.Ordinal);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void An_include_that_cannot_be_found_is_a_problem_of_the_script_with_the_paths_page_as_action()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/a.ssl", "#include \"nothing.h\"\r\n");

				await harness.Workspace.OpenIncludeAsync(script, "nothing.h");

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.File, problem.Source);
				Assert.Contains("nothing.h", problem.Message, StringComparison.Ordinal);
				Assert.Equal(SettingsPage.Paths, problem.Action!.Page);
				Assert.Same(script, problem.Owner);
				Assert.Equal(script.FilePath, problem.File);
			});
		}

		[Fact]
		public void A_decompilation_that_fails_reports_the_missing_decompiler_and_the_failure()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String compiled = harness.Folder.Write("a.int", "binary");
				harness.Compiler.DecompiledText = null;
				harness.Compiler.ToolFailures = new[] { new ToolFailure(ToolFailureKind.NotFound, harness.Paths.GetResourcePath("int2ssl.exe")) };

				DocumentViewModel? document = await harness.Workspace.OpenAsync(compiled, new OpenDocumentOptions { AddToRecent = false });

				Assert.Null(document);
				Assert.Equal(new[] { "int2ssl.exe was not found: " + harness.Paths.GetResourcePath("int2ssl.exe"), "a.int could not be decompiled." }, harness.Problems.Problems.Select(problem => problem.Message));
				Assert.Equal(SettingsPage.Compiler, harness.Problems.Problems[1].Action!.Page);
			});
		}

		[Fact]
		public void Opening_a_compiled_script_is_an_operation_the_user_can_cancel()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String compiled = harness.Folder.Write("a.int", "binary");
				harness.Compiler.DecompiledText = "// text\r\n";
				harness.Compiler.Hang = true;

				Task<DocumentViewModel?> open = harness.Workspace.OpenAsync(compiled, new OpenDocumentOptions { AddToRecent = false });
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				Assert.True(harness.Operations.IsRunning);
				Assert.Equal("Decompiling a.int", harness.Main.OperationText);
				Assert.True(harness.Main.CancelOperationCommand.CanExecute(null));
				harness.Main.CancelOperationCommand.Execute(null);
				DocumentViewModel? document = await open;

				Assert.Null(document);
				Assert.Empty(harness.Workspace.Documents);
				Assert.Equal("Decompilation of a.int cancelled.", harness.Output.StatusMessage);
				Assert.False(harness.Operations.IsRunning);
				Assert.Empty(harness.Dialogs.Messages);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_failed_background_operation_is_a_problem_and_a_cancelled_one_is_not()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Workspace.GuardAsync(() => throw new InvalidOperationException("it broke"));
				await harness.Workspace.GuardAsync(() => throw new OperationCanceledException());

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal("An operation failed: it broke", problem.Message);
				Assert.Contains("An operation failed: it broke", harness.Output.StatusMessage, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_failure_reported_by_an_editor_or_a_tool_window_reaches_the_status_bar_and_the_problem_list()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();

				harness.Workspace.ReportFailure("The flowchart could not be saved.");

				Assert.Equal("The flowchart could not be saved.", harness.Output.StatusMessage);
				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal("An operation failed: The flowchart could not be saved.", problem.Message);
				Assert.Equal(1, harness.Output.ProblemErrorCount);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void A_script_without_a_message_file_is_a_warning_only_when_the_user_asked()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);

				await harness.Workspace.AssociateMessageFileAsync(document, false, false);
				Assert.Empty(harness.Problems.Problems);

				await harness.Workspace.AssociateMessageFileAsync(document, false, true);

				Problem problem = Assert.Single(harness.Problems.Problems, item => item.Source == ProblemSource.Message);
				Assert.Equal(ProblemSeverity.Warning, problem.Severity);
				Assert.Equal("No message file (.msg) was found for npc.ssl.", problem.Message);
				Assert.Equal(SettingsPage.Messages, problem.Action!.Page);

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				Assert.NotNull(await harness.Workspace.AssociateMessageFileAsync(document, false, true));
				Assert.DoesNotContain(harness.Problems.Problems, item => item.Source == ProblemSource.Message);
			});
		}

		[Fact]
		public void A_script_that_cannot_be_extracted_from_the_archive_is_a_file_problem_with_the_reason()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.OpenFileAnswer = harness.Folder.Write("master.dat", "x");

				DocumentViewModel? document = await harness.Workspace.OpenFromArchiveAsync("missing.int");

				Assert.Null(document);
				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal("missing.int could not be extracted from the archive: not found in the archive", problem.Message);
			});
		}

		[Fact]
		public void An_external_editor_that_cannot_start_is_a_tool_problem_with_the_paths_page_as_action()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				String editor = harness.Folder.Write("editor.exe", "x");
				harness.Settings.Update(settings => settings.Paths.ExternalEditorPath = editor);
				harness.Shell.StartProgramResult = false;

				await harness.Main.OpenInExternalEditorAsync(document);

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.Tool, problem.Source);
				Assert.Contains(editor, problem.Message, StringComparison.Ordinal);
				Assert.Equal(SettingsPage.Paths, problem.Action!.Page);
			});
		}

		[Fact]
		public void A_native_parser_that_is_missing_is_a_note_and_one_that_broke_is_a_warning_and_both_go_when_it_works()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Problems.Clear();
				ScriptedParser parser = new ScriptedParser(harness.Parser);
				ParseCoordinator coordinator = new ParseCoordinator(parser, harness.Settings, null, TimeSpan.Zero, harness.Problems);

				parser.Outcome = ParseOutcome.NativeUnavailable;
				parser.Message = "The native parser was not found";
				await coordinator.ParseAsync(document, true);
				Problem missing = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSeverity.Info, missing.Severity);
				Assert.Equal(SettingsPage.Parser, missing.Action!.Page);

				// parser.dll is where the settings expect it, but the parser does not work: that is worth a warning.
				File.WriteAllText(harness.Paths.GetResourcePath("parser.dll"), "not a library");
				parser.Outcome = ParseOutcome.NativeUnavailable;
				parser.Message = "disabled after 3 crashes";
				await coordinator.ParseAsync(document, true);
				Problem broken = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSeverity.Warning, broken.Severity);
				Assert.Equal("The native parser stopped working: disabled after 3 crashes", broken.Message);
				Assert.Equal(1, harness.Output.ProblemWarningCount);

				parser.Outcome = ParseOutcome.NativeSucceeded;
				await coordinator.ParseAsync(document, true);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_native_parser_that_the_user_turned_off_is_not_a_problem()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Settings.Update(settings => settings.Editor.EnableParser = false);
				ScriptedParser parser = new ScriptedParser(harness.Parser) { Outcome = ParseOutcome.NativeDisabled };
				ParseCoordinator coordinator = new ParseCoordinator(parser, harness.Settings, null, TimeSpan.Zero, harness.Problems);

				await coordinator.ParseAsync(document, true);

				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_parse_that_throws_is_a_problem_of_the_document_that_the_next_good_parse_removes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Problems.Clear();
				ScriptedParser parser = new ScriptedParser(harness.Parser) { Failure = new InvalidOperationException("parser bug") };
				ParseCoordinator coordinator = new ParseCoordinator(parser, harness.Settings, null, TimeSpan.Zero, harness.Problems);

				await coordinator.ParseAsync(document, true);

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.Parse, problem.Source);
				Assert.Equal("a.ssl could not be analysed: parser bug", problem.Message);
				Assert.Same(document, problem.Owner);

				parser.Failure = null;
				await coordinator.ParseAsync(document, true);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		/// <summary>A parser whose outcome, message and failure the test chooses; the program data come from the real internal parser.</summary>
		private sealed class ScriptedParser : IScriptParser
		{
			private readonly IScriptParser _inner;

			public ScriptedParser(IScriptParser inner)
			{
				this._inner = inner;
			}

			public ParseOutcome Outcome { get; set; } = ParseOutcome.NativeDisabled;

			public String? Message { get; set; }

			public Exception? Failure { get; set; }

			public MacroRegistry GlobalMacros => this._inner.GlobalMacros;

			public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken)
			{
				if (this.Failure is not null)
				{
					throw this.Failure;
				}

				ParseResult real = await this._inner.ParseAsync(request, cancellationToken);
				return real with { Outcome = this.Outcome, Message = this.Message };
			}

			public Task ReloadGlobalMacrosAsync(CancellationToken cancellationToken)
			{
				return this._inner.ReloadGlobalMacrosAsync(cancellationToken);
			}
		}
	}
}