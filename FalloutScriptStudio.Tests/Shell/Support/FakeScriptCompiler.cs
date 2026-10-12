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

		/// <summary>Gets the request of the last compilation.</summary>
		public CompilationRequest? LastRequest { get; private set; }

		public List<String> Preprocessed { get; } = new List<String>();

		public String? PreprocessedFile { get; set; }

		public IReadOnlyList<BuildError> Errors { get; set; } = Array.Empty<BuildError>();

		/// <summary>Gets or sets the tool failures reported with every compilation, preprocessing and decompilation.</summary>
		public IReadOnlyList<ToolFailure> ToolFailures { get; set; } = Array.Empty<ToolFailure>();

		/// <summary>Gets or sets a value indicating whether a compilation, preprocessing or decompilation waits until it is cancelled (a hanging tool).</summary>
		public Boolean Hang { get; set; }

		/// <summary>Gets the number of runs that started waiting because of <see cref="Hang"/>.</summary>
		public Int32 HangingRuns { get; private set; }

		/// <summary>Gets the number of runs that were cancelled while waiting.</summary>
		public Int32 CancelledRuns { get; private set; }

		/// <summary>Gets or sets a function that decides, from the number of the run (1 for the first), whether that run hangs; overrides <see cref="Hang"/>.</summary>
		public Func<Int32, Boolean>? HangWhen { get; set; }

		public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			this.Compiled.Add(request.ScriptPath);
			this.LastRequest = request;
			await this.MaybeHangAsync(this.Compiled.Count, cancellationToken);
			return new CompilationResult
			{
				Success = this.CompileSucceeds,
				Output = "compile output",
				Errors = this.Errors,
				OutputFilePath = Path.ChangeExtension(request.ScriptPath, ".int"),
				ToolFailures = this.ToolFailures,
			};
		}

		public async Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			this.Preprocessed.Add(request.ScriptPath);
			await this.MaybeHangAsync(this.Preprocessed.Count, cancellationToken);
			return new CompilationResult
			{
				Success = this.CompileSucceeds,
				Output = "preprocess output",
				PreprocessedFilePath = this.PreprocessedFile,
				ToolFailures = this.ToolFailures,
			};
		}

		public async Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			await this.MaybeHangAsync(1, cancellationToken);
			if (this.DecompiledText is null)
			{
				return new DecompilationResult(false, null, "failed") { ToolFailures = this.ToolFailures };
			}

			outputProgress?.Report("decompiling");
			String file = Path.Combine(request.Options.ScratchDirectory, Path.GetFileNameWithoutExtension(request.ScriptPath) + ".ssl");
			Directory.CreateDirectory(request.Options.ScratchDirectory);
			File.WriteAllText(file, this.DecompiledText);
			return new DecompilationResult(true, file, "ok");
		}

		public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
		{
			return Path.ChangeExtension(scriptPath, ".int");
		}

		private async Task MaybeHangAsync(Int32 run, CancellationToken cancellationToken)
		{
			if (!(this.HangWhen?.Invoke(run) ?? this.Hang))
			{
				cancellationToken.ThrowIfCancellationRequested();
				return;
			}

			this.HangingRuns++;
			try
			{
				await Task.Delay(Timeout.Infinite, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				this.CancelledRuns++;
				throw;
			}
		}
	}
}
