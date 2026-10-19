namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.Tests.Formatting;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// The formatter over the generated scripts of 5 000 and 20 000 lines (real procedures of the FO2tweaksMk2 sample, a header of 2 000
	/// macros, nested blocks, strings and comments): formatting twice is formatting once, only white space changes, and a whole document
	/// is formatted well inside the budget of an interactive command.
	/// </summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class FormatPerformanceTests
	{
		private static readonly CodeFormatter _formatter = new CodeFormatter();

		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public FormatPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		public static IEnumerable<Object[]> SetNames()
		{
			return FormatSamples.SetNames();
		}

		[Theory]
		[MemberData(nameof(SetNames))]
		public void Formatting_a_generated_script_changes_only_white_space_and_is_stable(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];
			foreach (String text in new[] { this._inputs.Script5kText, this._inputs.ValidScript5kText, this._inputs.HeaderText })
			{
				foreach (String input in new[] { text, FormatSamples.Mutate(text, 21) })
				{
					String once = _formatter.Format(input, options);

					Assert.Equal(FormatSamples.Signature(input), FormatSamples.Signature(once));
					Assert.Equal(once, _formatter.Format(once, options));
				}
			}
		}

		[Theory]
		[InlineData("default")]
		[InlineData("everything")]
		public void Formatting_a_script_of_20000_lines_is_stable(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];
			String text = this._inputs.Script20kText;
			Assert.InRange(text.Count(c => c == '\n'), 19000, 23000);

			String once = _formatter.Format(text, options);

			Assert.Equal(FormatSamples.Signature(text), FormatSamples.Signature(once));
			Assert.Equal(once, _formatter.Format(once, options));
		}

		[Fact]
		public void The_default_options_format_20000_lines_within_the_interactive_budget()
		{
			String text = this._inputs.Script20kText;

			this._meter.Measure("Format 20k lines, default options", 400, () => _formatter.Format(text), 5);
		}

		[Fact]
		public void Every_option_on_formats_20000_lines_within_a_second()
		{
			String text = this._inputs.Script20kText;
			FormatOptions options = FormatSamples.Sets["everything"];

			this._meter.Measure("Format 20k lines, every option", 1500, () => _formatter.Format(text, options), 3);
		}

		[Fact]
		public void The_lines_that_change_are_found_in_a_document_of_20000_lines()
		{
			String text = this._inputs.Script20kText;
			FormatOptions options = FormatSamples.Sets["k&r"];
			String formatted = _formatter.Format(text, options);

			IReadOnlyList<TextEdit> edits = null!;
			this._meter.Measure("LineDiff 20k lines, K&R style", 600, () => edits = LineDiff.Compute(text, formatted), 3);

			Assert.Equal(formatted, TextEditApplier.Apply(text, edits));
		}
	}
}