namespace FalloutScriptStudio.Core.Tests.Decompilation
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Xunit;

	/// <summary>A file of the scratch folder that cannot be deleted or replaced gives a failed result, not an exception for the UI.</summary>
	public sealed class DecompilationFileFailureTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			foreach (String file in Directory.GetFiles(this._workspace.Root, "*", SearchOption.AllDirectories))
			{
				File.SetAttributes(file, FileAttributes.Normal);
			}

			this._workspace.Dispose();
		}

		private DecompilationRequest CreateRequest()
		{
			String intFile = Path.Combine(this._workspace.Scripts, "test.int");
			File.WriteAllBytes(intFile, CompiledScriptSamples.LoadMap);
			return new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() };
		}

		[Fact]
		public async Task Decompile_WhenTheWorkFileCannotBeDeleted_FailsWithoutThrowing()
		{
			String workFile = Path.Combine(this._workspace.Scratch, "decomp.ssl");
			File.WriteAllText(workFile, "old");
			File.SetAttributes(workFile, FileAttributes.ReadOnly);

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest());

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.Contains("[Error]", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_WhenTheResultCannotBeReplaced_FailsWithoutThrowing()
		{
			String previous = Path.Combine(this._workspace.Scratch, "test_[decomp].ssl");
			File.WriteAllText(previous, "old");
			File.SetAttributes(previous, FileAttributes.ReadOnly);

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(this.CreateRequest());

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.Contains("could not be saved", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_WhenTheScratchFolderIsAFile_FailsWithoutThrowing()
		{
			DecompilationRequest request = this.CreateRequest();
			String blocker = Path.Combine(this._workspace.Root, "blocker");
			File.WriteAllText(blocker, "not a folder");
			request = request with { Options = request.Options with { ScratchDirectory = Path.Combine(blocker, "scrTemp") } };

			DecompilationResult result = await new BuiltInScriptDecompiler().DecompileAsync(request);

			Assert.False(result.Success);
			Assert.Contains("scratch folder", result.Output, StringComparison.OrdinalIgnoreCase);
		}
	}
}
