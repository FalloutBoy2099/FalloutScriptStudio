namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;

	public class DialogueScriptEditorTests
	{
		[Fact]
		public void GetNodeNames_ListsTheNodesOfTheScript()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, harness.ScriptEditor.GetNodeNames(harness.Script).ToArray());
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void GetNodeCode_ReturnsTheBodyOrNull()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				Assert.Contains("Reply(120);", harness.ScriptEditor.GetNodeCode(harness.Script, "Node002"), StringComparison.Ordinal);
				Assert.Null(harness.ScriptEditor.GetNodeCode(harness.Script, "Node404"));
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void ReplaceNodeCode_ReparsesTheScript()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				Int32 parses = harness.WorkspaceRecorder.ParseCount;

				Assert.True(await harness.ScriptEditor.ReplaceNodeCodeAsync(harness.Script, "Node777", "\r\n   Reply(131);\r\n"));
				Assert.False(await harness.ScriptEditor.ReplaceNodeCodeAsync(harness.Script, "Node404", "x"));

				Assert.Contains("Reply(131);", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(parses + 1, harness.WorkspaceRecorder.ParseCount);
			});
		}

		[Fact]
		public void CreateNode_ReportsWhyANameIsRefused()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				String original = harness.Script.TextDocument.Text;

				Assert.Equal(NodeCreationError.EmptyName, (await harness.ScriptEditor.CreateNodeAsync(harness.Script, " ", String.Empty)).Error);
				Assert.Equal(NodeCreationError.PlaceholderInName, (await harness.ScriptEditor.CreateNodeAsync(harness.Script, "Node###", String.Empty)).Error);
				Assert.Equal(NodeCreationError.InvalidName, (await harness.ScriptEditor.CreateNodeAsync(harness.Script, "Node 5", String.Empty)).Error);
				Assert.Equal(NodeCreationError.NameExists, (await harness.ScriptEditor.CreateNodeAsync(harness.Script, "node001", String.Empty)).Error);

				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void CreateNode_ParsesAScriptThatWasNotParsedYet()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.ProgramInfo = null;

				NodeCreationResult result = await harness.ScriptEditor.CreateNodeAsync(harness.Script, "Node500", "\r\n   Reply(1);\r\n");

				Assert.True(result.Succeeded);
				Assert.Contains("procedure Node500 begin", harness.Script.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void DeleteNodes_RemovesTheProceduresAndTheirDeclarations()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				Int32 count = await harness.ScriptEditor.DeleteNodesAsync(harness.Script, new[] { "Node777" });

				Assert.True(count > 0);
				Assert.DoesNotContain("Node777", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(0, await harness.ScriptEditor.DeleteNodesAsync(harness.Script, new[] { "Node404" }));
			});
		}

		[Fact]
		public void RenameNode_OfAnUnknownNode_ReportsNotFound()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				NodeRenameResult result = await harness.ScriptEditor.RenameNodeAsync(harness.Script, "Node404");

				Assert.Equal(NodeRenameOutcome.NotFound, result.Outcome);
			});
		}
	}
}