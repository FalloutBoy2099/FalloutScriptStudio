namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// The refactorings applied to the real scripts of FO2tweaksMk2 give scripts that the real compiler (<c>compile.exe</c>) still accepts.
	/// Skipped when the compiler or the samples are absent.
	/// </summary>
	public sealed class RealRefactoringTests : IDisposable
	{
		private static readonly Encoding _encoding = TextEncodings.Ansi;
		private readonly TestFolder _folder = new TestFolder("refactoring-compile");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private static async Task<(Boolean Success, String Output)> CompileAsync(String script)
		{
			ProcessRunResult run = await new ProcessRunner().RunAsync(new ProcessRunRequest
			{
				FileName = SslcTools.CompileExe,
				Arguments = "-q -l -O1 -p \"" + Path.GetFileName(script) + "\" -o \"" + Path.ChangeExtension(script, ".int") + "\"",
				WorkingDirectory = Path.GetDirectoryName(script),
			});
			return (run.ExitCode == 0, run.Output);
		}

		private String CopySources()
		{
			String target = Path.Combine(this._folder.Path, "sources");
			foreach (String file in Directory.EnumerateFiles(SslcTools.Fo2TweaksSources, "*", SearchOption.AllDirectories))
			{
				String destination = Path.Combine(target, Path.GetRelativePath(SslcTools.Fo2TweaksSources, file));
				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
				File.Copy(file, destination, true);
			}

			return target;
		}

		private static String Around(String text, String compilerOutput)
		{
			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(compilerOutput, @"\[Error\][^\r\n]*?:(\d+):(\d+):");
			if (!match.Success)
			{
				return String.Empty;
			}

			String[] lines = text.Replace("\r", String.Empty, StringComparison.Ordinal).Split('\n');
			Int32 line = Int32.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
			return String.Join("\n", lines.Skip(Math.Max(line - 6, 0)).Take(9));
		}

		private static IEnumerable<(Int32 Start, Int32 End)> TopLevelStatements(SslScriptModel model, SslProcedureModel procedure)
		{
			SslTokenStream tokens = model.Tokens;
			Int32 position = procedure.BeginToken + 1;
			while (position < procedure.EndToken)
			{
				Int32 end = SslStatements.FindStatementEnd(tokens, position);
				if (end >= procedure.EndToken)
				{
					yield break;
				}

				yield return (position, end);
				position = end + 1;
			}
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task Extracting_statements_of_the_real_scripts_still_compiles()
		{
			String sources = this.CopySources();
			Int32 extracted = 0;
			List<String> failures = new List<String>();
			foreach (String script in Directory.GetFiles(sources, "gl_g_*.ssl").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(27))
			{
				if (!(await CompileAsync(script)).Success)
				{
					continue;
				}

				String text = File.ReadAllText(script, _encoding);
				SslScriptModel model = SslScriptModel.Analyze(text);
				Int32 taken = 0;
				foreach (SslProcedureModel procedure in model.Procedures.Where(p => p.IsClosed))
				{
					List<(Int32 Start, Int32 End)> statements = TopLevelStatements(model, procedure).ToList();
					for (Int32 i = 0; i < statements.Count && taken < 6; i++)
					{
						// One statement, and for the first ones two statements in a row.
						Int32 last = Math.Min(i + (i % 2), statements.Count - 1);
						Int32 start = model.Tokens[statements[i].Start].Offset;
						Int32 end = model.Tokens[statements[last].End].End;
						ExtractAnalysis analysis = ProcedureExtractor.Analyze(text, start, end - start);
						if (!analysis.IsValid || (i % 2) != 0)
						{
							continue;
						}

						String declaration = "fss_extracted_" + taken + (analysis.Parameters.Count == 0 ? "()" : "(" + String.Join(", ", analysis.Parameters) + ")");
						ExtractResult result = ProcedureExtractor.CreateEdits(text, analysis, declaration, "    ");
						String refactored = Path.Combine(Path.GetDirectoryName(script)!, Path.GetFileNameWithoutExtension(script) + "_x" + taken + ".ssl");
						File.WriteAllText(refactored, result.NewText, _encoding);
						(Boolean success, String output) = await CompileAsync(refactored);
						extracted++;
						taken++;
						if (!success)
						{
							failures.Add(Path.GetFileName(script) + " / " + procedure.Name + "\n" + output + "\n" + Around(result.NewText, output));
						}
					}
				}
			}

			Assert.True(extracted >= 10, "only " + extracted + " extractions were tried");
			Assert.True(failures.Count == 0, String.Join("\n---\n", failures));
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task Removing_the_unused_variables_of_the_real_scripts_still_compiles()
		{
			String sources = this.CopySources();
			Int32 removed = 0;
			List<String> failures = new List<String>();
			foreach (String script in Directory.GetFiles(sources, "gl_g_*.ssl").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
			{
				if (!(await CompileAsync(script)).Success)
				{
					continue;
				}

				String text = File.ReadAllText(script, _encoding);
				while (UnusedVariableFinder.Find(text).FirstOrDefault(v => v.CanRemove) is { } variable)
				{
					TextEdit edit = VariableDeclarationEditor.CreateRemoval(text, variable)!;
					text = TextEditApplier.Apply(text, new[] { edit });
					removed++;
				}

				String refactored = Path.Combine(Path.GetDirectoryName(script)!, Path.GetFileNameWithoutExtension(script) + "_v.ssl");
				File.WriteAllText(refactored, text, _encoding);
				(Boolean success, String output) = await CompileAsync(refactored);
				if (!success)
				{
					failures.Add(Path.GetFileName(script) + "\n" + output);
				}
			}

			Assert.True(failures.Count == 0, String.Join("\n---\n", failures));
			Assert.True(removed > 0, "no unused variable was removed");
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task Deleting_an_unused_procedure_of_a_real_script_still_compiles()
		{
			String sources = this.CopySources();
			Int32 deleted = 0;
			List<String> failures = new List<String>();
			foreach (String script in Directory.GetFiles(sources, "gl_g_*.ssl").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(10))
			{
				if (!(await CompileAsync(script)).Success)
				{
					continue;
				}

				String text = File.ReadAllText(script, _encoding);

				// A procedure that nothing calls is added (with a leading comment), then removed again.
				String withExtra = text + "\r\n\r\n// Never called.\r\nprocedure fss_never_called begin\r\n    return 1;\r\nend\r\n";
				ProcedureRemovalPlan plan = UnusedProcedureRemover.Plan(withExtra, "fss_never_called")!;
				Assert.Equal(text.TrimEnd() + "\r\n", TextEditApplier.Apply(withExtra, plan.Edits).TrimEnd() + "\r\n");

				// Every procedure of the script that is not used (as the references show) is removed.
				foreach (ScriptProcedure procedure in new InternalParser().ParseProcedures(text, script).Where(p => p.StartLine > 0))
				{
					if (UnusedProcedureRemover.IsEngineHandler(procedure.Name))
					{
						continue;
					}

					Boolean used = SymbolOccurrenceScanner.Scan(text, script, procedure.Name, false, true).Any(UnusedProcedureRemover.IsUsage);
					ProcedureRemovalPlan? real = UnusedProcedureRemover.Plan(text, procedure.Name);
					if (used || real is null)
					{
						continue;
					}

					String refactored = Path.Combine(Path.GetDirectoryName(script)!, Path.GetFileNameWithoutExtension(script) + "_d" + deleted + ".ssl");
					File.WriteAllText(refactored, TextEditApplier.Apply(text, real.Edits), _encoding);
					deleted++;
					(Boolean success, String output) = await CompileAsync(refactored);
					if (!success)
					{
						failures.Add(Path.GetFileName(script) + " / " + procedure.Name + "\n" + output);
					}
				}
			}

			Assert.True(failures.Count == 0, String.Join("\n---\n", failures));
		}
	}
}
