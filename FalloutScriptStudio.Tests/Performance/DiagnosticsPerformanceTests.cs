namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Globalization;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>The semantic diagnostics in the editor of a 20 000 line script: drawing the underlines and the Quick Fix command must stay interactive.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class DiagnosticsPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;
		private readonly ITestOutputHelper _output;

		public DiagnosticsPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._output = output;
			this._meter = new PerfMeter(output);
		}

		/// <summary>Misspells the calls of some built-in functions, so that the script is full of findings that bring a suggestion.</summary>
		private static String Damage(String text)
		{
			String result = text;
			foreach (String name in new[] { "obj_pid", "obj_name", "debug_msg", "len_array", "get_proto_data", "is_critter", "critter_inven_obj2" })
			{
				result = result.Replace(name + "(", name + "x(", StringComparison.Ordinal);
			}

			return result;
		}

		[Fact]
		public void The_underlines_of_500_findings_in_a_20k_line_script()
		{
			String text = Damage(this._inputs.ValidScript20kText);
			String path = this._inputs.ValidScript20kPath;
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				using EditorSession session = env.StartSession(document);
				ScriptDiagnostic[] found = ScriptDiagnostics.Analyze(text, new ScriptEnvironment { Functions = DiagnosticsFixture.ShippedCatalog }).ToArray();
				this._output.WriteLine("[perf] findings: " + found.Length.ToString(CultureInfo.InvariantCulture));
				Assert.True(found.Length >= 100, "findings: " + found.Length);
				document.LiveDiagnosticsVersion = document.TextDocument.Version;
				document.LiveDiagnostics = found;

				this._meter.Measure("DiagnosticsController.Update 20k lines, " + found.Length.ToString(CultureInfo.InvariantCulture) + " findings", 90, () => session.Diagnostics.Update());

				Assert.Equal(found.Length, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void The_quick_fix_of_a_finding_in_a_20k_line_script_analyses_the_text_again_within_the_budget()
		{
			String damaged = Damage(this._inputs.ValidScript20kText);
			ScriptEnvironment environment = new ScriptEnvironment { Functions = DiagnosticsFixture.ShippedCatalog };
			ScriptDiagnostic first = ScriptDiagnostics.Analyze(damaged, environment).First(d => d.Fix is not null);
			Int32 offset = first.Offset + 1;

			this._meter.Measure("ScriptDiagnostics.Analyze + ScriptQuickFixes.CreateEdits 20k lines", 150, () =>
			{
				ScriptDiagnostic? diagnostic = ScriptDiagnostics.FindAt(ScriptDiagnostics.Analyze(damaged, environment), offset, true);
				Assert.NotNull(diagnostic);
				Assert.Single(ScriptQuickFixes.CreateEdits(damaged, diagnostic!.Fix!));
			}, 3);
		}
	}
}