namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// Every rule of <see cref="ScriptDiagnostics"/> against the real <c>compile.exe</c>: the scripts of the table
	/// (<see cref="ScriptDiagnosticCases"/>) are compiled, the verdict of the compiler must be the one the table states, and the analysis must
	/// report the script exactly when the compiler rejects it (and never with an error when the compiler accepts it). Skipped when the
	/// compiler is absent, see <see cref="SslcTools"/>. The table is split between two classes so that xunit runs them in parallel.
	/// </summary>
	public abstract class RealScriptDiagnosticsTestsBase
	{
		protected RealScriptDiagnosticsTestsBase(RealCompilerFixture compiler)
		{
			this.Compiler = compiler;
		}

		protected RealCompilerFixture Compiler { get; }

		protected static IEnumerable<Object[]> Shard(Int32 index)
		{
			return ScriptDiagnosticCase.All.Where((c, i) => c.CanCompile && i % 2 == index).Select(c => new Object[] { c.Name });
		}

		protected async Task CheckAsync(String name)
		{
			ScriptDiagnosticCase item = ScriptDiagnosticCase.All.Single(c => c.Name == name);

			CompilationResult result = await this.Compiler.CompileAsync(item.Crlf);
			IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(item.Crlf);

			Assert.True(item.CompilerRejects == !result.Success, "The table says the compiler " + (item.CompilerRejects ? "rejects" : "accepts") + " the script, but: " + result.Output);
			Assert.Equal(item.Expected.OrderBy(c => c, StringComparer.Ordinal), DiagnosticsFixture.Codes(diagnostics));
			if (result.Success)
			{
				Assert.DoesNotContain(diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
			}
			else
			{
				Assert.Contains(diagnostics, d => d.Severity >= ScriptDiagnosticSeverity.Warning);
			}
		}
	}

	/// <summary>The first half of the table against the real compiler.</summary>
	public sealed class RealScriptDiagnosticsTestsFirst : RealScriptDiagnosticsTestsBase, IClassFixture<RealCompilerFixture>
	{
		public RealScriptDiagnosticsTestsFirst(RealCompilerFixture compiler)
			: base(compiler)
		{
		}

		public static IEnumerable<Object[]> Names => Shard(0);

		[RealToolTheory(RealTools.Compiler)]
		[MemberData(nameof(Names))]
		public Task The_analysis_agrees_with_the_compiler(String name)
		{
			return this.CheckAsync(name);
		}
	}

	/// <summary>The second half of the table against the real compiler.</summary>
	public sealed class RealScriptDiagnosticsTestsSecond : RealScriptDiagnosticsTestsBase, IClassFixture<RealCompilerFixture>
	{
		public RealScriptDiagnosticsTestsSecond(RealCompilerFixture compiler)
			: base(compiler)
		{
		}

		public static IEnumerable<Object[]> Names => Shard(1);

		[RealToolTheory(RealTools.Compiler)]
		[MemberData(nameof(Names))]
		public Task The_analysis_agrees_with_the_compiler(String name)
		{
			return this.CheckAsync(name);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task A_fix_of_a_diagnostic_makes_the_compiler_accept_the_script()
		{
			(String Text, String Code)[] scripts =
			{
				("procedure start begin\r\n\tvariable x;\r\n\tx := foo(1);\r\nend\r\nprocedure foo(variable a) begin return a; end\r\n", "SSL1004"),
				("procedure start begin\r\n\tdisplay_msgg(\"a\");\r\nend\r\n", "SSL1002"),
				("variable a;\r\nvariable a;\r\nprocedure start begin end\r\n", "SSL1009"),
				("procedure foo begin end\r\nprocedure foo;\r\nprocedure start begin end\r\n", "SSL1008"),
			};

			foreach ((String text, String code) in scripts)
			{
				IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(text);
				ScriptDiagnostic diagnostic = Assert.Single(diagnostics, d => d.Code == code);
				Assert.NotNull(diagnostic.Fix);
				String fixedText = FalloutScriptStudio.Core.TextTools.TextEditApplier.Apply(text, ScriptQuickFixes.CreateEdits(text, diagnostic.Fix!));

				CompilationResult result = await this.Compiler.CompileAsync(fixedText);

				Assert.True(result.Success, code + " fixed to:\r\n" + fixedText + "\r\n" + result.Output);
				Assert.DoesNotContain(DiagnosticsFixture.Analyze(fixedText), d => d.Severity == ScriptDiagnosticSeverity.Error);
			}
		}
	}
}