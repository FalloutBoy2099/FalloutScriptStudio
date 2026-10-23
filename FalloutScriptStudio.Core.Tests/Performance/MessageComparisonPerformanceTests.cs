namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>The comparison of two message files of 30 000 lines each stays under 200 ms (Release; the budget grows in a Debug build like the others).</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class MessageComparisonPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public MessageComparisonPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		/// <summary>Makes a translation of the reference: every third message is translated, one in seven is missing, some are empty, some have another placeholder.</summary>
		private static String Translate(String reference)
		{
			StringBuilder text = new(reference.Length + 4096);
			Int32 index = 0;
			foreach (String line in reference.Split("\r\n"))
			{
				index++;
				if (line.StartsWith("{", StringComparison.Ordinal) || line.StartsWith("\t{", StringComparison.Ordinal))
				{
					if (index % 7 == 0)
					{
						continue;
					}

					if (index % 3 == 0)
					{
						text.Append(line.Replace("Hello traveler", "Bonjour voyageur", StringComparison.Ordinal).Replace("Goodbye", "Au revoir", StringComparison.Ordinal).Replace(".}", " (fr).}", StringComparison.Ordinal)).Append("\r\n");
						continue;
					}
				}

				text.Append(line).Append("\r\n");
			}

			return text.ToString();
		}

		[Fact]
		public async Task Comparing_two_files_of_30000_lines_takes_under_200_milliseconds()
		{
			MessageComparisonService service = new();
			String referenceText = await File.ReadAllTextAsync(this._inputs.MessagePath);
			Assert.True(referenceText.Split('\n').Length >= 30000);
			String targetPath = Path.Combine(this._inputs.Folder, "text", "translated.msg");
			await File.WriteAllTextAsync(targetPath, Translate(referenceText), TextEncodings.Utf8);
			LoadedMessageFile reference = await service.LoadAsync(this._inputs.MessagePath, TextEncodings.Utf8, true);
			LoadedMessageFile target = await service.LoadAsync(targetPath, TextEncodings.Utf8, false);

			MessageComparison last = MessageComparison.Empty;
			this._meter.Measure("MessageFileComparer.Compare 30000 lines", 200, () => last = service.Compare(reference, target));

			Assert.True(last.Rows.Count > 10000);
			Assert.True(last.Count(MessageComparisonStatus.MissingInTarget) > 1000);
			Assert.True(last.Count(MessageComparisonStatus.SameText) > 1000);
			Assert.True(last.Count(MessageComparisonStatus.Ok) > 500);
		}

		[Fact]
		public async Task Reading_and_parsing_a_file_of_30000_lines_for_the_comparison_is_fast()
		{
			MessageComparisonService service = new();

			await this._meter.MeasureAsync("MessageComparisonService.LoadAsync 30000 lines", 250, async () => Assert.True((await service.LoadAsync(this._inputs.MessagePath, TextEncodings.Utf8, false)).Document.Entries.Count > 20000), 3);
		}
	}
}