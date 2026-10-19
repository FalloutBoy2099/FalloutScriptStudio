namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The analysis runs on text that is being typed: whatever the text is, it must finish and return ranges that lie in the text.</summary>
	public class ScriptDiagnosticsRobustnessTests
	{
		private static IEnumerable<String> Samples()
		{
			String folder = Path.Combine(TestFiles.Samples, "FO2tweaksMk2", "source");
			if (Directory.Exists(folder))
			{
				return Directory.GetFiles(folder, "gl_g_*.ssl").OrderBy(f => f, StringComparer.Ordinal).Select(File.ReadAllText);
			}

			return new[] { String.Join("\r\n", ScriptDiagnosticCase.All.SelectMany(c => c.Lines)) };
		}

		private static void AssertWellFormed(String text, IReadOnlyList<ScriptDiagnostic> diagnostics)
		{
			Int32 previous = -1;
			foreach (ScriptDiagnostic diagnostic in diagnostics)
			{
				Assert.InRange(diagnostic.Offset, 0, text.Length);
				Assert.InRange(diagnostic.End, diagnostic.Offset, text.Length);
				Assert.True(diagnostic.Line >= 1, "line " + diagnostic.Line);
				Assert.True(diagnostic.Column >= 1, "column " + diagnostic.Column);
				Assert.True(diagnostic.Offset >= previous, "not in text order");
				Assert.False(String.IsNullOrEmpty(diagnostic.MessageKey));
				if (diagnostic.Fix is { } fix)
				{
					Assert.InRange(fix.TargetOffset, 0, text.Length);
				}

				previous = diagnostic.Offset;
			}
		}

		[Theory]
		[InlineData("")]
		[InlineData("   \r\n\t\r\n")]
		[InlineData("procedure")]
		[InlineData("procedure start")]
		[InlineData("procedure start(")]
		[InlineData("procedure start(variable")]
		[InlineData("procedure start(variable a,")]
		[InlineData("procedure start begin")]
		[InlineData("procedure start begin if")]
		[InlineData("procedure start begin if (")]
		[InlineData("procedure start begin call")]
		[InlineData("procedure start begin foo(")]
		[InlineData("procedure start begin foo(1,")]
		[InlineData("procedure start begin variable")]
		[InlineData("procedure start begin variable begin")]
		[InlineData("procedure start begin variable a :=")]
		[InlineData("procedure start begin \"unterminated")]
		[InlineData("procedure start begin /* unterminated")]
		[InlineData("procedure start begin // comment")]
		[InlineData("#if")]
		[InlineData("#ifdef A\r\nprocedure p begin")]
		[InlineData("#endif\r\n#else\r\n#elif\r\nprocedure p begin end")]
		[InlineData("#define")]
		[InlineData("#define X(")]
		[InlineData("#include")]
		[InlineData("#include \"")]
		[InlineData("#include <")]
		[InlineData("variable")]
		[InlineData("variable begin")]
		[InlineData("variable begin a;")]
		[InlineData("import")]
		[InlineData("export variable")]
		[InlineData("end end end")]
		[InlineData("begin begin begin")]
		[InlineData(")))((([[[]]]{{{")]
		[InlineData("procedure a begin end procedure a begin end procedure a;")]
		[InlineData("@@@ ### $$$ ~~~ ``` \\\\ ???")]
		public void Text_that_is_not_a_script_is_analysed_without_a_failure(String text)
		{
			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, DiagnosticsFixture.ShippedEnvironment);

			AssertWellFormed(text, diagnostics);
		}

		[Fact]
		public void Every_prefix_of_a_script_is_analysed_without_a_failure_as_when_it_is_typed()
		{
			String text = Samples().First();

			for (Int32 length = 0; length <= text.Length; length += 53)
			{
				String prefix = text.Substring(0, length);
				AssertWellFormed(prefix, ScriptDiagnostics.Analyze(prefix, DiagnosticsFixture.ShippedEnvironment));
			}
		}

		[Fact]
		public void Damaged_copies_of_real_scripts_are_analysed_without_a_failure()
		{
			Random random = new Random(20261013);
			String[] garbage = { "(", ")", "begin", "end", "\"", "/*", "//", "#if", "#endif", ";", ",", "procedure", "variable", "then", "{", "}", "[", "]", ":=", "call ", "\r\n", "#define A(", "\\\r\n" };
			Int32 runs = 0;
			foreach (String sample in Samples())
			{
				for (Int32 i = 0; i < 12; i++)
				{
					StringBuilder text = new StringBuilder(sample);
					for (Int32 edit = 0; edit < 1 + random.Next(4); edit++)
					{
						Int32 at = random.Next(text.Length);
						if (random.Next(3) == 0)
						{
							text.Remove(at, Math.Min(1 + random.Next(120), text.Length - at));
						}
						else
						{
							text.Insert(at, garbage[random.Next(garbage.Length)]);
						}
					}

					String damaged = text.ToString();
					AssertWellFormed(damaged, ScriptDiagnostics.Analyze(damaged, DiagnosticsFixture.ShippedEnvironment));
					runs++;
				}
			}

			Assert.True(runs >= 12);
		}

		[Fact]
		public void Very_deep_and_very_long_inputs_do_not_overflow_the_stack_or_take_long()
		{
			String parentheses = "procedure start begin\r\n\tfoo" + new String('(', 20000) + new String(')', 20000) + ";\r\nend\r\n";
			String blocks = "procedure start begin\r\n" + String.Concat(Enumerable.Repeat("if (1) then begin\r\n", 5000)) + String.Concat(Enumerable.Repeat("end\r\n", 5000)) + "end\r\n";
			String line = "procedure start begin\r\n\tdisplay_msg(" + String.Join(" + ", Enumerable.Repeat("\"a\"", 20000)) + ");\r\nend\r\n";

			foreach (String text in new[] { parentheses, blocks, line })
			{
				System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
				AssertWellFormed(text, ScriptDiagnostics.Analyze(text, DiagnosticsFixture.ShippedEnvironment));
				Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), "took " + watch.Elapsed);
			}
		}

		[Fact]
		public void The_analysis_of_a_text_does_not_change_with_the_instance_or_the_order_of_the_calls()
		{
			String text = String.Join("\r\n", ScriptDiagnosticCase.All.Where(c => c.CompilerRejects).SelectMany(c => c.Lines));

			IReadOnlyList<ScriptDiagnostic> first = ScriptDiagnostics.Analyze(text, DiagnosticsFixture.ShippedEnvironment);
			ScriptDiagnostics.Analyze("procedure x begin end", DiagnosticsFixture.ShippedEnvironment);
			IReadOnlyList<ScriptDiagnostic> second = ScriptDiagnostics.Analyze(text, DiagnosticsFixture.ShippedEnvironment);

			Assert.Equal(first.Select(d => (d.Code, d.Offset, d.Length)), second.Select(d => (d.Code, d.Offset, d.Length)));
		}
	}
}