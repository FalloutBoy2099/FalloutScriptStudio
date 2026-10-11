namespace FalloutScriptStudio.Tests.DialoguePreview
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Tests.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using Xunit;

	public class DialoguePreviewViewModelTests
	{
		private static async Task<DialoguePreviewViewModel> StartAsync(FlowchartHarness harness)
		{
			DialoguePreviewViewModel preview = harness.CreatePreview();
			Assert.True(await preview.InitializeAsync(harness.Script));
			return preview;
		}

		private static async Task WaitAsync(Func<Boolean> condition)
		{
			for (Int32 i = 0; i < 100 && !condition(); i++)
			{
				await Task.Delay(20);
			}
		}

		[Fact]
		public void Initialize_WhenAnotherMessageFileLoadsOnAThreadPoolThread_UpdatesTheBoundListsOnTheUiThread()
		{
			StaTestRunner.Run(async () =>
			{
				String script = FlowchartSamples.Script().Replace("   Reply(100);", "   Reply(message_str(123, 100));", StringComparison.Ordinal);
				using FlowchartHarness harness = new FlowchartHarness(script);
				harness.Messages.LoadOnAnotherThread = true;
				DialoguePreviewViewModel preview = harness.CreatePreview();
				Int32 uiThread = Environment.CurrentManagedThreadId;
				List<Int32> threads = new List<Int32>();
				preview.Rows.CollectionChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
				preview.NodeNames.CollectionChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

				Boolean started = await preview.InitializeAsync(harness.Script);

				Assert.True(started);
				Assert.Equal(3, preview.Rows.Count);
				Assert.NotEmpty(threads);
				Assert.All(threads, thread => Assert.Equal(uiThread, thread));
			});
		}

		[Fact]
		public void Initialize_ShowsTheStartNodeWithItsRows()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				DialoguePreviewViewModel preview = await StartAsync(harness);

				Assert.Equal("talk_p_proc", preview.SelectedNode);
				Assert.Equal("Dialogue Preview - dcjohn.ssl", preview.Title);
				Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, preview.NodeNames.ToArray());
				Assert.Equal(3, preview.Rows.Count);

				DialoguePreviewRowViewModel reply = preview.Rows[0];
				Assert.Equal("[Reply]", reply.Target);
				Assert.Equal("Hello there.", reply.Text);
				Assert.Equal("100", reply.LineText);
				Assert.False(reply.IsOption);
				Assert.False(reply.CanFollow);

				DialoguePreviewRowViewModel option = preview.Rows[1];
				Assert.Equal("Node001", option.Target);
				Assert.Equal("● Who are you?", option.Text);
				Assert.Equal("4", option.Iq);
				Assert.True(option.IsOption);
				Assert.True(option.CanFollow);
				Assert.True(option.CanEditMessage);
				Assert.False(preview.CanGoBack);
				Assert.False(preview.CanGoForward);
			});
		}

		[Fact]
		public void Initialize_StartsAtTheNodeThatContainsTheCaret()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.Editor = FakeEditorSurface.Create(harness.Script.TextDocument);
				Int32 line = harness.Script.ProgramInfo!.GetProcedure("Node002")!.StartLine + 1;
				harness.Script.Editor.GoTo(line);

				DialoguePreviewViewModel preview = await StartAsync(harness);

				Assert.Equal("Node002", preview.SelectedNode);
				Assert.Equal("More.", preview.Rows[0].Text);
			});
		}

		[Fact]
		public void Initialize_WithoutDialogue_ReturnsFalse()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				ReplaceScript(harness, "procedure start begin\r\nend\r\n");
				DialoguePreviewViewModel preview = harness.CreatePreview();

				Assert.False(await preview.InitializeAsync(harness.Script));
				Assert.Empty(preview.Rows);
			});
		}

		private static void ReplaceScript(FlowchartHarness harness, String text)
		{
			harness.Script.TextDocument.Text = text;
			harness.Script.ProgramInfo = new FalloutScriptStudio.Core.Parsing.InternalParser().Parse(text, harness.Script.FilePath);
		}

		[Fact]
		public void Follow_ShowsTheTargetNodeAndJumpsToItsCode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);

				await preview.FollowAsync(preview.Rows[1]);

				Assert.Equal("Node001", preview.SelectedNode);
				Assert.True(preview.CanGoBack);
				Assert.Equal("● Who are you?", preview.OptionText);
				Assert.Equal("I am John.", preview.Rows[0].Text);
				Assert.Equal(harness.Script.ProgramInfo!.GetProcedure("Node001")!.StartLine, harness.WorkspaceRecorder.Navigations.Single().Line);
			});
		}

		[Fact]
		public void Follow_WithoutAutoJump_DoesNotMoveTheEditor()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);
				preview.AutoJump = false;

				await preview.FollowAsync(preview.Rows[1]);

				Assert.Equal("Node001", preview.SelectedNode);
				Assert.Empty(harness.WorkspaceRecorder.Navigations);
			});
		}

		[Fact]
		public void Follow_OfAReply_DoesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);

				await preview.FollowAsync(preview.Rows[0]);
				await preview.FollowAsync(null);

				Assert.Equal("talk_p_proc", preview.SelectedNode);
				Assert.Equal(String.Empty, preview.OptionText);
			});
		}

		[Fact]
		public void BackAndForward_WalkTheHistory()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);
				await preview.FollowAsync(preview.Rows[1]);
				await preview.FollowAsync(preview.Rows[1]);
				Assert.Equal("Node002", preview.SelectedNode);

				await preview.GoBackAsync();
				Assert.Equal("Node001", preview.SelectedNode);
				Assert.True(preview.CanGoForward);
				Assert.True(preview.GoForwardCommand.CanExecute(null));

				await preview.GoForwardAsync();
				Assert.Equal("Node002", preview.SelectedNode);
				Assert.False(preview.CanGoForward);

				await preview.GoBackAsync();
				await preview.GoBackAsync();
				Assert.Equal("talk_p_proc", preview.SelectedNode);
				Assert.False(preview.GoBackCommand.CanExecute(null));
			});
		}

		[Fact]
		public void PickingANode_ShowsItAndStartsTheHistoryAgain()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);
				await preview.FollowAsync(preview.Rows[1]);

				preview.SelectedNode = "Node002";
				await WaitAsync(() => preview.Rows.Count > 0 && preview.Rows[0].Text == "More.");

				Assert.Equal("More.", preview.Rows[0].Text);
				Assert.False(preview.CanGoBack);
			});
		}

		[Fact]
		public void FemaleMode_ShowsTheMessagesWithTheOffset()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Messages.SetMessages(MessageDocument.Parse(new[] { "{100}{}{Hello there.}", "{101}{}{Who are you?}", "{1100}{}{Hello, lady.}" }));
				DialoguePreviewViewModel preview = await StartAsync(harness);

				preview.IsFemale = true;
				await WaitAsync(() => preview.Rows[0].Text == "Hello, lady.");

				Assert.Equal("Hello, lady.", preview.Rows[0].Text);
				Assert.Equal("1100", preview.Rows[0].LineText);
				Assert.Equal("● Who are you?", preview.Rows[1].Text);
				Assert.Equal("101", preview.Rows[1].LineText);

				preview.IsFemale = false;
				await WaitAsync(() => preview.Rows[0].Text == "Hello there.");
				Assert.Equal("Hello there.", preview.Rows[0].Text);
			});
		}

		[Fact]
		public void MissingMessages_AreErrorRows()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Messages.SetMessages(MessageDocument.Parse(new[] { "{100}{}{Hello there.}" }));

				DialoguePreviewViewModel preview = await StartAsync(harness);

				Assert.False(preview.Rows[0].IsError);
				Assert.True(preview.Rows[1].IsError);
			});
		}

		[Fact]
		public void JumpToCode_GoesToTheCurrentNodeForTextsAndToTheTargetForOptions()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);

				await preview.JumpToCodeAsync(preview.Rows[0]);
				await preview.JumpToCodeAsync(preview.Rows[1]);
				await preview.JumpToCodeAsync(null);

				Assert.Equal(
					new[]
					{
						harness.Script.ProgramInfo!.GetProcedure("talk_p_proc")!.StartLine,
						harness.Script.ProgramInfo!.GetProcedure("Node001")!.StartLine,
					},
					harness.WorkspaceRecorder.Navigations.Select(n => n.Line).ToArray());
			});
		}

		[Fact]
		public void EditMessage_OpensTheMessageEditorAndShowsTheChangesAfterwards()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);

				await harness.Messages.UpdateMessageTextAsync(FlowchartSamples.MessagePath, 100, "Changed in the editor.");
				await preview.EditMessageAsync(preview.Rows[0]);

				Services.MessageEditor.MessageEditorOpenOptions request = Assert.Single(harness.MessageEditor.Requests);
				Assert.Equal(FlowchartSamples.MessagePath, request.FilePath);
				Assert.Equal(100, request.SelectMessageId);
				Assert.True(request.Modal);
				Assert.Null(request.Target);
				Assert.Equal("Changed in the editor.", preview.Rows[0].Text);
			});
		}

		[Fact]
		public void UpdateScript_PicksUpNewNodesAndTheCurrentNodeStays()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);
				await preview.FollowAsync(preview.Rows[1]);
				harness.Script.TextDocument.Text += "\r\nprocedure Node800 begin\r\n   Reply(100);\r\nend\r\n";
				harness.Script.NeedsParse = true;

				await preview.UpdateScriptAsync();

				Assert.Contains("Node800", preview.NodeNames);
				Assert.Equal("Node001", preview.SelectedNode);
				Assert.False(preview.NeedUpdate);
			});
		}

		[Fact]
		public void Activation_UpdatesAfterTheScriptWasParsed()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = await StartAsync(harness);
				harness.Script.TextDocument.Text += "\r\nprocedure Node800 begin\r\n   Reply(100);\r\nend\r\n";
				harness.Script.NeedsParse = true;

				await preview.OnActivatedAsync();
				Assert.DoesNotContain("Node800", preview.NodeNames);

				harness.WorkspaceRecorder.RaiseParsed(harness.Script);
				Assert.True(preview.NeedUpdate);
				await preview.OnActivatedAsync();

				Assert.Contains("Node800", preview.NodeNames);
				Assert.False(preview.NeedUpdate);
			});
		}
	}
}
