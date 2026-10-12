namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartViewModelQuickEditTests
	{
		[Fact]
		public void QuickEdit_WritesTheNewTextToTheMessageFile()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramRow row = harness.Canvas.FindNode("Node001")!.Rows[0];
				harness.FlowDialogs.MessageAnswer = "Changed text";

				await harness.ViewModel.QuickEditMessageAsync(row);

				Assert.Equal((FlowchartSamples.MessagePath, 110, "Changed text"), Assert.Single(harness.Messages.Updates));
				Assert.Equal("\"Changed text\"", row.Text);
				Assert.Equal("Message file: " + FlowchartSamples.MessagePath, Assert.Single(harness.FlowDialogs.Captions));
			});
		}

		[Fact]
		public void QuickEdit_Cancelled_ChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramRow row = harness.Canvas.FindNode("Node001")!.Rows[1];
				harness.FlowDialogs.MessageAnswer = null;

				await harness.ViewModel.QuickEditMessageAsync(row);

				Assert.Empty(harness.Messages.Updates);
				Assert.Equal("Tell me more.", row.Text);
			});
		}

		[Fact]
		public void QuickEdit_OfAMissingMessage_ReportsIt()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("Reply(110)", "Reply(999)", StringComparison.Ordinal);
				await harness.LoadAsync();
				DiagramRow row = harness.Canvas.FindNode("Node001")!.Rows[0];
				harness.FlowDialogs.MessageAnswer = "text";

				await harness.ViewModel.QuickEditMessageAsync(row);

				Assert.Empty(harness.Messages.Updates);
				Assert.Single(harness.Dialogs.Errors);
			});
		}

		[Fact]
		public void QuickEdit_OfACodeRow_DoesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.FlowDialogs.MessageAnswer = "text";

				await harness.ViewModel.QuickEditMessageAsync(harness.Canvas.FindNode("Node001")!.Rows[2]);

				Assert.Empty(harness.Messages.Updates);
				Assert.Empty(harness.FlowDialogs.Captions);
			});
		}
	}
}
