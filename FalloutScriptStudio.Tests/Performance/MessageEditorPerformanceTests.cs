namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.MessageEditor;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Timings of the Message Editor view model on a message file of 30 000 lines.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class MessageEditorPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public MessageEditorPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		[Fact]
		public void Load_and_edit_a_30k_line_message_file()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				MessageEditorViewModel editor = harness.Editor;

				await this._meter.MeasureAsync("MessageEditorViewModel.LoadFileAsync 30k lines", 600, async () => Assert.True(await editor.LoadFileAsync(this._inputs.MessagePath)), 3);
				Assert.True(editor.Rows.Count > 20000);
				this._meter.Note("rows: " + editor.Rows.Count);

				MessageRowViewModel target = editor.Rows.First(r => r.IsMessage && r.Entry.Id > 5000);
				Int32 index = editor.Rows.IndexOf(target);
				harness.Select(index);
				Int32 counter = 0;
				this._meter.Measure("Edit the text of one row (undo snapshot + structure check + search update)", 120, () => target.Text = "Edited text number " + (++counter), 10);
				this._meter.Measure("RunStructureCheck", 250, () => editor.RunStructureCheck(), 5);

				harness.Select(index);
				this._meter.Measure("Insert a blank line below the current row", 120, () => editor.InsertBlankBelowCommand.Execute(null), 5);
				this._meter.Measure("Undo", 200, () => editor.UndoCommand.Execute(null), 5);
				this._meter.Measure("Redo", 200, () => editor.RedoCommand.Execute(null), 3);
				harness.Select(index);
				this._meter.Measure("Add the next numbered line", 20, () => editor.TryAddNextLine(), 3);
				this._meter.Measure("Type a character in the search box (match update)", 60, () => editor.SearchText = "wasteland" + (++counter % 7), 10);
				this._meter.Measure("Switch the filter on and off", 200, () =>
				{
					editor.SearchText = "greeting number 12";
					editor.IsFilterActive = true;
					editor.IsFilterActive = false;
				}, 3);
				editor.SearchText = "wasteland";
				this._meter.Measure("Find next x 20", 150, () =>
				{
					for (Int32 i = 0; i < 20; i++)
					{
						editor.FindNextCommand.Execute(null);
					}
				}, 3);
				this._meter.Note("peak working set: " + PerfMeter.PeakWorkingSetMegabytes.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " MB, managed " + PerfMeter.ManagedMegabytes.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " MB");
			});
		}

		[Fact]
		public void Memory_of_the_undo_history_of_a_30k_line_file()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				MessageEditorViewModel editor = harness.Editor;
				Assert.True(await editor.LoadFileAsync(this._inputs.MessagePath));
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				MessageRowViewModel target = editor.Rows.First(r => r.IsMessage && r.Entry.Id > 5000);
				harness.Select(editor.Rows.IndexOf(target));
				for (Int32 i = 0; i < 30; i++)
				{
					target.Text = "Edited " + i;
				}

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Double after = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				this._meter.Note("managed memory after loading " + before.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MB, after 30 edits " + after.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MB (undo history of " + (after - before).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MB)");
				Assert.True(after - before < 6, "the undo history of 30 edits of a file of " + editor.Rows.Count + " rows needs " + (after - before).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " MB");
			});
		}
	}
}
