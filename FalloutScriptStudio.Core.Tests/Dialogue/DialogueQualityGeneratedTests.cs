namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>No false positive on the generated dialogues and on the real scripts that ship with the tests (none of them is a dialogue).</summary>
	public class DialogueQualityGeneratedTests
	{
		private static DialogueQualityReport CheckGenerated(Int32 nodes, DialogueFunctionRules rules)
		{
			String script = PerformanceInputs.BuildDialogueScript(nodes);
			ProgramInfo info = new InternalParser().Parse(script, @"C:\mod\scripts\perf_dialogue.ssl")
				.WithScriptMacros(new MacroCollector(new IncludeResolver(null, null)).CollectFromText(script, @"C:\mod\scripts\perf_dialogue.ssl", false));
			return DialogueQualityChecker.Check(script, info, new DialogueNodeParser(rules));
		}

		[Theory]
		[InlineData(2)]
		[InlineData(10)]
		[InlineData(200)]
		public void A_generated_dialogue_has_no_error_and_no_warning(Int32 nodes)
		{
			DialogueQualityReport withHeaderRules = CheckGenerated(nodes, DialogueQualitySamples.Rules());
			DialogueQualityReport builtInRules = CheckGenerated(nodes, new DialogueFunctionRules());

			Assert.True(withHeaderRules.HasDialogue);
			Assert.Equal(nodes + 2, withHeaderRules.NodeCount);
			Assert.DoesNotContain(withHeaderRules.Findings, finding => finding.Severity != DialogueQualitySeverity.Info);
			Assert.DoesNotContain(builtInRules.Findings, finding => finding.Severity != DialogueQualitySeverity.Info);
		}

		[Fact]
		public void A_generated_dialogue_has_only_the_notes_that_the_generator_makes_true()
		{
			DialogueQualityReport report = CheckGenerated(200, DialogueQualitySamples.Rules());

			// For n = 100 and n = 200 the two "next" options of the generator lead to the same node, with different intelligence requirements.
			Assert.Empty(report.Findings);
		}

		[Fact]
		public void The_real_scripts_of_the_tests_have_no_dialogue_and_so_no_finding()
		{
			String folder = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			if (!Directory.Exists(folder))
			{
				return;
			}

			List<String> withFindings = [];
			foreach (String path in Directory.EnumerateFiles(folder, "*.ssl"))
			{
				String text = File.ReadAllText(path);
				ProgramInfo info = new InternalParser().Parse(text, path);
				DialogueQualityReport report = DialogueQualityChecker.Check(text, info, new DialogueNodeParser(new DialogueFunctionRules()));
				if (report.Findings.Count > 0)
				{
					withFindings.Add(Path.GetFileName(path));
				}
			}

			Assert.Empty(withFindings);
		}
	}
}