namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Xunit;

	public sealed class BuiltInScriptDecompilerTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private String WriteScript(Byte[] content, String name = "test.int")
		{
			String path = Path.Combine(this._workspace.Scripts, name);
			File.WriteAllBytes(path, content);
			return path;
		}

		private DecompilationRequest CreateRequest(String intFile, CompilerOptions? options = null, Boolean temporary = false)
		{
			return new DecompilationRequest { ScriptPath = intFile, Options = options ?? this._workspace.CreateOptions(), TemporaryOutput = temporary };
		}

		[Fact]
		public async Task Decompile_WritesTheDecompiledFileLikeTheExternalTool()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.LoadMap);

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile));

			Assert.True(result.Success, result.Output);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[decomp].ssl"), result.OutputFilePath);
			Assert.False(File.Exists(Path.Combine(this._workspace.Scratch, "decomp.ssl")));
			String text = File.ReadAllText(result.OutputFilePath!, Encoding.Latin1);
			Assert.Contains("procedure start;", text, StringComparison.Ordinal);
			Assert.Contains("load_map(33, 0);", text, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_TemporaryOutputUsesTheTempSuffix()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.LoadMap);

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile, temporary: true));

			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[temp].ssl"), result.OutputFilePath);
			Assert.True(File.Exists(result.OutputFilePath));
		}

		[Fact]
		public async Task Decompile_UsesLineBreaksOfWindows_AndReplacesAnOlderResult()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.LoadMap);
			String old = Path.Combine(this._workspace.Scratch, "test_[decomp].ssl");
			File.WriteAllText(old, "stale");

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile));

			String text = File.ReadAllText(result.OutputFilePath!);
			Assert.DoesNotContain("stale", text, StringComparison.Ordinal);
			Assert.DoesNotContain("\n", text.Replace("\r\n", String.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_IndentsWithTheTabSettingsOfTheEditor()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.LoadMap);
			BuiltInScriptDecompiler decompiler = new BuiltInScriptDecompiler();

			DecompilationResult spaces = await decompiler.DecompileAsync(this.CreateRequest(intFile, this._workspace.CreateOptions() with { TabsToSpaces = true, TabSize = 4 }));
			String spacesText = File.ReadAllText(spaces.OutputFilePath!);
			DecompilationResult tabs = await decompiler.DecompileAsync(this.CreateRequest(intFile, this._workspace.CreateOptions() with { TabsToSpaces = false }));
			String tabsText = File.ReadAllText(tabs.OutputFilePath!);

			Assert.Contains("\r\n    if (script_action == 6) then begin\r\n        load_map(33, 0);\r\n", spacesText, StringComparison.Ordinal);
			Assert.Contains("\r\n\tif (script_action == 6) then begin\r\n\t\tload_map(33, 0);\r\n", tabsText, StringComparison.Ordinal);
		}

		[Fact]
		public void CreateEngineOptions_MapsTheCompilerOptions()
		{
			CompilerOptions options = this._workspace.CreateOptions() with { DecompileForFallout1 = true, TabsToSpaces = true, TabSize = 2 };

			ScriptDecompilerOptions engine = BuiltInScriptDecompiler.CreateEngineOptions(options);

			Assert.Equal(ScriptGame.Fallout1, engine.Game);
			Assert.Equal("  ", engine.Indent);
			Assert.Equal(ScriptGame.Fallout2, BuiltInScriptDecompiler.CreateEngineOptions(options with { DecompileForFallout1 = false }).Game);
			Assert.Equal("\t", BuiltInScriptDecompiler.CreateEngineOptions(options with { TabsToSpaces = false }).Indent);
		}

		[Fact]
		public async Task Decompile_ReportsProgressAndTheMessagesInTheOutput()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.Sample);
			List<String> lines = new List<String>();

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile), new SynchronousProgress(lines));

			Assert.True(result.Success);
			Assert.Contains(lines, line => line.Contains("test.int", StringComparison.Ordinal));
			Assert.Equal(lines, result.Output.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList());
		}

		[Fact]
		public async Task Decompile_ScriptsThatAreNotCompiledScripts_FailWithoutAFile()
		{
			String intFile = this.WriteScript(Encoding.ASCII.GetBytes("procedure start begin end"));

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile));

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.Contains("[Error]", result.Output, StringComparison.Ordinal);
			Assert.False(File.Exists(Path.Combine(this._workspace.Scratch, "test_[decomp].ssl")));
		}

		[Fact]
		public async Task Decompile_AMissingFile_FailsWithoutThrowing()
		{
			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(Path.Combine(this._workspace.Scripts, "missing.int")));

			Assert.False(result.Success);
			Assert.Contains("could not be read", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_CanBeCancelled()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.LoadMap);
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile), null, cancellation.Token));
		}

		[Fact]
		public async Task Decompile_ARealisticScript_KeepsStringsAndNames()
		{
			String intFile = this.WriteScript(CompiledScriptSamples.Sample);

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest(intFile));

			String text = File.ReadAllText(result.OutputFilePath!, Encoding.Latin1);
			Assert.Contains("procedure helper(variable arg0, variable arg1);", text, StringComparison.Ordinal);
			Assert.Contains("variable msg_text := \"hello\\n\";", text, StringComparison.Ordinal);
			Assert.Contains("call timed_event in (5);", text, StringComparison.Ordinal);
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
