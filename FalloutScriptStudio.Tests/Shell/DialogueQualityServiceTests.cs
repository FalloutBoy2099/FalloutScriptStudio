namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class DialogueQualityServiceTests
	{
		/// <summary>A dialogue with one unreachable node (line 15) and one option to an unknown procedure (line 5).</summary>
		private static readonly String _script = String.Join(
			"\r\n",
			"#define NAME SCRIPT_NPC",                   // 1
			"#define SCRIPT_NPC 123",                    // 2
			"procedure talk_p_proc begin",               // 3
			"   Reply(100);",                            // 4
			"   NOption(101, Node404, 4);",              // 5
			"   NOption(102, Node001, 4);",              // 6
			"   NLowOption(103, Node999);",              // 7
			"end",                                       // 8
			"procedure Node001 begin",                   // 9
			"   Reply(110);",                            // 10
			"   NOption(111, Node999, 4);",              // 11
			"end",                                       // 12
			"procedure Node999 begin",                   // 13
			"end",                                       // 14
			"procedure Node777 begin",                   // 15
			"   Reply(130);",                            // 16
			"   NOption(131, Node999, 4);",              // 17
			"end",                                       // 18
			String.Empty);

		private static readonly String NodeBlock = String.Join("\r\n", "procedure Node777 begin", "   Reply(130);", "   NOption(131, Node999, 4);", "end", String.Empty);

		private static IReadOnlyList<Problem> OfCheck(ShellHarness harness)
		{
			return harness.Problems.Problems.Where(problem => problem.Origin == ProblemOrigins.DialogueQuality).ToList();
		}

		[Fact]
		public void The_check_lists_the_weak_points_at_their_lines_with_the_names_of_the_nodes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);

				DialogueCheckResult result = await harness.Workspace.CheckDialogueAsync(script);

				Assert.True(result.HasDialogue);
				IReadOnlyList<Problem> problems = OfCheck(harness);
				Assert.Equal(2, result.Count);
				Assert.Equal(2, problems.Count);
				Assert.All(problems, problem =>
				{
					Assert.Equal(ProblemSource.Parse, problem.Source);
					Assert.Same(script, problem.Owner);
					Assert.Equal(script.FilePath, problem.File);
				});

				Problem undefined = Assert.Single(problems, p => p.Severity == ProblemSeverity.Error);
				Assert.Equal(5, undefined.Line);
				Assert.Equal("Str.Shell.Problem.DlgUndefinedTarget", undefined.MessageKey);
				Assert.Contains("talk_p_proc", undefined.Message, StringComparison.Ordinal);
				Assert.Contains("Node404", undefined.Message, StringComparison.Ordinal);
				Problem unreachable = Assert.Single(problems, p => p.Severity == ProblemSeverity.Warning);
				Assert.Equal(15, unreachable.Line);
				Assert.Contains("Node777", unreachable.Message, StringComparison.Ordinal);
				Assert.Contains("[DLG001]", unreachable.Message, StringComparison.Ordinal);
				Assert.NotNull(unreachable.Hint);
			});
		}

		[Fact]
		public void A_new_check_replaces_the_previous_results_and_a_clean_dialogue_leaves_none()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				await harness.Workspace.CheckDialogueAsync(script);
				await harness.Workspace.CheckDialogueAsync(script);
				Assert.Equal(2, OfCheck(harness).Count);

				script.TextDocument.Text = _script.Replace("NOption(101, Node404, 4);", "NOption(101, Node999, 4);", StringComparison.Ordinal).Replace(NodeBlock, String.Empty, StringComparison.Ordinal);
				DialogueCheckResult result = await harness.Workspace.CheckDialogueAsync(script);

				Assert.True(result.HasDialogue);
				Assert.Equal(0, result.Count);
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void A_script_without_dialogue_statements_is_not_a_dialogue()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/quiet.ssl", "procedure start begin\r\nend\r\n");

				DialogueCheckResult result = await harness.Workspace.CheckDialogueAsync(script);

				Assert.False(result.HasDialogue);
				Assert.Equal(0, result.Count);
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void An_unsaved_script_cannot_be_checked_because_the_problems_point_to_a_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = harness.Workspace.CreateNew(_script);

				DialogueCheckResult result = await harness.Workspace.CheckDialogueAsync(script);

				Assert.False(result.HasDialogue);
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void The_messages_that_the_message_file_lacks_are_linked_to_the_node()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script.Replace("NOption(101, Node404, 4);", "NOption(101, Node999, 4);", StringComparison.Ordinal));
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n{101}{}{More.}\r\n{102}{}{More.}\r\n{103}{}{Bye.}\r\n{110}{}{Reply.}\r\n{111}{}{Leave.}\r\n{130}{}{x}\r\n{131}{}{y}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				Assert.Empty(OfCheck(harness));

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n{102}{}{More.}\r\n{103}{}{Bye.}\r\n{110}{}{Reply.}\r\n{111}{}{Leave.}\r\n{130}{}{x}\r\n{131}{}{y}\r\n");
				await harness.Workspace.CheckDialogueAsync(script);

				Problem problem = Assert.Single(OfCheck(harness), p => p.MessageKey == "Str.Shell.Problem.DlgMissingMessages");
				Assert.Equal(ProblemSeverity.Info, problem.Severity);
				Assert.Equal(5, problem.Line);
				Assert.Contains("101", problem.Message, StringComparison.Ordinal);
				Assert.Contains("talk_p_proc", problem.Message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_findings_survive_a_new_parse_of_the_same_text_and_go_with_a_change_of_the_text()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				await harness.Workspace.CheckDialogueAsync(script);

				await harness.Workspace.RequestParseAsync(script, true);
				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 2));

				script.TextDocument.Insert(script.TextDocument.TextLength, "// edited\r\n");
				await harness.Workspace.RequestParseAsync(script, true);

				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 0));
			});
		}

		[Fact]
		public void A_saved_script_is_checked_by_itself_a_moment_later_when_the_setting_is_on()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Assert.True(harness.Settings.Settings.Messages.CheckDialogueOnSave);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				Assert.Empty(OfCheck(harness));

				script.TextDocument.Insert(script.TextDocument.TextLength, "// edited\r\n");
				Assert.True(await harness.Workspace.SaveAsync(script));

				Assert.True(await Eventually.TrueAsync(() => OfCheck(harness).Count == 2));
			});
		}

		[Fact]
		public void A_saved_script_is_not_checked_when_the_setting_is_off_or_when_it_cannot_have_a_dialogue()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Messages.CheckDialogueOnSave = false);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);
				script.TextDocument.Insert(script.TextDocument.TextLength, "// edited\r\n");
				Assert.True(await harness.Workspace.SaveAsync(script));
				await Task.Delay(400);
				Assert.Empty(OfCheck(harness));

				harness.Settings.Update(settings => settings.Messages.CheckDialogueOnSave = true);
				DocumentViewModel plain = await harness.OpenFileAsync("scripts/plain.ssl", "procedure start begin\r\nend\r\n");
				plain.TextDocument.Insert(plain.TextDocument.TextLength, "// edited\r\n");
				Assert.True(await harness.Workspace.SaveAsync(plain));
				await Task.Delay(400);
				Assert.Empty(OfCheck(harness));
			});
		}

		[Fact]
		public void The_analysis_for_the_flowchart_reports_nothing_as_problems()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _script);

				DialogueQualityReport report = await harness.DialogueQuality.AnalyzeAsync(script);

				Assert.Equal(2, report.Findings.Count);
				Assert.Contains(report.Findings, finding => finding.Code == DialogueQualityCode.UnreachableNode && finding.Node == "Node777");
				Assert.Empty(OfCheck(harness));
			});
		}

		[Theory]
		[InlineData("procedure Node001 begin", true)]
		[InlineData("PROCEDURE TALK_P_PROC BEGIN", true)]
		[InlineData("procedure start begin\r\nend", false)]
		public void Only_a_text_that_mentions_a_node_is_worth_a_check_on_save(String text, Boolean expected)
		{
			Assert.Equal(expected, DialogueQualityService.MayHaveDialogue(text));
		}
	}
}