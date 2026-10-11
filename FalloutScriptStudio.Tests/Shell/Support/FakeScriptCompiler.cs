namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>A compiler that does not start any tool and returns scripted results.</summary>
	internal sealed class FakeScriptCompiler : IScriptCompiler
	{
		public Boolean CompileSucceeds { get; set; } = true;

		public String? DecompiledText { get; set; }

		public List<String> Compiled { get; } = new List<String>();

		public List<String> Preprocessed { get; } = new List<String>();

		public String? PreprocessedFile { get; set; }

		public IReadOnlyList<BuildError> Errors { get; set; } = Array.Empty<BuildError>();

		public Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			this.Compiled.Add(request.ScriptPath);
			return Task.FromResult(new CompilationResult
			{
				Success = this.CompileSucceeds,
				Output = "compile output",
				Errors = this.Errors,
				OutputFilePath = Path.ChangeExtension(request.ScriptPath, ".int"),
			});
		}

		public Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			this.Preprocessed.Add(request.ScriptPath);
			return Task.FromResult(new CompilationResult
			{
				Success = this.CompileSucceeds,
				Output = "preprocess output",
				PreprocessedFilePath = this.PreprocessedFile,
			});
		}

		public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			if (this.DecompiledText is null)
			{
				return Task.FromResult(new DecompilationResult(false, null, "failed"));
			}

			outputProgress?.Report("decompiling");
			String file = Path.Combine(request.Options.ScratchDirectory, Path.GetFileNameWithoutExtension(request.ScriptPath) + ".ssl");
			Directory.CreateDirectory(request.Options.ScratchDirectory);
			File.WriteAllText(file, this.DecompiledText);
			return Task.FromResult(new DecompilationResult(true, file, "ok"));
		}

		public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
		{
			return Path.ChangeExtension(scriptPath, ".int");
		}
	}
}
