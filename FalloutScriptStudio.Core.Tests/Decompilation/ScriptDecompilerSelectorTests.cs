namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public sealed class ScriptDecompilerSelectorTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		[Fact]
		public async Task Decompile_ChoosesTheDecompilerByTheOptions()
		{
			RecordingDecompiler builtIn = new RecordingDecompiler("built-in");
			RecordingDecompiler external = new RecordingDecompiler("external");
			ScriptDecompilerSelector selector = new ScriptDecompilerSelector(builtIn, external);
			DecompilationRequest defaults = new DecompilationRequest { ScriptPath = "a.int", Options = this._workspace.CreateOptions() };
			DecompilationRequest chosen = defaults with { Options = defaults.Options with { Decompiler = DecompilerKind.ExternalInt2Ssl } };

			Assert.Equal("built-in", (await selector.DecompileAsync(defaults)).Output);
			Assert.Equal("external", (await selector.DecompileAsync(chosen)).Output);
			Assert.Same(defaults, builtIn.Request);
			Assert.Same(chosen, external.Request);
		}

		[Fact]
		public void TheDefaultSettingIsTheBuiltInDecompiler()
		{
			Assert.Equal(DecompilerKind.BuiltIn, new CompilerSettings().Decompiler);
			Assert.Null(new CompilerSettings().ExternalDecompilerPath);
			Assert.Equal(DecompilerKind.BuiltIn, this._workspace.CreateOptions().Decompiler);
			Assert.Null(this._workspace.CreateOptions().ExternalDecompilerPath);
		}

		[Fact]
		public async Task AddDecompilation_RegistersTheSelectorAndBothDecompilers()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton<IProcessRunner, FakeProcessRunner>();
			services.AddDecompilation();
			using ServiceProvider provider = services.BuildServiceProvider();

			IScriptDecompiler decompiler = provider.GetRequiredService<IScriptDecompiler>();

			Assert.IsType<ScriptDecompilerSelector>(decompiler);
			Assert.NotNull(provider.GetRequiredService<BuiltInScriptDecompiler>());
			Assert.NotNull(provider.GetRequiredService<ExternalInt2SslDecompiler>());

			String intFile = this._workspace.CreateFile(@"scripts\test.int", String.Empty);
			File.WriteAllBytes(intFile, CompiledScriptSamples.LoadMap);
			DecompilationResult result = await decompiler.DecompileAsync(new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() });
			Assert.True(result.Success, result.Output);
		}

		[Fact]
		public void TheScriptCompiler_CanBeCreatedByTheContainerWithAndWithoutDecompilers()
		{
			ServiceCollection withoutDecompilers = new ServiceCollection();
			withoutDecompilers.AddLogging();
			withoutDecompilers.AddSingleton<IProcessRunner, FakeProcessRunner>();
			withoutDecompilers.AddSingleton<IScriptCompiler, ScriptCompiler>();

			ServiceCollection withDecompilers = new ServiceCollection();
			withDecompilers.AddLogging();
			withDecompilers.AddSingleton<IProcessRunner, FakeProcessRunner>();
			withDecompilers.AddDecompilation();
			withDecompilers.AddSingleton<IScriptCompiler, ScriptCompiler>();

			using ServiceProvider first = withoutDecompilers.BuildServiceProvider();
			using ServiceProvider second = withDecompilers.BuildServiceProvider();

			Assert.IsType<ScriptCompiler>(first.GetRequiredService<IScriptCompiler>());
			Assert.IsType<ScriptCompiler>(second.GetRequiredService<IScriptCompiler>());
		}

		private sealed class RecordingDecompiler : IScriptDecompiler
		{
			private readonly String _name;

			public RecordingDecompiler(String name)
			{
				this._name = name;
			}

			public DecompilationRequest? Request { get; private set; }

			public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
			{
				this.Request = request;
				return Task.FromResult(new DecompilationResult(true, null, this._name));
			}
		}
	}
}
