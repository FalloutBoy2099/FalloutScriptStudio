namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using Xunit;

	/// <summary>
	/// Parses the real scripts of the author's mods (<c>_test_files\samples</c>, copied to a scratch folder first) with the real
	/// <c>parser.dll</c>, compares the verdict with the real <c>compile.exe</c> and checks the reported lines against the text of the
	/// files. Skipped when the tools or the sample set are absent (InventoryFilterMk2 and PartyOrdersMk2 are optional sample sets that
	/// are not shipped), see <see cref="SslcTools"/>.
	/// </summary>
	public sealed class NativeParserSampleTests : IClassFixture<NativeParserFixture>
	{
		private static readonly Regex _firstError = new Regex(@"\[Error\][^\r\n]*?:(?<line>\d+):(?<column>\d+):", RegexOptions.CultureInvariant);

		private readonly NativeParserFixture _fixture;

		public NativeParserSampleTests(NativeParserFixture fixture)
		{
			this._fixture = fixture;
		}

		/// <summary>
		/// Lists the procedures whose reported lines do not match the text of their file. The body starts at the line of
		/// <c>begin</c>, which may be a few lines below <c>procedure name</c>; it ends at a line that starts with <c>end</c>.
		/// </summary>
		private static List<String> FindWrongLines(NativeParseResult result)
		{
			var problems = new List<String>();
			var files = new Dictionary<String, String[]>(StringComparer.OrdinalIgnoreCase);
			foreach (ScriptProcedure procedure in result.Procedures)
			{
				if (procedure.StartLine < 1)
				{
					continue;
				}

				if (!files.TryGetValue(procedure.StartFile, out String[]? lines))
				{
					lines = File.ReadAllLines(procedure.StartFile, TextEncodings.Ansi);
					files[procedure.StartFile] = lines;
				}

				Boolean nameFound = false;
				for (Int32 line = procedure.StartLine; line >= Math.Max(1, procedure.StartLine - 3) && line <= lines.Length; line--)
				{
					nameFound |= lines[line - 1].Contains(procedure.Name, StringComparison.OrdinalIgnoreCase);
				}

				if (!nameFound)
				{
					problems.Add(procedure.Name + ": the body starts at line " + procedure.StartLine + " of " + procedure.StartFile + ", which does not belong to it");
				}

				String end = procedure.EndLine >= 1 && procedure.EndLine <= lines.Length ? lines[procedure.EndLine - 1].Trim() : String.Empty;
				if (!end.StartsWith("end", StringComparison.OrdinalIgnoreCase))
				{
					problems.Add(procedure.Name + ": end line " + procedure.EndLine + " of " + procedure.StartFile + " is '" + end + "'");
				}
			}

			return problems;
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

		[RealToolFact(RealTools.Parser | RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task Parse_Fo2TweaksScripts_AgreeWithTheCompilerAndTheLinesMatchTheSourceText()
		{
			String sources = this._fixture.CopyDirectory(SslcTools.Fo2TweaksSources, "fo2tweaks");
			String[] scripts = Directory.GetFiles(sources, "gl_g_*.ssl");
			Assert.NotEmpty(scripts);

			var problems = new List<String>();
			Int32 compiled = 0;
			foreach (String script in scripts)
			{
				String name = Path.GetFileName(script);
				NativeParseResult native = await this._fixture.ParseFileAsync(script, File.ReadAllText(script, TextEncodings.Ansi));
				(Boolean compileSucceeded, String compilerOutput) = await CompileAsync(script);

				if (native.Succeeded != compileSucceeded)
				{
					problems.Add(name + ": parser.dll says " + native.ExitCode + " but compile.exe " + (compileSucceeded ? "succeeds" : "fails") + ". " + native.Message + native.Log);
					continue;
				}

				if (!compileSucceeded)
				{
					// Both tools reject the script (some scripts use syntax that this sslc does not know): they have to blame the same place.
					Match fromCompiler = _firstError.Match(compilerOutput);
					Match fromParser = _firstError.Match(native.Log);
					if (!fromCompiler.Success || !fromParser.Success || fromCompiler.Groups["line"].Value + ":" + fromCompiler.Groups["column"].Value != fromParser.Groups["line"].Value + ":" + fromParser.Groups["column"].Value)
					{
						problems.Add(name + ": the first error differs. Compiler: " + fromCompiler.Value + " Parser: " + fromParser.Value);
					}

					continue;
				}

				compiled++;
				problems.AddRange(FindWrongLines(native).Select(p => name + " " + p));
			}

			Assert.True(compiled >= 10, "Only " + compiled + " scripts compile, the comparison is not meaningful.");
			Assert.True(problems.Count == 0, String.Join(Environment.NewLine, problems));
		}

		[RealToolFact(RealTools.Parser | RealTools.InventoryFilterSamples)]
		public async Task Parse_InventoryFilterMainScript_FollowsTheIncludesOfAllModules()
		{
			String sources = this._fixture.CopyDirectory(SslcTools.InventoryFilterSources, "invenfilter");
			String main = Path.Combine(sources, "gl_InvenFilter.ssl");

			NativeParseResult result = await this._fixture.ParseFileAsync(main, File.ReadAllText(main, TextEncodings.Ansi));

			Assert.True(result.Succeeded, result.Message + result.Log);
			Assert.True(result.Procedures.Count > 100, "Procedures: " + result.Procedures.Count);
			Assert.Empty(FindWrongLines(result));

			// The prototypes sit in InvenFilter.h, the bodies in the module that includes it.
			ScriptProcedure start = result.Procedures.Single(p => p.Name == "start");
			Assert.Equal("InvenFilter.h", Path.GetFileName(start.DeclaredFile));
			Assert.Equal("gl_InvenFilter.ssl", Path.GetFileName(start.StartFile));
			Assert.True(start.HasForwardDeclaration);

			HashSet<String> modules = result.Procedures.Select(p => Path.GetFileName(p.StartFile)).ToHashSet(StringComparer.OrdinalIgnoreCase);
			Assert.Contains("ui_buttons.ssl", modules);
			Assert.Contains("filter_logic.ssl", modules);
			Assert.Contains("party_swap.ssl", modules);

			// A call to a procedure of another module is a reference reported with the file of the caller.
			Assert.Contains(result.Procedures, p => p.References.Any(r => r.File is not null && !String.Equals(r.File, p.StartFile, StringComparison.OrdinalIgnoreCase)));
			Assert.NotEmpty(result.Variables);
		}

		[RealToolFact(RealTools.Parser | RealTools.PartyOrdersSamples)]
		public async Task Parse_ScriptWhoseHeadersAreMissing_ReportsEveryMissingIncludeInTheLog()
		{
			String sources = this._fixture.CopyDirectory(SslcTools.PartyOrdersSources, "partyorders");
			String main = Path.Combine(sources, "gl_p_party_orders.ssl");

			NativeParseResult result = await this._fixture.ParseFileAsync(main, File.ReadAllText(main, TextEncodings.Ansi));

			Assert.Equal(NativeParseStatus.Completed, result.Status);
			Assert.Equal(2, result.ExitCode);
			Assert.False(result.HasData);
			Assert.Contains("headers/rp/define.h", result.Log, StringComparison.Ordinal);
			Assert.Contains("headers/rp/itempid.h", result.Log, StringComparison.Ordinal);
			Assert.Contains("Can't open include file", result.Log, StringComparison.Ordinal);
		}
	}
}