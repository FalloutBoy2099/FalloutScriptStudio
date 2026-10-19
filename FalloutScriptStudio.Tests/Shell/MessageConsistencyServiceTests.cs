namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class MessageConsistencyServiceTests
	{
		/// <summary>The script: line 1-2 define the script number, then a dialogue that uses the messages 100, 101 and 110.</summary>
		private static readonly String _script = String.Join(
			"\r\n",
			"#define NAME SCRIPT_NPC",                // 1
			"#define SCRIPT_NPC 123",                 // 2
			"procedure talk_p_proc begin",            // 3
			"   Reply(100);",                         // 4
			"   NOption(101, Node001, 4);",           // 5
			"   NMessage(110);",                      // 6
			"end",                                    // 7
			"procedure Node001 begin",                // 8
			"end",
			String.Empty);

		private static class MessageKeys
		{
			public const String Missing = "Str.Shell.Problem.MsgMissing";
			public const String NoUsage = "Str.Shell.Problem.MsgNoUsage";
		}

		private static IReadOnlyList<Problem> OfCheck(ShellHarness harness)
		{
			return harness.Problems.Problems.Where(problem => problem.Origin == ProblemOrigins.MessageConsistency).ToList();
		}

		private static async Task<DocumentViewModel> OpenWithMessagesAsync(ShellHarness harness, String messages)
		{
			DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
			harness.Folder.Write("scripts/npc.msg", messages);
			Assert.NotNull(await harness.Workspace.AssociateMessageFileAsync(script, false, false));
			return script;
		}

		[Fact]
		public void The_check_lists_every_kind_of_difference_at_the_place_to_fix_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n{101}{}{}\r\n{777}{}{Unused.}\r\n{100}{}{Again.}\r\n");

				Int32 count = await harness.Workspace.CheckMessagesAsync(script);

				IReadOnlyList<Problem> problems = OfCheck(harness);
				Assert.Equal(4, count);
				Assert.Equal(4, problems.Count);
				Assert.All(problems, problem =>
				{
					Assert.Equal(ProblemSource.Message, problem.Source);
					Assert.Same(script, problem.Owner);
				});

				Problem missing = Assert.Single(problems, p => p.Severity == ProblemSeverity.Error && p.File == script.FilePath);
				Assert.Equal(6, missing.Line);
				Assert.Equal("Message 110 is used by the script but is not in the message file npc.msg.", missing.Message);
				Assert.Equal(ProblemActionKind.AddMissingMessages, missing.Action!.Kind);
				Assert.Equal(new[] { 110 }, missing.Action.Numbers);
				Assert.Equal(script.FilePath, missing.Action.FilePath);

				String messageFile = harness.Folder.Combine("scripts", "npc.msg");
				Problem duplicate = Assert.Single(problems, p => p.Severity == ProblemSeverity.Error && p.File == messageFile);
				Assert.Equal(4, duplicate.Line);
				Problem empty = Assert.Single(problems, p => p.Severity == ProblemSeverity.Warning);
				Assert.Equal((messageFile, 2), (empty.File, empty.Line));
				Problem unused = Assert.Single(problems, p => p.Severity == ProblemSeverity.Info);
				Assert.Equal((messageFile, 3), (unused.File, unused.Line));
			});
		}

		[Fact]
		public void A_new_check_replaces_the_previous_results_and_a_clean_pair_leaves_none()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");

				await harness.Workspace.CheckMessagesAsync(script);
				await harness.Workspace.CheckMessagesAsync(script);
				Assert.Equal(2, OfCheck(harness).Count);

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n{101}{}{More.}\r\n{110}{}{A message.}\r\n");
				Int32 count = await harness.Workspace.CheckMessagesAsync(script);

				Assert.Equal(0, count);
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void The_problems_of_other_producers_and_other_scripts_are_left_alone()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				Object otherScript = new Object();
				harness.Problems.Report(ProblemFactory.OperationFailed("something else"));
				harness.Problems.Report(ProblemFactory.MessageFileMissing(@"C:\other\b.ssl", otherScript));
				harness.Problems.Report(ProblemFactory.ScriptMessage(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 5, ScriptMessageUsage.OwnFile, true, 3, 1, 1), @"C:\other\b.ssl", @"C:\other\b.msg", new[] { 5 }, 1000, otherScript));

				await harness.Workspace.CheckMessagesAsync(script);
				await harness.Workspace.CheckMessagesAsync(script);

				Assert.Equal(2, OfCheck(harness).Count(problem => ReferenceEquals(problem.Owner, script)));
				Assert.Contains(harness.Problems.Problems, problem => problem.MessageKey == "Str.Shell.Problem.OperationFailed");
				Assert.Contains(harness.Problems.Problems, problem => problem.MessageKey == "Str.Shell.Problem.MessageFileMissing");
				Assert.Contains(OfCheck(harness), problem => ReferenceEquals(problem.Owner, otherScript));
			});
		}

		[Fact]
		public void The_findings_of_the_check_survive_the_refresh_of_the_cached_texts()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				await harness.Workspace.CheckMessagesAsync(script);

				await harness.Workspace.RefreshMessageTextsAsync(script);
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);

				Assert.Equal(2, OfCheck(harness).Count);
			});
		}

		[Fact]
		public void Without_a_message_file_a_single_problem_offers_to_create_or_associate_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);

				Int32 count = await harness.Workspace.CheckMessagesAsync(script);

				Assert.Equal(1, count);
				Problem problem = Assert.Single(OfCheck(harness));
				Assert.Equal(ProblemSeverity.Warning, problem.Severity);
				Assert.Equal("npc.ssl uses 3 message(s) but has no message file.", problem.Message);
				Assert.Equal(ProblemActionKind.CreateMessageFile, problem.Action!.Kind);
				Assert.Equal("Create or associate the message file", problem.Action.Label);
				Assert.Equal(script.FilePath, problem.File);
			});
		}

		[Fact]
		public void A_script_that_uses_no_message_has_nothing_to_report_without_a_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/quiet.ssl", "procedure start begin\r\nend\r\n");

				Assert.Equal(0, await harness.Workspace.CheckMessagesAsync(script));
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void The_create_link_asks_to_create_the_file_and_checks_the_script_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				await harness.Workspace.CheckMessagesAsync(script);
				harness.Dialogs.ConfirmAnswer = true;

				await harness.Output.RunProblemActionAsync(harness.Output.Errors.Single(item => item.Action?.Kind == ProblemActionKind.CreateMessageFile));

				String messageFile = harness.Folder.Combine("scripts", "npc.msg");
				Assert.True(File.Exists(messageFile));
				Assert.Equal(messageFile, script.MessageFilePath);
				Assert.DoesNotContain(OfCheck(harness), p => p.Action?.Kind == ProblemActionKind.CreateMessageFile);
				Assert.Contains(OfCheck(harness), p => p.MessageKey == "Str.Shell.Problem.MsgMissing" && (Int32)p.MessageArguments[0]! == 101);
			});
		}

		[Fact]
		public void The_text_of_an_open_message_file_counts_even_when_it_is_not_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				DocumentViewModel tab = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				tab.TextDocument.Insert(tab.TextDocument.TextLength, "{101}{}{More.}\r\n{110}{}{Message.}\r\n");

				Int32 count = await harness.Workspace.CheckMessagesAsync(script);

				Assert.Equal(0, count);
			});
		}

		[Fact]
		public void The_fix_of_a_missing_message_appends_the_placeholder_lines_to_the_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "# NPC\r\n{100}{}{Hello.}\r\n\r\n");
				await harness.Workspace.CheckMessagesAsync(script);

				await harness.Output.RunProblemActionAsync(harness.Output.Errors.First(item => item.Action?.Kind == ProblemActionKind.AddMissingMessages));

				String messageFile = harness.Folder.Combine("scripts", "npc.msg");
				Assert.Equal("# NPC\r\n{100}{}{Hello.}\r\n{101}{}{}\r\n{110}{}{}\r\n\r\n", File.ReadAllText(messageFile));
				Assert.DoesNotContain(OfCheck(harness), p => p.MessageKey == "Str.Shell.Problem.MsgMissing");
				Assert.Equal(new[] { 101, 110 }, OfCheck(harness).Where(p => p.MessageKey == "Str.Shell.Problem.MsgEmpty").Select(p => (Int32)p.MessageArguments[0]!).Order());
				Assert.Equal("Added 2 message(s) to npc.msg.", harness.Output.StatusMessage);
				Assert.NotNull(harness.Workspace.GetMessageText(script, 101));
			});
		}

		[Fact]
		public void The_fix_reloads_the_text_tab_of_the_message_file_when_it_has_no_unsaved_changes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				DocumentViewModel tab = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				await harness.Workspace.CheckMessagesAsync(script);

				await harness.Output.RunProblemActionAsync(harness.Output.Errors.First(item => item.Action?.Kind == ProblemActionKind.AddMissingMessages));

				Assert.Equal("{100}{}{Hello.}\r\n{101}{}{}\r\n{110}{}{}\r\n", tab.TextDocument.Text);
				Assert.False(tab.IsModified);
			});
		}

		[Fact]
		public void The_fix_edits_the_tab_and_leaves_the_file_alone_when_the_tab_has_unsaved_changes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				DocumentViewModel tab = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				tab.TextDocument.Insert(tab.TextDocument.TextLength, "{500}{}{Typed, not saved.}\r\n");
				await harness.Workspace.CheckMessagesAsync(script);

				await harness.Output.RunProblemActionAsync(harness.Output.Errors.First(item => item.Action?.Kind == ProblemActionKind.AddMissingMessages));

				Assert.Equal("{100}{}{Hello.}\r\n", File.ReadAllText(harness.Folder.Combine("scripts", "npc.msg")));
				Assert.Equal("{100}{}{Hello.}\r\n{500}{}{Typed, not saved.}\r\n{101}{}{}\r\n{110}{}{}\r\n", tab.TextDocument.Text);
				Assert.True(tab.IsModified);
				Assert.Equal("Added 2 message(s) to the tab npc.msg; save the tab to keep them.", harness.Output.StatusMessage);
				Assert.DoesNotContain(OfCheck(harness), p => p.MessageKey == "Str.Shell.Problem.MsgMissing");
			});
		}

		[Fact]
		public void The_fix_never_touches_a_message_file_that_cannot_be_written()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				String messageFile = harness.Folder.Combine("scripts", "npc.msg");
				await harness.Workspace.CheckMessagesAsync(script);

				using (new FileStream(messageFile, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					await harness.Workspace.AddMissingMessagesAsync(script.FilePath!, new[] { 101, 110 });
				}

				Assert.Equal("{100}{}{Hello.}\r\n", File.ReadAllText(messageFile));
				Assert.Contains(harness.Problems.Problems, p => p.MessageKey == "Str.Shell.Problem.SaveFailed");
			});
		}

		[Fact]
		public void Saving_the_script_checks_it_after_the_quiet_period_when_it_has_a_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");

				Assert.True(await harness.Workspace.SaveAsync(script));
				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 2));

				script.TextDocument.Insert(script.TextDocument.TextLength, "// edited\r\n");
				Assert.True(await harness.Workspace.SaveAsync(script));
				await Task.Delay(300);
				Assert.Equal(2, OfCheck(harness).Count);
			});
		}

		[Fact]
		public void The_setting_turns_the_check_on_save_off()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				harness.Settings.Update(settings => settings.Messages.CheckMessagesOnSave = false);

				await harness.Workspace.SaveAsync(script);
				await Task.Delay(300);

				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void A_script_without_a_message_file_association_is_not_checked_on_save()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				await harness.Workspace.SaveAsync(script);
				await Task.Delay(300);

				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void Saving_the_message_file_checks_the_scripts_that_use_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				DocumentViewModel tab = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				tab.TextDocument.Insert(tab.TextDocument.TextLength, "{101}{}{More.}\r\n");
				Assert.True(await harness.Workspace.SaveAsync(tab));

				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 1));
				Problem remaining = Assert.Single(OfCheck(harness));
				Assert.Equal(110, (Int32)remaining.MessageArguments[0]!);
				Assert.Same(script, remaining.Owner);
			});
		}

		[Fact]
		public void The_message_editor_that_saves_the_file_of_a_script_starts_the_check_too()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n{101}{}{More.}\r\n");

				harness.Workspace.UpdateMessageTexts(script, MessageDocument.Parse(new[] { "{100}{}{Hello.}", "{101}{}{More.}" }));

				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 1));
			});
		}

		[Fact]
		public void Closing_the_script_drops_the_pending_check_and_its_problems()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				await harness.Workspace.CheckMessagesAsync(script);
				Assert.NotEmpty(OfCheck(harness));

				await harness.Workspace.SaveAsync(script);
				Assert.True(await harness.Workspace.CloseAsync(script));
				await Task.Delay(300);

				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void A_newer_save_cancels_the_check_that_waits_for_the_quiet_period()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n");
				MessageChecks checks = new MessageChecks(harness);

				for (Int32 i = 0; i < 5; i++)
				{
					await harness.Workspace.SaveAsync(script);
				}

				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 2));
				await Task.Delay(300);
				Assert.True(checks.Changes <= 2, "The problems were replaced " + checks.Changes + " times.");
			});
		}

		[Fact]
		public void The_check_command_tells_when_nothing_differs_and_shows_the_list_otherwise()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await OpenWithMessagesAsync(harness, "{100}{}{Hello.}\r\n{101}{}{More.}\r\n{110}{}{A message.}\r\n");

				await harness.Main.CheckMessagesAsync();
				Assert.Equal("Check: No errors were found.", Assert.Single(harness.Dialogs.Messages));

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				harness.Output.SelectedTab = (Int32)OutputTab.Parser;
				await harness.Main.CheckMessagesAsync();

				Assert.Single(harness.Dialogs.Messages);
				Assert.True(harness.Output.IsErrorsTabSelected);
				Assert.Equal(2, harness.Output.Errors.Count);
				Assert.Same(script, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void The_check_command_needs_a_saved_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Workspace.CreateNew(_script);

				await harness.Main.CheckMessagesAsync();

				Assert.Contains("Save the script first", Assert.Single(harness.Dialogs.Messages));
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void The_check_command_ignores_documents_that_are_not_scripts()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				await harness.Main.CheckMessagesAsync();

				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void The_dialogue_rules_in_effect_decide_which_calls_show_a_message()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String text = "#define NAME SCRIPT_NPC\r\n#define SCRIPT_NPC 123\r\nprocedure talk_p_proc begin\r\n   Say(120);\r\nend\r\n";
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", text);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				FixedDialogueRules rules = new FixedDialogueRules();
				using MessageConsistencyService service = new MessageConsistencyService(harness.Settings, new MessageFileService(NullLogger<MessageFileService>.Instance), rules, harness.Problems, TimeSpan.FromMilliseconds(10));
				service.Attach(harness.Workspace);

				Assert.Equal(1, await service.CheckAsync(script));
				Assert.Equal(MessageKeys.NoUsage, Assert.Single(OfCheck(harness)).MessageKey);

				rules.Current = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", messageArg: 1, totalArgs: 1) });
				Assert.Equal(2, await service.CheckAsync(script));
				Problem missing = Assert.Single(OfCheck(harness), problem => problem.Severity == ProblemSeverity.Error);
				Assert.Equal(MessageKeys.Missing, missing.MessageKey);
				Assert.Equal(4, missing.Line);
			});
		}

		/// <summary>Counts how many times the list of problems of the check was replaced.</summary>
		private sealed class MessageChecks
		{
			private readonly ShellHarness _harness;
			private String _last = String.Empty;

			public MessageChecks(ShellHarness harness)
			{
				this._harness = harness;
				harness.Problems.Changed += (_, _) => this.OnChanged();
			}

			public Int32 Changes { get; private set; }

			private void OnChanged()
			{
				String now = String.Join("|", OfCheck(this._harness).Select(problem => problem.Identity));
				if (now != this._last)
				{
					this._last = now;
					this.Changes++;
				}
			}
		}
	}
}