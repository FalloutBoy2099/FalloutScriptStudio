namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// The checks that compare a call with the description of a function (<c>SSL2xxx</c>) against the real compiler, and the whole analysis against
	/// the real scripts of the author's mods. Skipped when the compiler or the samples are absent, see <see cref="SslcTools"/>.
	/// </summary>
	public sealed class RealFunctionCatalogTests : IClassFixture<RealCompilerFixture>
	{
		/// <summary>
		/// Functions whose description in <c>opcodes.txt</c> or <c>functions.ini</c> differs from what sslc accepts (a macro of the sfall headers
		/// documented under the name of the opcode it expands to, or an argument that the description lists and the compiler does not take).
		/// The checks are warnings for this reason; the list must stay short, fix the description rather than extending it.
		/// </summary>
		private static readonly HashSet<String> _knownDiscrepancies = new(StringComparer.OrdinalIgnoreCase)
		{
			"attack",
			"inven_unwield",
			"reg_anim_animate_forever",
			"skill_contest",
			"set_shader_mode",
		};

		private readonly RealCompilerFixture _compiler;

		public RealFunctionCatalogTests(RealCompilerFixture compiler)
		{
			this._compiler = compiler;
		}

		private static String Arguments(FunctionSignature signature, Int32 count)
		{
			// A procedure parameter takes the name of a procedure; the compiler checks nothing else about the types.
			return String.Join(", ", Enumerable.Range(0, count).Select(i => i < signature.Parameters.Count && signature.Parameters[i].Type.Equals("procedure", StringComparison.OrdinalIgnoreCase) ? "start" : "1"));
		}

		private async Task<Boolean> AcceptsAsync(String name, String arguments)
		{
			String statement = "procedure start begin\r\n\t" + name + "(" + arguments + ");\r\nend\r\n";
			if ((await this._compiler.CompileAsync(statement)).Success)
			{
				return true;
			}

			String value = "procedure start begin\r\n\tvariable x;\r\n\tx := " + name + "(" + arguments + ");\r\nend\r\n";
			return (await this._compiler.CompileAsync(value)).Success;
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task The_argument_checks_agree_with_the_compiler_for_the_functions_of_the_catalog()
		{
			FunctionSignatureCatalog catalog = DiagnosticsFixture.ShippedCatalog;
			String[] names = catalog.Names
				.Where(n => ScriptBuiltins.IsBuiltin(n) && !ScriptBuiltins.IsKeyword(n) && !n.StartsWith("sfall_func", StringComparison.OrdinalIgnoreCase)
					&& catalog.TryGetSignatures(n, out FunctionSignature[] signatures) && signatures.All(s => s.MaxArguments < 12))
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.Where((n, i) => i % 3 == 0 || _knownDiscrepancies.Contains(n))
				.ToArray();
			Assert.True(names.Length > 100, "functions: " + names.Length);

			ConcurrentBag<String> disagreements = [];
			Int32 compared = 0;
			using SemaphoreSlim gate = new SemaphoreSlim(6);
			await Task.WhenAll(names.Select(async name =>
			{
				await gate.WaitAsync();
				try
				{
					catalog.TryGetSignatures(name, out FunctionSignature[] signatures);
					Int32 minimum = signatures.Min(s => s.MinArguments);
					Int32 maximum = Math.Min(signatures.Max(s => s.MaxArguments), 6);
					foreach (Int32 count in new[] { minimum - 1, minimum, maximum, maximum + 1 }.Where(c => c >= 0).Distinct())
					{
						String arguments = Arguments(signatures[0], count);
						String script = "procedure start begin\r\n\t" + name + "(" + arguments + ");\r\nend\r\n";
						Boolean flagged = DiagnosticsFixture.Codes(DiagnosticsFixture.Analyze(script)).Any(c => c is "SSL2001" or "SSL2004");
						Boolean accepted = await this.AcceptsAsync(name, arguments);
						Interlocked.Increment(ref compared);
						if (flagged == accepted)
						{
							disagreements.Add(name + "(" + count + " arguments): the compiler " + (accepted ? "accepts" : "rejects") + " it and the analysis " + (flagged ? "reports" : "does not report") + " it");
						}
					}
				}
				finally
				{
					gate.Release();
				}
			}));

			Assert.True(compared > 250, "compared: " + compared);
			String[] unexpected = disagreements.Where(d => !_knownDiscrepancies.Contains(d.Substring(0, d.IndexOf('(', StringComparison.Ordinal)))).OrderBy(d => d, StringComparer.Ordinal).ToArray();
			Assert.True(unexpected.Length == 0, String.Join(Environment.NewLine, unexpected));
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task No_script_of_the_FO2tweaksMk2_sample_gets_an_error_and_most_of_them_compile()
		{
			String[] scripts = Directory.GetFiles(SslcTools.Fo2TweaksSources, "*.ssl");
			Assert.True(scripts.Length >= 20, "scripts: " + scripts.Length);

			List<String> problems = [];
			Int32 compiled = 0;
			foreach (String script in scripts)
			{
				String text = File.ReadAllText(script);
				IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(text);
				foreach (ScriptDiagnostic error in diagnostics.Where(d => d.Severity == ScriptDiagnosticSeverity.Error))
				{
					problems.Add(Path.GetFileName(script) + "(" + error.Line + "," + error.Column + "): " + error.Code + " " + String.Join(", ", error.MessageArguments));
				}

				if ((await this._compiler.CompileAsync(text)).Success)
				{
					compiled++;
				}
			}

			Assert.True(compiled >= 20, "Only " + compiled + " scripts compile with this sslc, the comparison is not meaningful.");
			Assert.True(problems.Count == 0, String.Join(Environment.NewLine, problems));
		}

		[RealToolFact(RealTools.Fo2TweaksSamples)]
		public void The_headers_of_the_sample_are_analysed_without_an_error_when_their_macros_are_unknown_to_the_analysis()
		{
			String[] headers = Directory.GetFiles(SslcTools.Fo2TweaksSources, "*.h", SearchOption.AllDirectories);
			Assert.NotEmpty(headers);

			foreach (String header in headers)
			{
				IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(File.ReadAllText(header), new ScriptEnvironment { IsComplete = false, Functions = DiagnosticsFixture.ShippedCatalog });

				Assert.DoesNotContain(diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error);
			}
		}
	}
}