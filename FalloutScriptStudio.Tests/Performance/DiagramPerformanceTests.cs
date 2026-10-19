namespace FalloutScriptStudio.Tests.Performance
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Timings of the dialogue flowchart (200 nodes): building the nodes, arranging, drawing, zooming, hit testing and dragging.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class DiagramPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public DiagramPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		[Fact]
		public void Flowchart_phases_of_200_nodes()
		{
			String script = File.ReadAllText(this._inputs.DialogueScriptPath);
			MessageDocument messages = MessageDocument.Parse(File.ReadAllLines(this._inputs.DialogueMessagePath));
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(script);
				harness.Messages.SetMessages(messages);
				DiagramCanvas canvas = new DiagramCanvas();
				Window window = OffscreenWindow.Show(canvas, 1400, 900);
				FalloutScriptStudio.Core.Dialogue.FlowchartModel model = await harness.Builder.BuildAsync(script, harness.Script.ProgramInfo!, new FalloutScriptStudio.Core.Dialogue.DialogueMessageSource(messages, "x.msg"), new FalloutScriptStudio.Core.Dialogue.FlowchartBuildOptions());
				for (Int32 round = 0; round < 2; round++)
				{
					canvas.Clear();
					System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
					System.Collections.Generic.List<DiagramNode> nodes = model.Nodes.Select(n => FalloutScriptStudio.Services.ScriptTools.Flowchart.FlowchartDiagramMapper.CreateNode(n, true)).ToList();
					Double create = watch.Elapsed.TotalMilliseconds;
					watch.Restart();
					Int32 x = 0;
					foreach (DiagramNode node in nodes)
					{
						node.X = 25 + (x * 30);
						node.Y = 25 + (x * 10);
						x++;
						canvas.AddItem(node);
					}

					Double add = watch.Elapsed.TotalMilliseconds;
					watch.Restart();
					canvas.AutoArrange();
					Double arrange = watch.Elapsed.TotalMilliseconds;
					watch.Restart();
					canvas.Refresh();
					Double refresh = watch.Elapsed.TotalMilliseconds;
					watch.Restart();
					UiThread.Flush();
					Double flush = watch.Elapsed.TotalMilliseconds;
					this._meter.Note("flowchart phases: create nodes " + create.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms, AddItem x" + nodes.Count + " (layout) " + add.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms, arrange " + arrange.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms, first draw " + refresh.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms, dispatcher flush " + flush.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms");
					Assert.True(create + add + arrange + refresh < 4000, "the flowchart of 200 nodes took " + (create + add + arrange + refresh).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
				}

				canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				window.Content = null;
				window.Close();
			});
		}

		[Fact]
		public void Flowchart_of_200_nodes()
		{
			String script = File.ReadAllText(this._inputs.DialogueScriptPath);
			MessageDocument messages = MessageDocument.Parse(File.ReadAllLines(this._inputs.DialogueMessagePath));
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness(script);
				harness.Messages.SetMessages(messages);
				Window window = OffscreenWindow.Show(harness.Canvas, 1400, 900);

				await this._meter.MeasureAsync("FlowchartViewModel.LoadAsync 200 nodes (model, nodes, canvas, arrange)", 4000, async () => await harness.LoadAsync(), 2);
				DiagramCanvas canvas = harness.Canvas;
				Assert.True(canvas.Nodes.Count() >= PerformanceInputs.DialogueNodeCount);
				this._meter.Note("nodes: " + canvas.Nodes.Count() + ", links: " + canvas.Links.Count);

				this._meter.Measure("DiagramCanvas.AutoArrange 200 nodes", 100, () => canvas.AutoArrange(), 3);
				canvas.Refresh();
				DiagramNode first = canvas.Nodes.First();

				this._meter.Measure("Select all + draw (every layer re-recorded)", 400, () =>
				{
					canvas.SelectAll();
					canvas.Refresh();
					canvas.ClearSelection();
					canvas.Refresh();
				}, 5);

				this._meter.Measure("Expand/collapse all nodes + draw", 2500, () =>
				{
					canvas.CollapseAllNodes();
					canvas.Refresh();
					canvas.ExpandAllNodes();
					canvas.Refresh();
				}, 3);

				Double zoom = 1.0;
				this._meter.Measure("Zoom step + draw x10", 250, () =>
				{
					for (Int32 i = 0; i < 10; i++)
					{
						zoom = zoom > 0.5 ? zoom / 1.1 : 1.0;
						canvas.ZoomAt(new Point(500, 400), zoom);
						canvas.Refresh();
					}
				}, 3);
				canvas.Zoom = 0.4;
				canvas.Refresh();

				Rect bounds = canvas.GetContentBounds();
				this._meter.Measure("HitTestWorld x2000", 150, () =>
				{
					Random random = new Random(7);
					for (Int32 i = 0; i < 2000; i++)
					{
						canvas.HitTestWorld(new Point(bounds.X + (random.NextDouble() * bounds.Width), bounds.Y + (random.NextDouble() * bounds.Height)));
					}
				}, 5);

				canvas.Select(first);
				this._meter.Measure("Drag the selected node x50 (links + upper layer re-recorded)", 800, () =>
				{
					for (Int32 i = 0; i < 50; i++)
					{
						first.X += 3;
						first.Y += 2;
						canvas.Refresh();
					}
				}, 3);

				this._meter.Measure("Scroll the view x50", 100, () =>
				{
					for (Int32 i = 0; i < 50; i++)
					{
						canvas.ScrollToOffset(i * 20, i * 12);
					}
				}, 3);

				this._meter.Measure("Jump to a node (select + scroll)", 30, () =>
				{
					canvas.ClearSelection();
					canvas.Select(canvas.Nodes.ElementAt(120));
					canvas.ScrollToItem(canvas.Nodes.ElementAt(120));
				}, 5);

				canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
				window.Content = null;
				window.Close();
			});
		}
	}
}