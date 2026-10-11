namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Xunit;

	public class FlowchartViewModelTests
	{
		private static String FcdPath(FlowchartHarness harness)
		{
			return Path.ChangeExtension(harness.Script.FilePath!, ".fcd");
		}

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

				await harness.LoadAsync();

				Assert.Single(harness.Dialogs.Warnings);
				Assert.Equal(5, harness.Canvas.Nodes.Count());
			});
		}

		[Fact]
		public void Load_DamagedFile_ReportsTheErrorAndBuildsANewChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				File.WriteAllText(FcdPath(harness), "this is not xml");

				await harness.LoadAsync();

				Assert.Single(harness.Dialogs.Errors);
				Assert.Equal(5, harness.Canvas.Nodes.Count());
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

		[Fact]
		public void Options_AreReadFromAndWrittenToTheSettings()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Settings.Settings.Flowchart.AutoUpdate = true;
				harness.Settings.Settings.Flowchart.AutoArrange = true;
				harness.Settings.Settings.Flowchart.AutoSaveChart = false;
				harness.Settings.Settings.Flowchart.AutoHideNodes = true;

				FlowchartViewModel second = harness.CreateViewModel();
				Assert.True(second.AutoUpdate);
				Assert.True(second.AutoArrange);
				Assert.False(second.AutoSaveChart);
				Assert.True(second.AutoHideNodes);

				second.AutoUpdate = false;
				second.AutoSaveChart = true;
				second.WithoutExitNode = true;
				second.AutoArrange = false;
				second.AutoHideNodes = false;

				Assert.False(harness.Settings.Settings.Flowchart.AutoUpdate);
				Assert.True(harness.Settings.Settings.Flowchart.AutoSaveChart);
				Assert.True(harness.Settings.Settings.Flowchart.WithoutExitNode);
				Assert.False(harness.Settings.Settings.Flowchart.AutoArrange);
				Assert.False(harness.Settings.Settings.Flowchart.AutoHideNodes);
				second.Dispose();
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void DisplayOptions_ReachTheCanvas()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.HighQuality = false;
				harness.ViewModel.LowDetails = true;
				harness.ViewModel.LinksOnlyForSelection = true;

				Assert.False(harness.Canvas.HighQuality);
				Assert.True(harness.Canvas.LowDetail);
				Assert.True(harness.Canvas.LinksOnlyForSelection);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Zoom_FollowsTheCanvasAndTheSlider()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.ZoomPercent = 50;
				Assert.Equal(0.5, harness.Canvas.Zoom, 3);
				Assert.Equal("50%", harness.ViewModel.ZoomText);

				harness.Canvas.Zoom = 2;
				Assert.Equal(200, harness.ViewModel.ZoomPercent);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void ShowAllCode_ChangesOnlyTheNodesWithoutASettingOfTheirOwn()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.HasUserShowCodeSetting = true;

				harness.ViewModel.ShowAllCode = false;

				Assert.True(harness.Canvas.FindNode("Node001")!.ShowCodeRows);
				Assert.False(harness.Canvas.FindNode("Node002")!.ShowCodeRows);
			});
		}

		[Fact]
		public void Collapse_AndExpand_ReachTheNodesOrTheirRows()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				harness.ViewModel.CollapseAll(false);
				Assert.All(harness.Canvas.Nodes, n => Assert.True(n.IsCollapsed));

				harness.ViewModel.ExpandAll(false);
				Assert.All(harness.Canvas.Nodes, n => Assert.False(n.IsCollapsed));

				harness.ViewModel.ExpandAll(true);
				Assert.True(harness.Canvas.FindNode("Node001")!.Rows[0].IsExpanded);

				harness.ViewModel.CollapseAll(true);
				Assert.False(harness.Canvas.FindNode("Node001")!.Rows[0].IsExpanded);
			});
		}

		[Fact]
		public void Create_AsksBeforeReplacingAnExistingChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.X = 777;
				harness.Dialogs.ConfirmAnswer = false;

				await harness.ViewModel.CreateAsync();
				Assert.Equal(777, harness.Canvas.FindNode("Node001")!.X);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.ViewModel.CreateAsync();
				Assert.NotEqual(777, harness.Canvas.FindNode("Node001")!.X);
				Assert.True(harness.ViewModel.IsHintVisible);
			});
		}

		[Fact]
		public void Hide_Unhide_AndJump()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node1 = harness.Canvas.FindNode("Node001")!;
				DiagramNode node2 = harness.Canvas.FindNode("Node002")!;
				harness.Canvas.Select(node1);
				harness.Canvas.Select(node2, true);

				harness.ViewModel.HideSelection();

				Assert.True(node1.IsHidden);
				Assert.True(node2.IsHidden);
				Assert.Empty(harness.Canvas.SelectedItems);

				harness.ViewModel.PrepareContext(null);
				Assert.Equal(new[] { "Node001", "Node002" }, harness.ViewModel.HiddenNodeNames.ToArray());

				harness.ViewModel.UnhideNode("Node001");
				Assert.False(node1.IsHidden);

				harness.ViewModel.JumpToNode("Node002");
				Assert.False(node2.IsHidden);
				Assert.Same(node2, harness.Canvas.SelectedItem);

				node1.IsHidden = true;
				node2.IsHidden = true;
				node1.X = 0;
				node1.Y = 0;
				harness.ViewModel.UnhideAll();
				Assert.False(node1.IsHidden);
				Assert.False(node2.IsHidden);
				Assert.Equal(20, node1.Y);
			});
		}

		[Fact]
		public void PrepareContext_DescribesTheNodeUnderThePointer()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				harness.Canvas.Select(node);

				harness.ViewModel.PrepareContext(node);

				Assert.Equal("Edit Node: Node001", harness.ViewModel.EditNodeHeader);
				Assert.Equal("Rename Node: Node001", harness.ViewModel.RenameNodeHeader);
				Assert.True(harness.ViewModel.CanEditNode);
				Assert.True(harness.ViewModel.CanRenameNode);
				Assert.True(harness.ViewModel.CanHideSelection);
				Assert.True(harness.ViewModel.CanDeleteSelection);
				Assert.Equal("Jump from: Node001 to", harness.ViewModel.JumpHeader);
				Assert.Equal(new[] { "Node002", "Node999" }, harness.ViewModel.JumpToTargets.Select(t => t.Name).ToArray());
				Assert.Equal(new[] { "talk_p_proc" }, harness.ViewModel.JumpFromTargets.Select(t => t.Name).ToArray());

				harness.ViewModel.PrepareContext(null);
				Assert.False(harness.ViewModel.CanEditNode);
				Assert.Equal("Edit Node", harness.ViewModel.EditNodeHeader);
			});
		}

		[Fact]
		public void PrepareContext_StartNodeAndEditedNodesCannotBeRenamed()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("talk_p_proc"));
				Assert.False(harness.ViewModel.CanRenameNode);

				harness.Editors.Open(harness.Script, "Node001", true);
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				Assert.False(harness.ViewModel.CanRenameNode);
				Assert.True(harness.Canvas.FindNode("Node001")!.IsEditing);
			});
		}

		[Fact]
		public void Delete_RemovesTheProceduresFromTheScriptAsOneUndoableChange()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Canvas.Select(harness.Canvas.FindNode("Node777")!);
				harness.Canvas.Select(harness.Canvas.FindNode("Node002")!, true);

				await harness.ViewModel.DeleteSelectionAsync();

				String text = harness.Script.TextDocument.Text;
				Assert.DoesNotContain("Node777", text, StringComparison.Ordinal);
				Assert.DoesNotContain("procedure Node002", text, StringComparison.Ordinal);
				Assert.Contains("procedure Node001 begin", text, StringComparison.Ordinal);
				Assert.Null(harness.Canvas.FindNode("Node777"));
				Assert.Null(harness.Canvas.FindNode("Node002"));
				Assert.Single(harness.Dialogs.Questions);

				harness.Script.TextDocument.UndoStack.Undo();
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Delete_WhenTheUserDeclines_ChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Canvas.Select(harness.Canvas.FindNode("Node777")!);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.NotNull(harness.Canvas.FindNode("Node777"));
			});
		}

		[Fact]
		public void Delete_NeverDeletesTheStartNodeOrANodeThatIsBeingEdited()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Editors.Open(harness.Script, "Node001", true);
				harness.Canvas.Select(harness.Canvas.FindNode("talk_p_proc")!);
				harness.Canvas.Select(harness.Canvas.FindNode("Node001")!, true);

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Equal(2, harness.Dialogs.Warnings.Count);
				Assert.NotNull(harness.Canvas.FindNode("talk_p_proc"));
				Assert.NotNull(harness.Canvas.FindNode("Node001"));
			});
		}

		[Fact]
		public void Delete_Notes_LeavesTheScriptAlone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.AddNote();
				Assert.Single(harness.Canvas.Items.OfType<DiagramNote>());

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Empty(harness.Canvas.Items.OfType<DiagramNote>());
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Rename_ChangesTheScriptAndKeepsThePositionOfTheNode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				node.X = 321;
				harness.ViewModel.PrepareContext(node);
				harness.WorkspaceRecorder.RenameTo = "Node050";

				await harness.ViewModel.RenameNodeAsync();

				Assert.DoesNotContain("Node001", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.Null(harness.Canvas.FindNode("Node001"));
				Assert.Equal(321, harness.Canvas.FindNode("Node050")!.X);
				Assert.Equal("Node050", harness.Canvas.FindNode("talk_p_proc")!.Rows[1].LinkTargetName);
			});
		}

		[Fact]
		public void Rename_ToANameWithoutNode_IsRefusedAndUndone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				harness.WorkspaceRecorder.RenameTo = "Dialog001";

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Single(harness.Dialogs.Warnings);
				Assert.NotNull(harness.Canvas.FindNode("Node001"));
			});
		}

		[Fact]
		public void Rename_CancelledByTheUser_ChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				harness.WorkspaceRecorder.RenameTo = null;

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Empty(harness.Dialogs.Warnings);
			});
		}

		[Fact]
		public void Rename_TheStartNode_IsNotPossible()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("talk_p_proc"));
				harness.WorkspaceRecorder.RenameTo = "Node000";

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void AddNode_PutsTheNewNodeInTheMiddleOfTheViewAndSelectsIt()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.FlowDialogs.NewNode = async script =>
				{
					NodeCreationResult result = await harness.ScriptEditor.CreateNodeAsync(script, "300", "\r\n   Reply(100);\r\n");
					return result.NodeName;
				};

				await harness.ViewModel.AddNodeAsync();

				Assert.Contains("procedure Node300 begin", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				DiagramNode node = harness.Canvas.FindNode("Node300")!;
				Assert.Same(node, harness.Canvas.SelectedItem);
				Assert.Equal(6, harness.Canvas.Nodes.Count());
			});
		}

		[Fact]
		public void AddNode_Cancelled_AddsNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				await harness.ViewModel.AddNodeAsync();

				Assert.Equal(5, harness.Canvas.Nodes.Count());
			});
		}

		[Fact]
		public void EditNode_OpensTheNodeEditor()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node002"));

				harness.ViewModel.EditNode();

				Assert.True(harness.Editors.IsEditing(harness.Script, "Node002"));
			});
		}

		[Fact]
		public void AppliedNodeCode_UpdatesTheChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				await harness.ScriptEditor.ReplaceNodeCodeAsync(harness.Script, "Node002", "\r\n   Reply(120);\r\n   Reply(121);\r\n   Reply(122);\r\n");

				harness.Editors.RaiseApplied(harness.Script, "Node002");
				for (Int32 i = 0; i < 100 && harness.Canvas.FindNode("Node002")!.Rows.Count != 3; i++)
				{
					await Task.Delay(20);
				}

				Assert.Equal(3, harness.Canvas.FindNode("Node002")!.Rows.Count);
			});
		}

		[Fact]
		public void SaveFlowchart_AsksForTheFileTheFirstTimeAndOpenRestoresIt()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String path = harness.Folder.Combine("saved", "john.fcd");
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				harness.Dialogs.SaveFileAnswer = path;
				harness.Canvas.FindNode("Node001")!.X = 444;
				harness.Canvas.FindNode("Node001")!.Y = 555;

				await harness.ViewModel.SaveFlowchartAsync();

				Assert.True(File.Exists(path));
				Assert.Equal(path, harness.ViewModel.FilePath);
				FlowchartDocument document = await harness.Store.LoadAsync(path);
				Assert.Equal("dcjohn.ssl", document.ScriptName);
				Assert.Equal(444, document.FindNode("Node001")!.X);

				harness.Canvas.FindNode("Node001")!.X = 10;
				harness.Dialogs.OpenFileAnswer = path;
				await harness.ViewModel.OpenFlowchartAsync();
				Assert.Equal(444, harness.Canvas.FindNode("Node001")!.X);
				Assert.Equal(555, harness.Canvas.FindNode("Node001")!.Y);
			});
		}

		[Fact]
		public void SaveFlowchart_Cancelled_WritesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Dialogs.SaveFileAnswer = null;

				await harness.ViewModel.SaveFlowchartAsync();

				Assert.Null(harness.ViewModel.FilePath);
			});
		}

		[Fact]
		public void Open_OfAFileForAnotherScript_KeepsTheCurrentChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String other = harness.Folder.Combine("other.fcd");
				await harness.Store.SaveAsync(other, new FlowchartDocument("other.ssl"));
				harness.Dialogs.OpenFileAnswer = other;

				await harness.ViewModel.OpenFlowchartAsync();

				Assert.Single(harness.Dialogs.Warnings);
				Assert.Equal(5, harness.Canvas.Nodes.Count());
				Assert.Null(harness.ViewModel.FilePath);
			});
		}

		[Fact]
		public void Closing_SavesTheChartWhenAskedAndClosesTheNodeEditors()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.Store.SaveAsync(FcdPath(harness), new FlowchartDocument("dcjohn.ssl"));
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.X = 888;
				harness.ViewModel.AutoSaveChart = true;

				harness.ViewModel.OnClosing();

				FlowchartDocument document = await harness.Store.LoadAsync(FcdPath(harness));
				Assert.Equal(888, document.FindNode("Node001")!.X);
				Assert.Equal(1, harness.Editors.CloseCount);
			});
		}

		[Fact]
		public void Closing_WithoutAutoSave_LeavesTheFileAlone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.Store.SaveAsync(FcdPath(harness), new FlowchartDocument("dcjohn.ssl"));
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.X = 888;
				harness.ViewModel.AutoSaveChart = false;

				harness.ViewModel.OnClosing();

				FlowchartDocument document = await harness.Store.LoadAsync(FcdPath(harness));
				Assert.Null(document.FindNode("Node001"));
			});
		}

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

		[Fact]
		public void FunctionRules_OpenTheConfigurationAndRefreshTheChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				Int32 sources = harness.Messages.SourcesCreated;

				await harness.ViewModel.ShowFunctionRulesAsync();

				Assert.Contains("ShowFunctionRulesAsync", harness.ToolRecorder.Calls);
				Assert.True(harness.Messages.SourcesCreated > sources);
			});
		}

		[Fact]
		public void ShowCommentCode_RebuildsTheNodes()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("   Reply(130);", "   Reply(130);\r\n   // NOption(131, Node001, 4);", StringComparison.Ordinal);
				await harness.LoadAsync();
				Int32 rows = harness.Canvas.FindNode("Node777")!.Rows.Count;

				harness.ViewModel.ShowCommentCode = true;
				await Task.Delay(50);

				Assert.True(harness.Canvas.FindNode("Node777")!.Rows.Count > rows);
			});
		}

		[Fact]
		public void Hint_ShowsAndDismisses()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.ShowHint("Something", 60);
				Assert.True(harness.ViewModel.IsHintVisible);
				Assert.Equal("Something", harness.ViewModel.Hint);

				harness.ViewModel.DismissHint();
				Assert.False(harness.ViewModel.IsHintVisible);
				return Task.CompletedTask;
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
