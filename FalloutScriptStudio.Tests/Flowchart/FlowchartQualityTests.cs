namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The marks of the dialogue check on the nodes of the flowchart: the view model asks an analyzer and marks the nodes of the canvas.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartQualityTests
	{
		/// <summary>Runs the real checker on the text of the script, like the application does.</summary>
		private sealed class CheckingAnalyzer : IDialogueQualityAnalyzer
		{
			public Int32 Calls { get; private set; }

			public Exception? Failure { get; set; }

			public Task<DialogueQualityReport> AnalyzeAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
			{
				this.Calls++;
				if (this.Failure is not null)
				{
					throw this.Failure;
				}

				DialogueQualityReport report = DialogueQualityChecker.Check(script.TextDocument.Text, script.ProgramInfo!, new DialogueNodeParser(new DialogueFunctionRules()));
				return Task.FromResult(report);
			}
		}

		[Fact]
		public void The_nodes_with_a_problem_are_marked_and_the_unreachable_ones_are_dimmed()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(quality: new CheckingAnalyzer());

				await harness.LoadAsync();

				DiagramNode unreachable = harness.Canvas.FindNode("Node777")!;
				Assert.Equal(DiagramIssueSeverity.Warning, unreachable.IssueSeverity);
				Assert.True(unreachable.IsDimmed);
				Assert.Contains("Node777", unreachable.IssueText, StringComparison.Ordinal);
				Assert.Contains("[DLG001]", unreachable.IssueText, StringComparison.Ordinal);
				Assert.Contains("[DLG002]", unreachable.IssueText, StringComparison.Ordinal);
				Assert.Contains(Environment.NewLine, unreachable.IssueText, StringComparison.Ordinal);

				foreach (String name in new[] { "talk_p_proc", "Node001", "Node002", "Node999" })
				{
					DiagramNode node = harness.Canvas.FindNode(name)!;
					Assert.Equal(DiagramIssueSeverity.None, node.IssueSeverity);
					Assert.False(node.IsDimmed);
					Assert.Equal(String.Empty, node.IssueText);
				}
			});
		}

		[Fact]
		public void An_error_gives_the_node_the_badge_of_an_error()
		{
			StaTestRunner.Run(async () =>
			{
				String broken = FlowchartSamples.Script().Replace("NOption(111, Node002, 4);", "NOption(111, Node404, 4);", StringComparison.Ordinal);
				using FlowchartHarness harness = new FlowchartHarness(broken, quality: new CheckingAnalyzer());

				await harness.LoadAsync();

				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				Assert.Equal(DiagramIssueSeverity.Error, node.IssueSeverity);
				Assert.False(node.IsDimmed);
				Assert.Contains("Node404", node.IssueText, StringComparison.Ordinal);
				Assert.Equal(DiagramIssueSeverity.Warning, harness.Canvas.FindNode("Node002")!.IssueSeverity);
			});
		}

		[Fact]
		public void The_issues_are_listed_with_their_node_and_counted_for_the_button_and_the_summary()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(quality: new CheckingAnalyzer());

				await harness.LoadAsync();

				Assert.True(harness.ViewModel.HasQualityIssues);
				Assert.Equal(2, harness.ViewModel.QualityIssues.Count);
				Assert.All(harness.ViewModel.QualityIssues, issue => Assert.Equal("Node777", issue.Node));
				Assert.Equal("Issues (2)", harness.ViewModel.QualityButtonText);
				Assert.Equal("2 weak point(s) in the dialogue.", harness.ViewModel.QualitySummary);
				FlowchartIssueViewModel first = harness.ViewModel.QualityIssues[0];
				Assert.StartsWith("Warning: ", first.AccessibleName, StringComparison.Ordinal);
				Assert.Contains("Node777", first.Text, StringComparison.Ordinal);
				Assert.Equal(DiagramIssueSeverity.Warning, first.Severity);
			});
		}

		[Fact]
		public void Going_to_an_issue_selects_and_shows_its_node()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(quality: new CheckingAnalyzer());
				await harness.LoadAsync();

				harness.ViewModel.GoToIssueCommand.Execute(harness.ViewModel.QualityIssues[0]);

				Assert.True(harness.Canvas.FindNode("Node777")!.IsSelected);
			});
		}

		[Fact]
		public void Updating_the_chart_checks_the_dialogue_again_and_removes_the_marks_of_what_was_fixed()
		{
			StaTestRunner.Run(async () =>
			{
				CheckingAnalyzer analyzer = new();
				using FlowchartHarness harness = new FlowchartHarness(quality: analyzer);
				await harness.LoadAsync();
				Assert.Equal(DiagramIssueSeverity.Warning, harness.Canvas.FindNode("Node777")!.IssueSeverity);

				harness.Script.TextDocument.Text = FlowchartSamples.Script().Replace("   NLowOption(102, Node999);", "   NLowOption(102, Node999);\r\n   NOption(103, Node777, 4);", StringComparison.Ordinal).Replace("   Reply(130);\r\nend", "   Reply(130);\r\n   NOption(131, Node999, 4);\r\nend", StringComparison.Ordinal);
				harness.Script.ProgramInfo = new FalloutScriptStudio.Core.Parsing.InternalParser().Parse(harness.Script.TextDocument.Text, harness.Script.FilePath);
				await harness.ViewModel.UpdateAsync();

				Assert.Equal(2, analyzer.Calls);
				DiagramNode node = harness.Canvas.FindNode("Node777")!;
				Assert.Equal(DiagramIssueSeverity.None, node.IssueSeverity);
				Assert.False(node.IsDimmed);
				Assert.False(harness.ViewModel.HasQualityIssues);
				Assert.Equal("No weak point in the dialogue.", harness.ViewModel.QualitySummary);
				Assert.Equal("Issues (0)", harness.ViewModel.QualityButtonText);
			});
		}

		[Fact]
		public void Without_an_analyzer_or_when_it_fails_the_nodes_carry_no_mark()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness without = new FlowchartHarness();
				await without.LoadAsync();
				Assert.All(without.Canvas.Nodes, node => Assert.Equal(DiagramIssueSeverity.None, node.IssueSeverity));
				Assert.Empty(without.ViewModel.QualityIssues);

				using FlowchartHarness failing = new FlowchartHarness(quality: new CheckingAnalyzer { Failure = new InvalidOperationException("broken") });
				await failing.LoadAsync();
				Assert.Equal(5, failing.Canvas.Nodes.Count());
				Assert.All(failing.Canvas.Nodes, node => Assert.Equal(DiagramIssueSeverity.None, node.IssueSeverity));
				Assert.False(failing.ViewModel.HasQualityIssues);
			});
		}
	}
}