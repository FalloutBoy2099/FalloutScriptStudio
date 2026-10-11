namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class ExternalInt2SslDecompilerTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly FakeProcessRunner _runner = new FakeProcessRunner();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private ExternalInt2SslDecompiler CreateDecompiler()
		{
			return new ExternalInt2SslDecompiler(this._runner, NullLogger<ExternalInt2SslDecompiler>.Instance);
		}

		private CompilerOptions Options(CompilerOptions? options = null)
		{
			return (options ?? this._workspace.CreateOptions()) with { Decompiler = DecompilerKind.ExternalInt2Ssl };
		}

		[Fact]
		public async Task Decompile_FallsBackToTheOldDecompilerAndReportsProgress()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = request =>
			{
				request.OutputLineProgress?.Report("working " + Path.GetFileName(request.FileName));
				if (request.FileName.EndsWith("int2ssl_v35.exe", StringComparison.Ordinal))
				{
					File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "decompiled");
					return new ProcessRunResult(0, "ok\r\n");
				}

				return new ProcessRunResult(1, "failed\r\n");
			};
			List<String> lines = new List<String>();

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this.Options() },
				new SynchronousProgress(lines));

			Assert.True(result.Success);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[decomp].ssl"), result.OutputFilePath);
			Assert.Equal("decompiled", File.ReadAllText(result.OutputFilePath!));
			Assert.Equal(new[] { "working int2ssl.exe", "working int2ssl_v35.exe" }, lines);
			Assert.Equal("failed\r\nok\r\n", result.Output);
			Assert.Equal($" -s3 \"{intFile}\" \"{Path.Combine(this._workspace.Scratch, "decomp.ssl")}\"", this._runner.Requests[0].Arguments);
		}

		[Fact]
		public async Task Decompile_TheCommandLineIsTheOneOfTheLegacyBuilder()
		{
			this._workspace.CreateTools("int2ssl.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			CompilerOptions options = this.Options(this._workspace.CreateOptions() with { DecompileForFallout1 = true, TabSize = 4 });

			await this.CreateDecompiler().DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = options });

			Assert.Equal(
				new CompilerCommandLineBuilder(options).BuildDecompiler(intFile, Path.Combine(this._workspace.Scratch, "decomp.ssl")),
				Assert.Single(this._runner.Requests).Arguments);
			Assert.StartsWith("-1 -s4 ", this._runner.Requests[0].Arguments, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_OldDecompilerOptionSkipsTheNewOne_AndFailureIsReported()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ => new ProcessRunResult(1, "failed\r\n");

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this.Options(this._workspace.CreateOptions() with { UseLegacyDecompiler = true }), TemporaryOutput = true });

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.EndsWith("int2ssl_v35.exe", Assert.Single(this._runner.Requests).FileName, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_TemporaryOutputUsesTheTempSuffix()
		{
			this._workspace.CreateTools("int2ssl.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "x");
				return new ProcessRunResult(0, String.Empty);
			};

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this.Options(), TemporaryOutput = true });

			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[temp].ssl"), result.OutputFilePath);
		}

		[Fact]
		public async Task Decompile_MissingToolsAreReported_AndNothingIsStarted()
		{
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = this.Options() });

			Assert.False(result.Success);
			Assert.Empty(this._runner.Requests);
			Assert.Contains(Path.Combine(this._workspace.Resources, "int2ssl.exe"), result.Output, StringComparison.Ordinal);
			Assert.Contains("int2ssl_v35.exe", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_AToolThatCannotBeStarted_IsSkipped()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("int2ssl.exe", StringComparison.Ordinal))
				{
					throw new Win32Exception("not a valid application");
				}

				File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "from the old one");
				return new ProcessRunResult(0, String.Empty);
			};

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = this.Options() });

			Assert.True(result.Success);
			Assert.Contains("Failed to start", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_AFailingToolThatLeavesAnEmptyOutputFile_IsAFailureAndTheFileIsRemoved()
		{
			// The real int2ssl.exe creates the output file before it reads the input: an invalid .int leaves an empty file and exit code 1.
			this._workspace.CreateTools("int2ssl.exe");
			String intFile = this._workspace.CreateFile(@"scripts\junk.int", "not a compiled script");
			String workFile = Path.Combine(this._workspace.Scratch, "decomp.ssl");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(workFile, String.Empty);
				return new ProcessRunResult(1, "Error: Invalid opcode at 0x00000000\r\n");
			};

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = this.Options() });

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.False(File.Exists(workFile));
			Assert.Contains("Invalid opcode", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_AStaleWorkFileIsNeverTakenForAResult()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "stale");
			this._runner.Handler = _ => new ProcessRunResult(1, String.Empty);

			DecompilationResult result = await this.CreateDecompiler().DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = this.Options() });

			Assert.False(result.Success);
		}

		[Fact]
		public async Task Decompile_UsesTheConfiguredProgram_AndItsFolderForTheOldOne()
		{
			String custom = Path.Combine(this._workspace.Root, "tools", "my int2ssl.exe");
			Directory.CreateDirectory(Path.GetDirectoryName(custom)!);
			File.WriteAllText(custom, String.Empty);
			File.WriteAllText(Path.Combine(this._workspace.Root, "tools", "int2ssl_v35.exe"), String.Empty);
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ => new ProcessRunResult(1, String.Empty);

			await this.CreateDecompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this.Options(this._workspace.CreateOptions() with { ExternalDecompilerPath = custom }) });

			Assert.Equal(
				new[] { custom, Path.Combine(this._workspace.Root, "tools", "int2ssl_v35.exe") },
				this._runner.Requests.Select(request => request.FileName).ToArray());
		}

		[Fact]
		public void GetPrograms_ExplainsTheOrderOfTheCandidates()
		{
			CompilerOptions options = this._workspace.CreateOptions();
			String resources = this._workspace.Resources;

			Assert.Equal(
				new[] { Path.Combine(resources, "int2ssl.exe"), Path.Combine(resources, "int2ssl_v35.exe") },
				ExternalInt2SslDecompiler.GetPrograms(options));
			Assert.Equal(
				new[] { Path.Combine(resources, "int2ssl_v35.exe") },
				ExternalInt2SslDecompiler.GetPrograms(options with { UseLegacyDecompiler = true }));
			Assert.Equal(
				new[] { Path.Combine(@"C:\tools", "int2ssl.exe"), Path.Combine(@"C:\tools", "int2ssl_v35.exe") },
				ExternalInt2SslDecompiler.GetPrograms(options with { ExternalDecompilerPath = @"C:\tools\int2ssl.exe" }));
			Assert.Equal(
				new[] { Path.Combine(@"C:\tools", "int2ssl_v35.exe") },
				ExternalInt2SslDecompiler.GetPrograms(options with { ExternalDecompilerPath = @"C:\tools\int2ssl_v35.exe" }));
			Assert.Equal(
				new[] { Path.Combine(resources, "int2ssl.exe"), Path.Combine(resources, "int2ssl_v35.exe") },
				ExternalInt2SslDecompiler.GetPrograms(options with { ExternalDecompilerPath = "  " }));
		}

		[Fact]
		public void GetPrograms_AFolderAsThePathMeansTheDefaultNamesInIt()
		{
			String folder = Path.Combine(this._workspace.Root, "tools");
			Directory.CreateDirectory(folder);

			IReadOnlyList<String> programs = ExternalInt2SslDecompiler.GetPrograms(this._workspace.CreateOptions() with { ExternalDecompilerPath = folder });

			Assert.Equal(new[] { Path.Combine(folder, "int2ssl.exe"), Path.Combine(folder, "int2ssl_v35.exe") }, programs);
		}

		private sealed class SynchronousProgress : IProgress<String>
		{
			private readonly List<String> _target;

			public SynchronousProgress(List<String> target)
			{
				this._target = target;
			}

			public void Report(String value)
			{
				lock (this._target)
				{
					this._target.Add(value);
				}
			}
		}
	}
}
