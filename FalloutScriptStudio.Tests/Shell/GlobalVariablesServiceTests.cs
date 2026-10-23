namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class GlobalVariablesServiceTests
	{
		private static GlobalVariableIndex Index()
		{
			return GlobalVariableIndex.FromTexts(
			[
				new("c:\\p\\a.ssl", "procedure start begin\r\n\tvariable v := global_var(GVAR_NEVER_WRITTEN);\r\n\tset_global_var(GVAR_NEVER_READ, 1);\r\nend\r\n"),
			]);
		}

		[Fact]
		public void The_options_carry_the_ignore_list_of_the_settings()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Settings.Update(settings => settings.Editor.GlobalVariableIgnore.AddRange(["GVAR_A", "GVAR_B*"]));

			Assert.Equal(new[] { "GVAR_A", "GVAR_B*" }, harness.GlobalsService.CreateOptions().Ignored.ToArray());
		}

		[Fact]
		public void Ignoring_adds_an_entry_once_ignoring_the_case()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.True(harness.GlobalsService.Ignore("  GVAR_X "));
			Assert.False(harness.GlobalsService.Ignore("gvar_x"));

			Assert.Equal(new[] { "GVAR_X" }, harness.Settings.Settings.Editor.GlobalVariableIgnore.ToArray());
			Assert.Throws<ArgumentException>(() => harness.GlobalsService.Ignore("   "));
		}

		[Fact]
		public void The_findings_are_not_listed_by_themselves_unless_the_setting_is_on()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Assert.False(harness.Settings.Settings.Editor.GlobalVariableFindings);
				harness.SymbolIndex.GlobalVariables = Index();
				Boolean changed = false;
				harness.GlobalsService.Changed += (_, _) => changed = true;

				harness.SymbolIndex.RaiseUpdated();
				await Eventually.TrueAsync(() => changed);
				await Task.Delay(100);

				Assert.DoesNotContain(harness.Problems.Problems, p => p.Origin == ProblemOrigins.GlobalVariables);
			});
		}

		[Fact]
		public void With_the_setting_on_the_findings_follow_each_update()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.GlobalVariableFindings = true);
				harness.SymbolIndex.GlobalVariables = Index();

				harness.SymbolIndex.RaiseUpdated();

				await Eventually.TrueAsync(() => harness.Problems.Problems.Any(p => p.Origin == ProblemOrigins.GlobalVariables));
				Problem[] problems = harness.Problems.Problems.Where(p => p.Origin == ProblemOrigins.GlobalVariables).ToArray();
				Assert.Equal(2, problems.Length);
				Assert.Contains(problems, p => p.Severity == ProblemSeverity.Info && p.MessageKey == "Str.Shell.Problem.GlobalWrittenNeverRead" && p.Line == 3);
				Assert.Contains(problems, p => p.Severity == ProblemSeverity.Warning && p.MessageKey == "Str.Shell.Problem.GlobalReadNeverWritten" && p.Line == 2);

				harness.SymbolIndex.GlobalVariables = GlobalVariableIndex.Empty;
				harness.SymbolIndex.RaiseUpdated();
				await Eventually.TrueAsync(() => !harness.Problems.Problems.Any(p => p.Origin == ProblemOrigins.GlobalVariables));
			});
		}

		[Fact]
		public void A_check_replaces_the_problems_of_the_previous_check()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.SymbolIndex.GlobalVariables = Index();

				IReadOnlyList<GlobalFinding> first = await harness.GlobalsService.CheckAsync();
				IReadOnlyList<GlobalFinding> second = await harness.GlobalsService.CheckAsync();

				Assert.Equal(2, first.Count);
				Assert.Equal(2, second.Count);
				Assert.Equal(2, harness.Problems.Problems.Count(p => p.Origin == ProblemOrigins.GlobalVariables));
			});
		}

		[Fact]
		public void A_disposed_service_announces_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				GlobalVariablesService service = new(harness.SymbolIndex, harness.Settings, harness.Problems, TimeSpan.FromMilliseconds(20));
				Boolean changed = false;
				service.Changed += (_, _) => changed = true;
				service.Dispose();

				harness.SymbolIndex.RaiseUpdated();
				await Task.Delay(150);

				Assert.False(changed);
			});
		}

		[Fact]
		public void The_problem_of_a_finding_has_the_place_of_the_use_and_a_hint_for_every_kind()
		{
			foreach (GlobalFindingKind kind in Enum.GetValues<GlobalFindingKind>())
			{
				Problem problem = ProblemFactory.GlobalVariable(new GlobalFinding(kind, GlobalFindingSeverity.Warning, "GVAR_A", "GVAR_B", 3, "c:\\p\\a.ssl", 12, "start"));

				Assert.Equal("c:\\p\\a.ssl", problem.File);
				Assert.Equal(12, problem.Line);
				Assert.Equal(ProblemOrigins.GlobalVariables, problem.Origin);
				Assert.Null(problem.Owner);
				Assert.False(String.IsNullOrWhiteSpace(problem.Message));
				Assert.False(String.IsNullOrWhiteSpace(problem.Hint));
				Assert.DoesNotContain("{", problem.Message, StringComparison.Ordinal);
				Assert.DoesNotContain("{", problem.Hint, StringComparison.Ordinal);
			}
		}

		[Fact]
		public void The_similar_name_hint_asks_did_you_mean()
		{
			Problem problem = ProblemFactory.GlobalVariable(new GlobalFinding(GlobalFindingKind.SimilarName, GlobalFindingSeverity.Info, "GVAR_QUEST_DONr", "GVAR_QUEST_DONE", 2, "c:\\p\\a.ssl", 4, null));

			Assert.Contains("Did you mean GVAR_QUEST_DONE?", problem.Hint, StringComparison.Ordinal);
			Assert.Equal(ProblemSeverity.Info, problem.Severity);
		}
	}
}