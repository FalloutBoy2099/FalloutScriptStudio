namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>Runs the table of scripts (<see cref="ScriptDiagnosticCases"/>) through the analysis, without the compiler.</summary>
	public class ScriptDiagnosticTableTests
	{
		public static IEnumerable<Object[]> AllNames => ScriptDiagnosticCases.Names(_ => true);

		private static ScriptDiagnosticCase Find(String name)
		{
			return ScriptDiagnosticCase.All.Single(c => c.Name == name);
		}

		[Theory]
		[MemberData(nameof(AllNames))]
		public void The_analysis_reports_exactly_the_expected_rules_with_CRLF(String name)
		{
			ScriptDiagnosticCase item = Find(name);

			IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(item.Crlf);

			Assert.Equal(item.Expected.OrderBy(c => c, StringComparer.Ordinal), DiagnosticsFixture.Codes(diagnostics));
			if (!item.CompilerRejects)
			{
				Assert.DoesNotContain(diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
			}
		}

		[Theory]
		[MemberData(nameof(AllNames))]
		public void The_analysis_does_not_depend_on_the_line_breaks(String name)
		{
			ScriptDiagnosticCase item = Find(name);

			IReadOnlyList<ScriptDiagnostic> crlf = DiagnosticsFixture.Analyze(item.Crlf);
			IReadOnlyList<ScriptDiagnostic> lf = DiagnosticsFixture.Analyze(item.Lf);

			Assert.Equal(crlf.Select(d => (d.Code, d.Line, d.Column, d.Length)), lf.Select(d => (d.Code, d.Line, d.Column, d.Length)));
		}

		[Fact]
		public void Every_rule_of_the_language_has_a_script_that_the_compiler_rejects_and_one_that_it_accepts_or_none_is_needed()
		{
			String[] covered = ScriptDiagnosticCase.All.SelectMany(c => c.Expected).Distinct(StringComparer.Ordinal).ToArray();

			String[] missing = ScriptDiagnosticCodes.All.Select(r => r.Code).Where(code => !covered.Contains(code, StringComparer.Ordinal)).ToArray();

			// DeprecatedFunction (SSL2003) needs a description marked as deprecated; the shipped files have none, a unit test covers it.
			Assert.Equal(new[] { "SSL2003" }, missing);
		}

		[Fact]
		public void The_table_has_cases_that_must_not_be_reported_for_most_rules()
		{
			Assert.True(ScriptDiagnosticCase.All.Count(c => !c.CompilerRejects) >= 40, "accepted cases: " + ScriptDiagnosticCase.All.Count(c => !c.CompilerRejects));
			Assert.True(ScriptDiagnosticCase.All.Count(c => c.CompilerRejects) >= 60, "rejected cases: " + ScriptDiagnosticCase.All.Count(c => c.CompilerRejects));
		}
	}
}