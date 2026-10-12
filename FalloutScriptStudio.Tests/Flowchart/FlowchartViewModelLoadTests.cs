namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;
	using static FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartViewModelLoadTests
	{
		private static String[] Names(FlowchartHarness harness)
		{
			return harness.Canvas.Nodes.Select(n => n.Name).ToArray();
		}

		[Fact]
		public void Load_WithoutAFile_BuildsTheNodesAndTheirLinks()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				await harness.LoadAsync();

				Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, Names(harness));
				Assert.Equal(5, harness.Canvas.Links.Count);
				Assert.Equal(25, harness.Canvas.FindNode("talk_p_proc")!.X);
				Assert.True(harness.Canvas.FindNode("Node001")!.Height > 0);
				Assert.False(harness.ViewModel.NeedUpdate);
			});
		}

		[Fact]
		public void Load_WithoutExitNode_LeavesNode999Out()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(withoutExitNode: true);

				await harness.LoadAsync();

				Assert.DoesNotContain("Node999", Names(harness));
			});
		}

		[Fact]
		public void Load_WithAutoHide_HidesTheUnusedNodes()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.ViewModel.AutoHideNodes = true;

				await harness.LoadAsync();

				Assert.True(harness.Canvas.FindNode("Node777")!.IsHidden);
				Assert.False(harness.Canvas.FindNode("Node001")!.IsHidden);
			});
		}

		[Fact]
		public void Load_WithAutoArrange_ArrangesTheNodes()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.ViewModel.AutoArrange = true;

				await harness.LoadAsync();

				Assert.True(harness.Canvas.FindNode("Node001")!.X > harness.Canvas.FindNode("talk_p_proc")!.X);
			});
		}

		[Fact]
		public void Load_WithAFileNextToTheScript_RestoresTheSavedChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				FlowchartDocument saved = new FlowchartDocument("dcjohn.ssl")
				{
					Zoom = 0.5f,
					Nodes = new[]
					{
						new FlowchartNodeState("Node002") { X = 20, Y = 30, Width = 300, Hidden = true },
						new FlowchartNodeState("talk_p_proc") { X = 300, Y = 400, Width = 280 },
						new FlowchartNodeState("NodeGone") { X = 1, Y = 1, Width = 100 },
					},
					Notes = new[] { new FlowchartNoteState("remember") { X = 50, Y = 60, Width = 220, Height = 80 } },
				};
				await harness.Store.SaveAsync(FcdPath(harness), saved);

				await harness.LoadAsync();

				Assert.Equal(300, harness.Canvas.FindNode("talk_p_proc")!.X);
				Assert.Equal(400, harness.Canvas.FindNode("talk_p_proc")!.Y);
				Assert.True(harness.Canvas.FindNode("Node002")!.IsHidden);
				Assert.Null(harness.Canvas.FindNode("NodeGone"));
				Assert.Equal("remember", harness.Canvas.Items.OfType<DiagramNote>().Single().Text);
				Assert.Equal(50, harness.ViewModel.ZoomPercent);
				Assert.Contains(harness.Dialogs.Infos, m => m.StartsWith("Added new nodes", StringComparison.Ordinal) && m.Contains("Node001", StringComparison.Ordinal));
				Assert.Contains(harness.Dialogs.Infos, m => m.Contains("NodeGone", StringComparison.Ordinal));
				Assert.Equal(FcdPath(harness), harness.ViewModel.FilePath);
			});
		}

		[Fact]
		public void Load_FileOfAnotherScript_WarnsAndBuildsANewChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.Store.SaveAsync(FcdPath(harness), new FlowchartDocument("other.ssl"));
				harness.ViewModel.AutoSaveChart = true;

				await harness.LoadAsync();

				Assert.Single(harness.Dialogs.Warnings);
				Assert.Equal(5, harness.Canvas.Nodes.Count());
				Assert.Null(harness.ViewModel.FilePath);

				harness.ViewModel.OnClosing();

				Assert.Equal("other.ssl", (await harness.Store.LoadAsync(FcdPath(harness))).ScriptName);
			});
		}

		[Fact]
		public void Load_DamagedFile_ReportsTheErrorAndBuildsANewChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				File.WriteAllText(FcdPath(harness), "this is not xml");
				harness.ViewModel.AutoSaveChart = true;

				await harness.LoadAsync();

				Assert.Single(harness.Dialogs.Errors);
				Assert.Equal(5, harness.Canvas.Nodes.Count());
				Assert.Null(harness.ViewModel.FilePath);

				harness.ViewModel.OnClosing();

				Assert.Equal("this is not xml", File.ReadAllText(FcdPath(harness)));
			});
		}

		[Fact]
		public void Update_KeepsTheLayoutAndShowsTheChangedScript()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				node.X = 500;
				node.Y = 260;
				node.Rows[1].IsExpanded = true;
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("Reply(120);", "Reply(120);\r\n   Reply(121);", StringComparison.Ordinal);

				await harness.ViewModel.UpdateAsync();

				DiagramNode updated = harness.Canvas.FindNode("Node001")!;
				Assert.NotSame(node, updated);
				Assert.Equal(500, updated.X);
				Assert.Equal(260, updated.Y);
				Assert.True(updated.Rows[1].IsExpanded);
				Assert.Equal(3, harness.Canvas.FindNode("Node002")!.Rows.Count);
			});
		}

		[Fact]
		public void Update_RemovesNodesThatLeftTheScript()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("procedure Node777", "procedure Other777", StringComparison.Ordinal);

				await harness.ViewModel.UpdateAsync();

				Assert.Null(harness.Canvas.FindNode("Node777"));
			});
		}

		[Fact]
		public void Activate_WhenTheScriptWasParsed_TellsTheUserOrUpdates()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				Int32 sources = harness.Messages.SourcesCreated;

				harness.WorkspaceRecorder.RaiseParsed(harness.Script);
				Assert.True(harness.ViewModel.NeedUpdate);
				await harness.ViewModel.OnActivatedAsync();
				Assert.True(harness.ViewModel.IsHintVisible);
				Assert.Equal(sources, harness.Messages.SourcesCreated);

				harness.ViewModel.AutoUpdate = true;
				await harness.ViewModel.OnActivatedAsync();
				Assert.False(harness.ViewModel.NeedUpdate);
				Assert.True(harness.Messages.SourcesCreated > sources);
			});
		}

		[Fact]
		public void ParsingOtherDocuments_DoesNotMarkTheChartOutdated()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				harness.WorkspaceRecorder.RaiseParsed(FlowchartSamples.CreateDocument(path: @"C:\other.ssl"));

				Assert.False(harness.ViewModel.NeedUpdate);
			});
		}

		// The internal parser does not collect macros, so they are described the way the native parser would report them.
		private static FlowchartHarness CreateHarnessWithOtherMessageFile()
		{
			String script = FlowchartSamples.Script().Replace("Reply(120);", "Reply(mstr(120));", StringComparison.Ordinal).Replace("Reply(130);", "Reply(mstr(130));", StringComparison.Ordinal);
			FlowchartHarness harness = new FlowchartHarness(script);
			String path = harness.Script.FilePath!;
			SortedDictionary<String, ScriptMacro> macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal)
			{
				["NAME"] = new ScriptMacro("NAME", "NAME", "123", path, 1, null),
				["mstr"] = new ScriptMacro("mstr", "mstr(x)", "message_str(NAME, x)", path, 2, null),
			};
			harness.Script.ProgramInfo = new ProgramInfo(harness.Script.ProgramInfo!.Procedures, Array.Empty<ScriptVariable>(), macros, null, path);
			return harness;
		}

		[Fact]
		public void Load_ReportsEachMissingMessageFileOnce()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = CreateHarnessWithOtherMessageFile();
				harness.Messages.MissingFiles[123] = @"C:\mod\text\english\dialog\other.msg";

				await harness.LoadAsync();

				String error = Assert.Single(harness.Dialogs.Errors);
				Assert.Contains("other.msg", error, StringComparison.Ordinal);
				Assert.Contains("not found", error, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Load_WithAllMessageFilesPresent_ReportsNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = CreateHarnessWithOtherMessageFile();

				await harness.LoadAsync();

				Assert.Empty(harness.Dialogs.Errors);
			});
		}

		[Fact]
		public void Load_FromAFlowchartFile_ReportsMissingMessageFilesToo()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = CreateHarnessWithOtherMessageFile();
				await harness.Store.SaveAsync(FcdPath(harness), new FlowchartDocument("dcjohn.ssl") { Nodes = new[] { new FlowchartNodeState("Node002") { X = 20, Y = 30, Width = 300 } } });
				harness.Messages.MissingFiles[123] = @"C:\mod\text\english\dialog\other.msg";

				await harness.LoadAsync();

				Assert.Contains("other.msg", Assert.Single(harness.Dialogs.Errors), StringComparison.Ordinal);
			});
		}
	}
}
