namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Workspace.Formatting;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Format Project over the generated project folder of 5 000 files: the preview, its memory, the cancellation, and the formatting of 500 files.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class FormatProjectPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public FormatProjectPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		private static void UseTheEverythingStyle(ShellHarness harness, String folder)
		{
			harness.Settings.Update(settings =>
			{
				settings.Paths.SolutionFolder = folder;
				settings.Editor.Formatting.Reindent = true;
				settings.Editor.Formatting.TrimTrailingWhitespace = true;
				settings.Editor.Formatting.EnsureFinalNewline = true;
				settings.Editor.Formatting.BlankLinesBetweenProcedures = 1;
				settings.Editor.Formatting.MaxConsecutiveBlankLines = 1;
			});
		}

		[Fact]
		public void The_preview_of_5000_files_keeps_the_changes_not_the_texts()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				UseTheEverythingStyle(harness, this._inputs.ProjectFolder);
				ProjectFormatService service = new ProjectFormatService(harness.Settings, harness.Workspace, null, harness.Problems);
				GC.Collect();
				Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				Stopwatch watch = Stopwatch.StartNew();

				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);

				watch.Stop();
				GC.Collect();
				Double after = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
				this._meter.Note("Format Project preview of " + preview.Examined + " files, " + preview.Changes.Count + " would change: " + watch.ElapsedMilliseconds + " ms, managed memory grows by " + (after - before).ToString("F1", CultureInfo.InvariantCulture) + " MB, peak working set " + PerfMeter.PeakWorkingSetMegabytes.ToString("F0", CultureInfo.InvariantCulture) + " MB");
				Assert.True(preview.Examined >= 4000, "Only " + preview.Examined + " files were examined.");
				Assert.True(preview.Changes.Count > 0);
				Assert.True(after - before < 60, "The preview holds " + (after - before) + " MB.");
				Assert.True(watch.ElapsedMilliseconds < 20000, "The preview took " + watch.ElapsedMilliseconds + " ms.");
				GC.KeepAlive(preview);
			});
		}

		[Fact]
		public void A_preview_cancelled_stops_quickly()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				UseTheEverythingStyle(harness, this._inputs.ProjectFolder);
				ProjectFormatService service = new ProjectFormatService(harness.Settings, harness.Workspace, null, harness.Problems);
				using CancellationTokenSource cancellation = new CancellationTokenSource();
				System.Threading.Tasks.Task<ProjectFormatPreview> running = service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, ProjectFormatRequest.DefaultMasks), null, cancellation.Token);
				await System.Threading.Tasks.Task.Delay(20);
				Stopwatch watch = Stopwatch.StartNew();

				cancellation.Cancel();
				ProjectFormatPreview preview = await running;

				watch.Stop();
				this._meter.Note("Format Project preview cancelled after 20 ms: stopped " + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms after the request");
				Assert.True(watch.Elapsed.TotalMilliseconds < 2000);
				Assert.True(preview.Cancelled || preview.Examined > 0);
			});
		}

		[Fact]
		public void Formatting_500_files_writes_them_atomically_and_a_second_run_finds_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				List<String> files = Directory.EnumerateFiles(this._inputs.ProjectFolder, "*.ssl", SearchOption.AllDirectories).Take(500).ToList();
				String folder = harness.Folder.Combine("copy");
				Directory.CreateDirectory(folder);
				for (Int32 i = 0; i < files.Count; i++)
				{
					File.Copy(files[i], Path.Combine(folder, i.ToString(CultureInfo.InvariantCulture) + "_" + Path.GetFileName(files[i])));
				}

				UseTheEverythingStyle(harness, folder);
				ProjectFormatService service = new ProjectFormatService(harness.Settings, harness.Workspace, null, harness.Problems);
				ProjectFormatPreview preview = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);
				Stopwatch watch = Stopwatch.StartNew();

				ProjectFormatResult result = await service.ApplyAsync(preview.Changes, null, CancellationToken.None);

				watch.Stop();
				ProjectFormatPreview again = await service.PreviewAsync(new ProjectFormatRequest(FormatScope.ProjectFolder, ProjectFormatRequest.DefaultMasks), null, CancellationToken.None);
				this._meter.Note("Format Project applied to " + result.Formatted + " of " + preview.Examined + " files: " + watch.ElapsedMilliseconds + " ms; a second preview finds " + again.Changes.Count + " files");
				Assert.Equal(preview.Changes.Count, result.Formatted);
				Assert.Equal(0, result.Skipped);
				Assert.Empty(again.Changes);
				Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp*", SearchOption.AllDirectories));
				Assert.True(watch.ElapsedMilliseconds < 15000, "Applying took " + watch.ElapsedMilliseconds + " ms.");
			});
		}
	}
}