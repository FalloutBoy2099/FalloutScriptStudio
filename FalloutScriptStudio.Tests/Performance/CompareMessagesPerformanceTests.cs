namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Tests.ToolWindows;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>The window "Compare Message Files" on two message files of 30 000 lines: opening it, switching the filters and typing in the filter box.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class CompareMessagesPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public CompareMessagesPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		[Fact]
		public async Task Open_and_filter_a_comparison_of_30k_line_message_files()
		{
			String english = Path.Combine(this._inputs.Folder, "compare", "text", "english", "dialog", "big.msg");
			String french = Path.Combine(this._inputs.Folder, "compare", "text", "french", "dialog", "big.msg");
			Directory.CreateDirectory(Path.GetDirectoryName(english)!);
			Directory.CreateDirectory(Path.GetDirectoryName(french)!);
			String text = PerformanceInputs.BuildMessages(30000);
			await File.WriteAllTextAsync(english, text, TextEncodings.Utf8);
			await File.WriteAllTextAsync(french, String.Join("\r\n", text.Split("\r\n").Where((line, index) => index % 7 != 3).Select(line => line.Replace("}", " (fr)}", StringComparison.Ordinal))), TextEncodings.Utf8);

			FakeSettingsService settings = new(@"C:\ss");
			CompareMessagesViewModel viewModel = new(new MessageComparisonService(), settings, new FalloutScriptStudio.Tests.ToolWindows.FakeDialogService(), new FakeShellService(), new FakeMessageEditorService(), new ProblemService());

			await this._meter.MeasureAsync("CompareMessagesViewModel.InitializeAsync 30k lines", 800, async () => await viewModel.InitializeAsync(new CompareMessagesRequest(english)), 3);

			this._meter.Note("rows: " + viewModel.Rows.Count + ", targets: " + viewModel.Targets.Count);
			Assert.Single(viewModel.Targets);
			Assert.True(viewModel.SelectedTarget!.Comparison.Rows.Count > 15000);
			CompareFilterViewModel ok = viewModel.Filters.Single(filter => filter.Status == MessageComparisonStatus.Ok);
			this._meter.Measure("Show the OK lines (about 15 000 rows) and hide them again", 150, () =>
			{
				ok.IsChecked = true;
				ok.IsChecked = false;
			}, 5);
			Int32 counter = 0;
			this._meter.Measure("Type a character in the filter box", 100, () => viewModel.FilterText = "number " + (++counter % 40), 10);
			viewModel.FilterText = String.Empty;
			this._meter.Measure("Only lines with issues", 100, () =>
			{
				viewModel.IssuesOnly = true;
				viewModel.IssuesOnly = false;
			}, 5);
		}
	}
}