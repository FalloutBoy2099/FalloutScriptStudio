namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Decompiles compiled scripts for the workspace: runs the decompiler, fixes the result and asks where to save it.</summary>
	public sealed class ScriptDecompilationService
	{
		private readonly ISettingsService _settings;
		private readonly IScriptCompiler _compiler;
		private readonly IDialogService _dialogs;
		private readonly IOutputSink _output;
		private readonly DocumentFileService _files;
		private readonly IOperationTracker _operations;
		private readonly IProblemService _problems;

		/// <summary>Initializes the service.</summary>
		public ScriptDecompilationService(
			ISettingsService settings,
			IScriptCompiler compiler,
			IDialogService dialogs,
			IOutputSink output,
			DocumentFileService files,
			IOperationTracker? operations = null,
			IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(compiler);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(output);
			ArgumentNullException.ThrowIfNull(files);
			this._settings = settings;
			this._compiler = compiler;
			this._dialogs = dialogs;
			this._output = output;
			this._files = files;
			this._operations = operations ?? new OperationTracker();
			this._problems = problems ?? new ProblemService();
		}

		/// <summary>
		/// Decompiles a <c>.int</c> file, logging to the Build tab. Unless the decompilation is temporary the user is asked where to
		/// save the source; the caller then writes it with <see cref="SaveTargetAsync"/> (after checking that the target is not open).
		/// </summary>
		/// <returns>The decompiled script, or <see langword="null"/> (an error was already shown) when the decompilation failed or was cancelled.</returns>
		public async Task<DecompiledScript?> DecompileAsync(String path, OpenDocumentOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(options);

			if (options.ClearBuildLog)
			{
				this._output.SetBuildText(String.Empty);
			}

			this._output.ShowTab(OutputTab.Build);
			Progress<String> progress = new Progress<String>(line => this._output.AppendBuildLine(line));

			// A round trip hands over the token of its own operation; opening a compiled script is an operation of its own that the user can cancel.
			IOperation? own = options.CancellationToken.CanBeCanceled ? null : this._operations.TryBegin(OperationKind.Decompile, Path.GetFileName(path));
			CancellationToken token = own?.Token ?? options.CancellationToken;
			DecompilationResult result;
			try
			{
				result = await this._compiler.DecompileAsync(
					new DecompilationRequest
					{
						ScriptPath = path,
						Options = this._settings.Effective.ToCompilerOptions(this._settings.Paths),
						TemporaryOutput = options.TemporaryDecompile,
					},
					progress,
					token);
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				if (own is not null)
				{
					this._output.ReportStatus(Loc.Format("Str.Shell.Operation.DecompileCancelled", Path.GetFileName(path)));
				}

				return null;
			}
			finally
			{
				// The decompiler is done: choosing where to save the source is not part of the cancellable operation.
				own?.Dispose();
			}

			if (!result.Success || result.OutputFilePath is null || !File.Exists(result.OutputFilePath))
			{
				foreach (ToolFailure failure in result.ToolFailures)
				{
					this._problems.Report(ProblemFactory.FromToolFailure(failure));
				}

				this._problems.Report(ProblemFactory.DecompileFailed(path));
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.DecompileErrorTitle"), Loc.Format("Str.Shell.Dlg.DecompileFailed", path));
				return null;
			}

			DecodedText decoded = await this._files.ReadAsync(result.OutputFilePath);
			String text = new ProcedureBeginFixer().FixText(decoded.Text);

			String? target = null;
			if (!options.TemporaryDecompile)
			{
				target = this._dialogs.PickSaveFile(
					Loc.Get("Str.Shell.Dlg.SaveDecompiledTitle"),
					Loc.Get("Str.Shell.Filter.Script"),
					Path.GetDirectoryName(path),
					Path.GetFileNameWithoutExtension(path) + ".ssl");
			}

			return new DecompiledScript(text, decoded.Encoding, result.OutputFilePath, target);
		}

		/// <summary>Writes the decompiled source to the file the user chose (an error is shown when it fails).</summary>
		/// <param name="script">The decompiled script.</param>
		/// <returns><see langword="true"/> when the file was written; <see langword="false"/> when the script has no target or the write failed.</returns>
		public Task<Boolean> SaveTargetAsync(DecompiledScript script)
		{
			ArgumentNullException.ThrowIfNull(script);
			return script.TargetPath is null ? Task.FromResult(false) : this._files.WriteTextAsync(script.TargetPath, script.Text, script.Encoding);
		}
	}
}
