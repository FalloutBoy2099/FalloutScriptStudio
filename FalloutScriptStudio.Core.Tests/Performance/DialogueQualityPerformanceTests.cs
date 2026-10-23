namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Timings of the dialogue quality check on the generated dialogue of 200 nodes and on a dialogue four times as big.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class DialogueQualityPerformanceTests
	{
		private readonly PerfMeter _meter;

		public DialogueQualityPerformanceTests(ITestOutputHelper output)
		{
			this._meter = new PerfMeter(output);
		}

		[Theory]
		[InlineData(200, 150)]
		[InlineData(900, 1000)]
		public void The_check_of_a_generated_dialogue_stays_fast(Int32 nodes, Int32 budgetMilliseconds)
		{
			String script = PerformanceInputs.BuildDialogueScript(nodes);
			String path = Path.Combine(TestFiles.Work, "dialogue-quality.ssl");
			ProgramInfo info = new InternalParser().Parse(script, path)
				.WithScriptMacros(new MacroCollector(new IncludeResolver(null, null)).CollectFromText(script, path, false));
			DialogueNodeParser parser = new DialogueNodeParser(new DialogueFunctionRules());

			this._meter.Measure("DialogueQualityChecker.Check " + nodes + " nodes", budgetMilliseconds, () =>
			{
				DialogueQualityReport report = DialogueQualityChecker.Check(script, info, parser);
				Assert.Equal(nodes + 2, report.NodeCount);
				Assert.Empty(report.Findings);
			}, 3);
		}
	}
}