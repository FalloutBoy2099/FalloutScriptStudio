namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>A compiler stand-in that writes the compiled file the way the real one does and counts what it was asked to compile.</summary>
	internal sealed class WritingCompiler : IScriptCompiler
	{
		private readonly ConcurrentQueue<String> _compiled = new ConcurrentQueue<String>();

		/// <summary>Decides whether a script compiles (by default every script does).</summary>
		public Func<String, Boolean> Succeeds { get; set; } = _ => true;

		/// <summary>Called when a compilation starts (it may block, or cancel).</summary>
		public Func<String, CancellationToken, Task>? OnStart { get; set; }

		/// <summary>Gets the scripts that were compiled (including the ones that failed), in order of completion.</summary>
		public IReadOnlyCollection<String> Compiled => this._compiled;

		public void Reset()
		{
			while (this._compiled.TryDequeue(out _))
			{
			}
		}

		public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			if (this.OnStart is not null)
			{
				await this.OnStart(request.ScriptPath, cancellationToken).ConfigureAwait(false);
			}

			cancellationToken.ThrowIfCancellationRequested();
			Boolean success = this.Succeeds(request.ScriptPath);
			this._compiled.Enqueue(request.ScriptPath);
			String? output = null;
			if (success)
			{
				output = this.GetOutputPath(request.ScriptPath, request.Options);
				await File.WriteAllTextAsync(output, "int of " + Path.GetFileName(request.ScriptPath) + " " + Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
			}

			return new CompilationResult { Success = success, Output = success ? "ok" : "[Error] " + request.ScriptPath + ":1:1: broken", OutputFilePath = output };
		}

		public Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException();
		}

		public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException();
		}

		public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
		{
			return new CompilerPaths(options).GetOutputPath(Path.GetFullPath(scriptPath), String.Empty, temporaryOutput);
		}
	}
}