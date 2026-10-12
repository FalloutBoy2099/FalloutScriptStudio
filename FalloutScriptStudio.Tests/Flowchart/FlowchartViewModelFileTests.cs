namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;
	using static FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTestHelpers;

	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartViewModelFileTests
	{
		[Fact]
		public void Closing_WhenTheScriptCouldNotBeParsed_DoesNotOverwriteTheFileWithAnEmptyChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				FlowchartDocument saved = new FlowchartDocument("dcjohn.ssl")
				{
					Nodes = new[] { new FlowchartNodeState("Node001") { X = 123, Y = 45 } },
				};
				await harness.Store.SaveAsync(FcdPath(harness), saved);
				harness.WorkspaceRecorder.ParseSucceeds = false;
				harness.Script.ProgramInfo = null;
				harness.ViewModel.AutoSaveChart = true;

				await harness.LoadAsync();
				harness.ViewModel.OnClosing();

				FlowchartDocument document = await harness.Store.LoadAsync(FcdPath(harness));
				Assert.Equal(123, document.FindNode("Node001")!.X);
				Assert.Empty(harness.Canvas.Nodes);
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
	}
}
