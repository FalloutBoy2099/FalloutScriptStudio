namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.Analysis;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// The time of the semantic diagnostics on the generated scripts of 5 000 and 20 000 lines (a header with 2 000 macros, 300 procedures,
	/// macros that open blocks), and the proof that they find nothing to report in them: the scripts are plausible code, so an error would
	/// be a false positive. The budget of an analysis that runs after each parse is 100 ms.
	/// </summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class DiagnosticsPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public DiagnosticsPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		private ScriptEnvironment CreateEnvironment(String path, String text, SslTokenStream tokens, IncludeSymbolCache cache)
		{
			IncludeResolver resolver = new IncludeResolver(null, null);
			SortedDictionary<String, ScriptMacro> macros = new MacroCollector(resolver).CollectFromText(text, path, true, null);
			ScriptEnvironmentBuilder builder = new ScriptEnvironmentBuilder(resolver, cache, File.ReadAllText);
			return builder.Build(tokens, path, name => macros.GetValueOrDefault(name), DiagnosticsFixture.ShippedCatalog);
		}

		[Fact]
		public void The_valid_script_of_20000_lines_has_no_diagnostic_of_error_level()
		{
			String path = this._inputs.ValidScript20kPath;
			String text = this._inputs.ValidScript20kText;
			Assert.InRange(text.Count(c => c == '\n'), 19000, 23000);
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(path, text, tokens, new IncludeSymbolCache());

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(tokens, environment);

			Assert.True(environment.IsComplete);
			Assert.DoesNotContain(diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
		}

		[Fact]
		public void The_valid_script_of_5000_lines_has_no_diagnostic_of_error_level()
		{
			String path = this._inputs.ValidScript5kPath;
			String text = this._inputs.ValidScript5kText;
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(path, text, tokens, new IncludeSymbolCache());

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(tokens, environment);

			Assert.DoesNotContain(diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
		}

		[Fact]
		public void The_generated_script_with_undeclared_names_is_reported_without_a_crash_and_stops_at_the_limit()
		{
			String path = this._inputs.Script20kPath;
			String text = this._inputs.Script20kText;
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(path, text, tokens, new IncludeSymbolCache());

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(tokens, environment);

			Assert.True(environment.IsComplete);
			Assert.Equal(new ScriptDiagnosticOptions().MaximumCount, diagnostics.Count);
			Assert.True(diagnostics.Count(d => d.Severity == ScriptDiagnosticSeverity.Error) > 100);
		}

		[Fact]
		public void Analyze_a_20000_line_script_within_the_interactive_budget()
		{
			String path = this._inputs.ValidScript20kPath;
			String text = this._inputs.ValidScript20kText;
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(path, text, tokens, new IncludeSymbolCache());

			this._meter.Measure("ScriptDiagnostics.Analyze 20k lines (tokens ready, header of 2000 macros)", 100, () => ScriptDiagnostics.Analyze(tokens, environment), 7);
		}

		[Fact]
		public void Tokenize_and_analyze_a_20000_line_script_in_one_go()
		{
			String path = this._inputs.ValidScript20kPath;
			String text = this._inputs.ValidScript20kText;
			ScriptEnvironment environment = this.CreateEnvironment(path, text, SslTokenizer.Tokenize(text), new IncludeSymbolCache());

			this._meter.Measure("ScriptDiagnostics.Analyze 20k lines from the text", 150, () => ScriptDiagnostics.Analyze(text, environment), 5);
		}

		[Fact]
		public void Build_the_environment_of_a_script_that_includes_a_header_of_2000_macros()
		{
			String path = this._inputs.Script20kPath;
			String text = this._inputs.Script20kText;
			File.SetLastWriteTimeUtc(this._inputs.HeaderPath, DateTime.UtcNow.AddMinutes(-10));
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			IncludeSymbolCache cache = new IncludeSymbolCache();
			IncludeResolver resolver = new IncludeResolver(null, null);
			ScriptEnvironmentBuilder builder = new ScriptEnvironmentBuilder(resolver, cache, File.ReadAllText);

			this._meter.Measure("ScriptEnvironmentBuilder.Build (header cached)", 20, () => builder.Build(tokens, path, null, DiagnosticsFixture.ShippedCatalog), 7);

			Assert.True(cache.Count >= 1);
		}

		[Fact]
		public void Analyze_a_5000_line_script()
		{
			String path = this._inputs.ValidScript5kPath;
			String text = this._inputs.ValidScript5kText;
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(path, text, tokens, new IncludeSymbolCache());

			this._meter.Measure("ScriptDiagnostics.Analyze 5k lines", 40, () => ScriptDiagnostics.Analyze(tokens, environment), 7);
		}

		[Fact]
		public void A_large_script_with_thousands_of_errors_stays_within_the_budget()
		{
			String text = this._inputs.ValidScript20kText;
			foreach (String name in new[] { "obj_pid", "obj_name", "debug_msg", "len_array", "get_proto_data", "is_critter", "critter_inven_obj2" })
			{
				text = text.Replace(name + "(", name + "x(", StringComparison.Ordinal);
			}

			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			ScriptEnvironment environment = this.CreateEnvironment(this._inputs.ValidScript20kPath, text, tokens, new IncludeSymbolCache());

			this._meter.Measure("ScriptDiagnostics.Analyze 20k lines with a typo everywhere", 250, () => ScriptDiagnostics.Analyze(tokens, environment), 5);

			Assert.True(ScriptDiagnostics.Analyze(tokens, environment).Count(d => d.Code == "SSL1002") > 100);
		}
	}
}