namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class DialoguePreviewTests
	{
		private static DialoguePreview Create()
		{
			return new DialoguePreview(new DialogueNodeParser(new DialogueFunctionRules()), new ProcedureTextService());
		}

		private static async Task<DialoguePreview> StartAsync(Int32 caretLine = 1, String? script = null, FakeMessageSource? messages = null)
		{
			DialoguePreview preview = Create();
			String text = script ?? DialogueSamples.Script();
			Boolean ready = await preview.StartAsync(text, DialogueSamples.CreateInfo(text), messages ?? DialogueSamples.Messages(), caretLine);
			Assert.True(ready);
			return preview;
		}

		[Fact]
		public async Task Start_OutsideAnyNode_ShowsTheTalkProcedure()
		{
			DialoguePreview preview = await StartAsync();

			Assert.True(preview.IsReady);
			Assert.Equal("talk_p_proc", preview.CurrentNode);
			Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, preview.NodeNames.ToArray());
			Assert.False(preview.CanGoBack);
			Assert.False(preview.CanGoForward);
		}

		[Fact]
		public async Task Start_InsideANode_ShowsThatNode()
		{
			DialoguePreview preview = await StartAsync(caretLine: 33);

			Assert.Equal("Node001", preview.CurrentNode);
		}

		[Fact]
		public async Task Start_InsideAProcedureThatIsNotANode_ShowsTheTalkProcedure()
		{
			DialoguePreview preview = await StartAsync(caretLine: 17);

			Assert.Equal("talk_p_proc", preview.CurrentNode);
		}

		[Fact]
		public async Task Start_WithoutDialogue_IsNotReady()
		{
			DialoguePreview preview = Create();
			String text = "procedure start begin\r\nend";

			Boolean ready = await preview.StartAsync(text, DialogueSamples.CreateInfo(text), DialogueSamples.Messages(), 1);

			Assert.False(ready);
			Assert.False(preview.IsReady);
			Assert.Empty(preview.Rows);
		}

		[Fact]
		public async Task Rows_ShowTextsTargetsAndIq()
		{
			DialoguePreview preview = await StartAsync();

			DialoguePreviewRow[] rows = preview.Rows.ToArray();
			Assert.Equal(5, rows.Length);

			Assert.Equal((DialogueOpcode.Reply, "[Reply]", "Hello there, stranger.", 100), (rows[0].Opcode, rows[0].TargetNode, rows[0].MessageText, rows[0].MessageNumber));
			Assert.False(rows[0].IsOption);
			Assert.False(rows[0].CanFollow);
			Assert.Equal("Hello there, stranger.", rows[0].DisplayText);

			Assert.Equal(("Node001", "4", "I need your help."), (rows[1].TargetNode, rows[1].Iq, rows[1].MessageText));
			Assert.True(rows[1].IsOption);
			Assert.True(rows[1].CanFollow);
			Assert.Equal(DialoguePreviewRow.OptionBullet + " I need your help.", rows[1].DisplayText);
			Assert.EndsWith("dcjohn.msg", rows[1].MessageFilePath);
			Assert.Equal("Goodbye.", rows[2].MessageText);
			Assert.Equal("Node999", rows[2].TargetNode);
		}

		[Fact]
		public async Task Rows_OfAnotherMessageFile_ReportTheFile()
		{
			DialoguePreview preview = await StartAsync(caretLine: 33);

			DialoguePreviewRow reply = preview.Rows.First();
			Assert.Equal("Reply from message_str.", reply.MessageText);
			Assert.Equal(123, reply.MessageFileNumber);
			Assert.EndsWith("other.msg", reply.MessageFilePath);
			Assert.Equal(7, preview.Rows.Count);
			Assert.True(preview.Rows.Last().CanFollow);
			Assert.Equal(DialogueOpcode.Call, preview.Rows.Last().Opcode);
		}

		[Fact]
		public async Task Rows_WithoutAMessageNumber_ShowTheShortCode()
		{
			String script = DialogueSamples.Script().Replace("   Reply(130);", "   Reply(some_unknown);\r\n   Reply(777);");
			DialoguePreview preview = await StartAsync(script: script);
			await preview.SelectNodeAsync("Node777");

			Assert.Equal("<Reply(some_unknown)>", preview.Rows[0].MessageText);
			Assert.False(preview.Rows[0].IsError);
			Assert.True(preview.Rows[1].IsError);
			Assert.Equal(MessageFileTexts.TextNotFound, preview.Rows[1].MessageText);
		}

		[Fact]
		public async Task Rows_WithAMissingMessageFile_AreErrors()
		{
			var messages = new FakeMessageSource();
			messages.SetFile(-1, DialogueSamples.OwnMessages(), "own.msg");
			DialoguePreview preview = await StartAsync(caretLine: 33, messages: messages);

			DialoguePreviewRow reply = preview.Rows.First();
			Assert.True(reply.IsError);
			Assert.Equal(DialogueMessageStatus.FileNotFound, reply.Status);
			Assert.Equal(String.Format(MessageFileTexts.FileNotFoundFormat, 123), reply.MessageText);
		}

		[Fact]
		public async Task Follow_GoesToTheTargetNode_AndFillsTheHistory()
		{
			DialoguePreview preview = await StartAsync();

			Boolean followed = await preview.FollowAsync(preview.Rows[1]);

			Assert.True(followed);
			Assert.Equal("Node001", preview.CurrentNode);
			Assert.True(preview.CanGoBack);
			Assert.False(preview.CanGoForward);
			Assert.Equal(7, preview.Rows.Count);
		}

		[Fact]
		public async Task Follow_ARowThatCannotBeFollowed_DoesNothing()
		{
			DialoguePreview preview = await StartAsync();

			Assert.False(await preview.FollowAsync(preview.Rows[0]));
			Assert.Equal("talk_p_proc", preview.CurrentNode);
		}

		[Fact]
		public async Task Navigation_BackAndForward()
		{
			DialoguePreview preview = await StartAsync();
			await preview.FollowAsync(preview.Rows[1]);                          // Node001
			await preview.FollowAsync(preview.Rows.Last());                      // call Node002

			Assert.Equal("Node002", preview.CurrentNode);
			Assert.True(await preview.GoBackAsync());
			Assert.Equal("Node001", preview.CurrentNode);
			Assert.True(preview.CanGoForward);
			Assert.True(await preview.GoBackAsync());
			Assert.Equal("talk_p_proc", preview.CurrentNode);
			Assert.False(await preview.GoBackAsync());
			Assert.True(await preview.GoForwardAsync());
			Assert.True(await preview.GoForwardAsync());
			Assert.Equal("Node002", preview.CurrentNode);
			Assert.False(await preview.GoForwardAsync());
		}

		[Fact]
		public async Task Navigation_FollowingAfterGoingBack_DropsTheForwardHistory()
		{
			DialoguePreview preview = await StartAsync();
			await preview.FollowAsync(preview.Rows[1]);
			await preview.GoBackAsync();

			await preview.FollowAsync(preview.Rows[2]);                          // Node999

			Assert.Equal("Node999", preview.CurrentNode);
			Assert.False(preview.CanGoForward);
			await preview.GoBackAsync();
			Assert.Equal("talk_p_proc", preview.CurrentNode);
		}

		[Fact]
		public async Task SelectNode_StartsTheHistoryAgain()
		{
			DialoguePreview preview = await StartAsync();
			await preview.FollowAsync(preview.Rows[1]);

			Assert.True(await preview.SelectNodeAsync("node002"));

			Assert.Equal("Node002", preview.CurrentNode);
			Assert.False(preview.CanGoBack);
			Assert.False(await preview.SelectNodeAsync("missing"));
			Assert.Equal("Node002", preview.CurrentNode);
		}

		[Fact]
		public async Task FemaleMode_AddsTheOffsetToMessageNumbers()
		{
			DialoguePreview preview = await StartAsync();

			await preview.SetFemaleModeAsync(true, 1);

			Assert.True(preview.FemaleMode);
			Assert.Equal(1, preview.FemaleOffset);
			Assert.Equal("I need your help.", preview.Rows[0].MessageText);
			Assert.Equal(101, preview.Rows[0].MessageNumber);
			Assert.Equal("Goodbye.", preview.Rows[1].MessageText);
		}

		[Fact]
		public async Task FemaleMode_FallsBackToTheMaleMessage()
		{
			DialoguePreview preview = await StartAsync();

			await preview.SetFemaleModeAsync(true, 500);

			Assert.Equal("Hello there, stranger.", preview.Rows[0].MessageText);
			Assert.Equal(100, preview.Rows[0].MessageNumber);

			await preview.SetFemaleModeAsync(false, 500);
			Assert.Equal(0, preview.FemaleOffset);
		}

		[Fact]
		public async Task UpdateScript_RefreshesTheNodeListAndTheCurrentNode()
		{
			DialoguePreview preview = await StartAsync(caretLine: 33);
			String changed = DialogueSamples.Script().Replace("   NMessage(111);", "   NMessage(102);") + "\r\n\r\nprocedure Node555 begin\r\nend";

			await preview.UpdateScriptAsync(changed, DialogueSamples.CreateInfo(changed));

			Assert.Contains("Node555", preview.NodeNames);
			Assert.Equal("Node001", preview.CurrentNode);
			Assert.Equal("Goodbye.", preview.Rows[1].MessageText);
		}

		[Fact]
		public async Task UpdateScript_WhenTheCurrentNodeIsGone_ClearsTheRows()
		{
			DialoguePreview preview = await StartAsync(caretLine: 33);
			String changed = "procedure talk_p_proc begin\r\n   Reply(100);\r\nend";

			await preview.UpdateScriptAsync(changed, DialogueSamples.CreateInfo(changed));

			Assert.Empty(preview.Rows);
		}

		[Fact]
		public async Task GetJumpTarget_PointsAtTheNodeToShowInTheEditor()
		{
			DialoguePreview preview = await StartAsync();

			Assert.Equal(20, preview.GetJumpTarget(preview.Rows[0])!.Line);
			Assert.Equal(31, preview.GetJumpTarget(preview.Rows[1])!.Line);
		}

		[Fact]
		public async Task Changed_IsRaisedWhenTheStateChanges()
		{
			DialoguePreview preview = await StartAsync();
			Int32 raised = 0;
			preview.Changed += (_, _) => raised++;

			await preview.FollowAsync(preview.Rows[1]);
			await preview.GoBackAsync();
			await preview.RefreshAsync();

			Assert.Equal(3, raised);
		}
	}
}
