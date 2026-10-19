namespace FalloutScriptStudio.Core.Tests.Formatting
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.Tests.Native;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// Formatting must never change the meaning of a script: with the real <c>compile.exe</c>, the compiled file of the formatted script is
	/// the same as the one of the original, byte for byte (the compiler is deterministic and does not put the file name, the time or the
	/// line numbers into the compiled file), and a script the compiler rejects is rejected with the same message.
	/// </summary>
	// Heavy on the processor: it runs with the performance tests, never beside them, so that their timings stay meaningful.
	[Xunit.Collection(FalloutScriptStudio.Core.Tests.Performance.PerformanceCollection.Name)]
	public sealed class FormatCompilerTests
	{
		private static readonly Regex _position = new Regex(@":\d+(:\d+)?:", RegexOptions.CultureInvariant);
		private static readonly CodeFormatter _formatter = new CodeFormatter();

		public static IEnumerable<Object[]> SetNames()
		{
			return FormatSamples.SetNames();
		}

		private static async Task<(Boolean Success, Byte[]? Compiled, String Message)> CompileAsync(String folder, String name, String text)
		{
			String directory = Path.Combine(folder, Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			String script = Path.Combine(directory, name);
			await File.WriteAllTextAsync(script, text, FormatSamples.Bytes);
			String output = Path.ChangeExtension(script, ".int");
			ProcessRunResult run = await new ProcessRunner().RunAsync(new ProcessRunRequest
			{
				FileName = SslcTools.CompileExe,
				Arguments = "-q -l -O1 -p \"" + name + "\" -o \"" + output + "\"",
				WorkingDirectory = directory,
			});
			if (run.ExitCode == 0 && File.Exists(output))
			{
				return (true, await File.ReadAllBytesAsync(output), String.Empty);
			}

			return (false, null, _position.Replace(run.Output, ":").Replace(name, "<script>", StringComparison.Ordinal));
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task TheCompilerIsDeterministic_SoTheComparisonMeansSomething()
		{
			using TestFolder folder = new TestFolder("format-compile");
			(String name, String text) = FormatSamples.Fo2Tweaks().First(file => file.Name == "gl_g_ammobox.ssl");

			(Boolean first, Byte[]? a, _) = await CompileAsync(folder.Path, name, text);
			(Boolean second, Byte[]? b, _) = await CompileAsync(folder.Path, name, FormatSamples.Mutate(text, 3));

			Assert.True(first);
			Assert.True(second);
			Assert.NotNull(a);
			Assert.Equal(a, b);
		}

		[RealToolTheory(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		[MemberData(nameof(SetNames))]
		public async Task TheFormattedScriptsCompileToTheSameFile(String setName)
		{
			FormatOptions options = FormatSamples.Sets[setName];
			using TestFolder folder = new TestFolder("format-compile");
			List<(String Name, String Text)> scripts = FormatSamples.Fo2Tweaks().Where(file => file.Name.EndsWith(".ssl", StringComparison.Ordinal) && !file.Name.Contains('\\', StringComparison.Ordinal)).ToList();
			Assert.True(scripts.Count >= 20);
			ConcurrentBag<String> problems = [];
			Int32 compiled = 0;

			await Parallel.ForEachAsync(scripts, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (script, _) =>
			{
				(Boolean originalOk, Byte[]? original, String originalMessage) = await CompileAsync(folder.Path, script.Name, script.Text);
				if (originalOk)
				{
					System.Threading.Interlocked.Increment(ref compiled);
				}

				foreach (String input in new[] { script.Text, FormatSamples.Mutate(script.Text, 5) })
				{
					String formatted = _formatter.Format(input, options);
					(Boolean ok, Byte[]? compiledFile, String message) = await CompileAsync(folder.Path, script.Name, formatted);
					if (ok != originalOk)
					{
						problems.Add(script.Name + ": the verdict changed, the original " + (originalOk ? "compiles" : "fails") + " (" + message + originalMessage + ")");
					}
					else if (ok && !original!.AsSpan().SequenceEqual(compiledFile))
					{
						problems.Add(script.Name + ": the compiled file differs");
					}
					else if (!ok && !String.Equals(originalMessage, message, StringComparison.Ordinal))
					{
						problems.Add(script.Name + ": the compiler says something else: [" + message + "] instead of [" + originalMessage + "]");
					}
				}
			});

			Assert.True(compiled >= 20, "Only " + compiled + " scripts compile, the comparison is not meaningful.");
			Assert.True(problems.IsEmpty, setName + Environment.NewLine + String.Join(Environment.NewLine, problems.OrderBy(p => p, StringComparer.Ordinal)));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task TheEdgeCasesOfTheSyntaxCompileToTheSameFile()
		{
			String[] scripts =
			{
				"procedure start begin\r\n   variable a := 5;\r\n   variable x;\r\n   x := a-1;\r\n   x := a - -1;\r\n   x := a*-1;\r\n   x := -a;\r\n   x := (a)-1;\r\n   x += - 1;\r\n   x++;\r\n   x := a<-1;\r\nend\r\n",
				"procedure start begin\r\n   variable a := 5;\r\n   if (a) then\r\n      a := 1;\r\n   else\r\n      if (a == 2) then a := 3; else a := 4;\r\n   while (a < 10) do\r\n   begin\r\n      a += 1;\r\n   end\r\nend\r\n",
				"procedure start begin\r\n   variable a := 5;\r\n   switch (a) begin\r\n      case 1: a := 2;\r\n      case 2: begin a := 3; end\r\n      default: a := 0;\r\n   end\r\nend\r\n",
				"#define ADD(x, y) ((x)+(y))\r\n#define TWO 2\r\n#define NEG -1\r\nprocedure f(variable a, variable b) begin\r\n   return ADD(a,b)+TWO*NEG;\r\nend\r\nprocedure start begin\r\n   variable r := f(1,2);\r\n   display_msg(\"a+b,c:=d\" + r);\r\nend\r\n",
				"procedure start begin\r\n   variable s := \"x\";\r\n   /* a+b,\r\n      c */\r\n   s := s + \"y\"; // d+e,f\r\n   display_msg(s);\r\nend\r\n",
				"procedure start;\r\nprocedure other(variable a);\r\n\r\nprocedure other(variable a) begin\r\n   if (a) then begin\r\n   end else if (a == 3) then begin\r\n   end else begin\r\n   end\r\nend\r\n\r\nprocedure start begin\r\n   call other(1);\r\nend\r\n",
			};

			using TestFolder folder = new TestFolder("format-compile");
			foreach (String script in scripts)
			{
				(Boolean originalOk, Byte[]? original, String originalMessage) = await CompileAsync(folder.Path, "edge.ssl", script);
				Assert.True(originalOk, originalMessage);
				foreach ((String name, FormatOptions options) in FormatSamples.Sets)
				{
					(Boolean ok, Byte[]? compiledFile, String message) = await CompileAsync(folder.Path, "edge.ssl", _formatter.Format(script, options));
					Assert.True(ok, name + ": " + message + _formatter.Format(script, options));
					Assert.True(original!.AsSpan().SequenceEqual(compiledFile), name + " changed the compiled file of: " + script);
				}
			}
		}
	}
}