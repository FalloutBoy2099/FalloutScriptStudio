namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Decompilation;
	using IO;

	/// <summary>
	/// <see cref="IScriptCompiler"/> implementation that drives <c>compile.exe</c> (sslc), the external preprocessors,
	/// the user batch file; decompilation is delegated to an <see cref="IScriptDecompiler"/>.
	/// </summary>
	public sealed class ScriptCompiler : IScriptCompiler
	{
		/// <summary>The environment variable that tells the batch files where the scratch folder is (it is not <c>..\scrTemp</c> in the user-profile layout).</summary>
		public const String ScratchDirectoryVariable = "FSS_SCRATCH";

		private const String LineBreak = "\r\n";

		private readonly IProcessRunner _processRunner;
		private readonly ILogger<ScriptCompiler> _logger;
		private readonly TimeProvider _timeProvider;
		private readonly IScriptDecompiler _decompiler;
		private readonly BuildLogParser _buildLogParser = new BuildLogParser();

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptCompiler"/> class.
		/// </summary>
		/// <param name="processRunner">Starts the external tools.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="timeProvider">The clock used for the build log header; the system clock when omitted.</param>
		/// <param name="decompiler">Decompiles the <c>.int</c> files; when omitted, a <see cref="ScriptDecompilerSelector"/> over the built-in
		/// decompiler and <see cref="ExternalInt2SslDecompiler"/> (which uses <paramref name="processRunner"/>) is used.</param>
		public ScriptCompiler(IProcessRunner processRunner, ILogger<ScriptCompiler> logger, TimeProvider? timeProvider = null, IScriptDecompiler? decompiler = null)
		{
			ArgumentNullException.ThrowIfNull(processRunner);
			ArgumentNullException.ThrowIfNull(logger);
			this._processRunner = processRunner;
			this._logger = logger;
			this._timeProvider = timeProvider ?? TimeProvider.System;
			this._decompiler = decompiler ?? new ScriptDecompilerSelector(
				new BuiltInScriptDecompiler(),
				new ExternalInt2SslDecompiler(processRunner, NullLogger<ExternalInt2SslDecompiler>.Instance));
		}

		/// <inheritdoc/>
		public Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);
			return this.CompileAsync(request with { PreprocessOnly = true }, cancellationToken);
		}

		/// <inheritdoc/>
		public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
		{
			ArgumentNullException.ThrowIfNull(options);
			return new CompilerPaths(options).GetOutputPath(Path.GetFullPath(scriptPath), String.Empty, temporaryOutput);
		}

		/// <inheritdoc/>
		public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);
			if (String.IsNullOrWhiteSpace(request.ScriptPath))
			{
				return new CompilationResult { Success = false, Output = "No filename specified" };
			}

			CompilerOptions options = request.Options;
			CompilerPaths paths = new CompilerPaths(options);
			CompilerCommandLineBuilder commandLines = new CompilerCommandLineBuilder(options);
			String sourceFile = Path.GetFullPath(request.ScriptPath);
			String sourceDirectory = Path.GetDirectoryName(sourceFile) ?? String.Empty;
			StringBuilder output = new StringBuilder();
			List<ToolFailure> toolFailures = [];

			if (!request.Batch)
			{
				output.Append("****** ")
					.Append(this._timeProvider.GetLocalNow().ToString("HH:mm:ss", CultureInfo.InvariantCulture))
					.Append(" ******").Append(LineBreak)
					.Append('-', 22);
			}

			try
			{
				Directory.CreateDirectory(options.ScratchDirectory);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.ScratchFolderNotCreated(ex, options.ScratchDirectory);
				output.Append(LineBreak).Append("[Error] <none>:0: The scratch folder could not be created: ").Append(ex.Message);
				return new CompilationResult { Success = false, Output = output.ToString() };
			}

			Boolean success;
			if (options.UseUserCommand && !request.PreprocessOnly)
			{
				// The batch file is run by cmd.exe, which prints in the OEM code page of the system.
				success = await this.RunToolAsync(
					Path.Combine(options.ResourcesDirectory, "usercomp.bat"),
					commandLines.BuildUserCompiler(sourceFile, sourceDirectory, request.ShortCircuit),
					options.ResourcesDirectory,
					TextEncodings.Oem,
					output,
					toolFailures,
					request,
					cancellationToken).ConfigureAwait(false);
			}
			else
			{
				success = await this.RunCompilationChainAsync(request, sourceFile, sourceDirectory, paths, commandLines, output, toolFailures, cancellationToken).ConfigureAwait(false);
			}

			String? preprocessedCopy = null;
			if (request.PreprocessOnly && success && !request.Batch)
			{
				preprocessedCopy = this.MovePreprocessedFile(paths, sourceFile);
			}

			String? producedFile = null;
			if (success && !request.PreprocessOnly && !options.UseUserCommand && TryGetOutputPath(paths, sourceFile, sourceDirectory, request, out String? path))
			{
				producedFile = path;
			}

			IReadOnlyList<BuildError> errors = options.UseUserCommand
				? Array.Empty<BuildError>()
				: this._buildLogParser.Parse(output.ToString(), sourceFile, options.Preprocessor);

			this._logger.Finished(request.PreprocessOnly ? "Preprocessing" : "Compilation", sourceFile, success ? "succeeded" : "failed", errors.Count);

			return new CompilationResult
			{
				Success = success,
				Output = output.ToString(),
				Errors = errors,
				OutputFilePath = producedFile,
				PreprocessedFilePath = preprocessedCopy,
				ToolFailures = toolFailures,
			};
		}

		/// <inheritdoc/>
		public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);
			return this._decompiler.DecompileAsync(request, outputProgress, cancellationToken);
		}

		private static Boolean TryGetOutputPath(CompilerPaths paths, String sourceFile, String sourceDirectory, CompilationRequest request, out String? path)
		{
			try
			{
				path = request.Options.Preprocessor == PreprocessorKind.Builtin
					? paths.GetOutputPath(sourceFile, sourceDirectory, request.TemporaryOutput)
					: paths.GetOutputPath(paths.GetIntermediatePreprocessedPath(sourceFile), sourceDirectory, request.TemporaryOutput);
				return true;
			}
			catch (InvalidOperationException)
			{
				path = null;
				return false;
			}
		}

		private async Task<Boolean> RunCompilationChainAsync(
			CompilationRequest request,
			String sourceFile,
			String sourceDirectory,
			CompilerPaths paths,
			CompilerCommandLineBuilder commandLines,
			StringBuilder output,
			List<ToolFailure> toolFailures,
			CancellationToken cancellationToken)
		{
			CompilerOptions options = request.Options;
			String inputFile = sourceFile;
			Boolean external = options.Preprocessor != PreprocessorKind.Builtin;
			String? preprocessed = null;

			try
			{
				if (external)
				{
					// A common preprocess file is used for interactive runs, a random one for parallel batch runs.
					String outputName = request.Batch ? Path.GetRandomFileName() : Path.GetFileName(paths.PreprocessFile);
					preprocessed = Path.Combine(options.ScratchDirectory, outputName);

					Boolean preprocessSucceeded = await this.RunExternalPreprocessorAsync(request, sourceFile, outputName, commandLines, output, toolFailures, cancellationToken).ConfigureAwait(false);
					if (!preprocessSucceeded || request.PreprocessOnly)
					{
						return preprocessSucceeded;
					}

					// Rename preprocess.ssl to <scriptname>_[pre]~<hash of the full path>.ssl (two scripts of the same name must not share the file)
					inputFile = paths.GetIntermediatePreprocessedPath(sourceFile);
					try
					{
						File.Delete(inputFile);
						File.Move(preprocessed, inputFile);
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						this._logger.PreprocessedNotMoved(ex, preprocessed);
						output.Append(LineBreak).Append("[Error] <none>:0: The preprocessed file is missing: ").Append(ex.Message);
						return false;
					}
				}

				return await this.RunSslcAsync(request, inputFile, sourceDirectory, paths, commandLines, output, toolFailures, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// A cancelled run must not leave half-written intermediate files in the scratch folder.
				if (external)
				{
					this.TryDelete(preprocessed);
					this.TryDelete(paths.GetIntermediatePreprocessedPath(sourceFile));
					if (!request.Batch)
					{
						this.TryDelete(Path.Combine(options.ResourcesDirectory, GetErrorLogName(options)));
					}
				}

				throw;
			}
		}

		private async Task<Boolean> RunExternalPreprocessorAsync(
			CompilationRequest request,
			String sourceFile,
			String outputName,
			CompilerCommandLineBuilder commandLines,
			StringBuilder output,
			List<ToolFailure> toolFailures,
			CancellationToken cancellationToken)
		{
			CompilerOptions options = request.Options;
			Boolean watcom = options.Preprocessor == PreprocessorKind.Watcom;

			if (!request.Batch)
			{
				output.Append(Environment.NewLine)
					.Append(watcom ? "Open Watcom C32 preprocessing script: " : "External MCPP preprocessing script: ")
					.Append(Path.GetFileName(sourceFile)).Append(Environment.NewLine);
				output.Append("Predefine: ").Append(options.PreprocessorDefine ?? String.Empty).Append(Environment.NewLine);
			}

			// A stale result must never be mistaken for the output of this run.
			try
			{
				File.Delete(Path.Combine(options.ScratchDirectory, outputName));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.PreviousPreprocessedNotDeleted(ex, outputName);
				output.Append(LineBreak).Append("[Error] <none>:0: The previous preprocessed file could not be deleted: ").Append(ex.Message);
				return false;
			}

			Boolean success = await this.RunToolAsync(
				Path.Combine(options.ResourcesDirectory, watcom ? "wcc.bat" : "mcpp.bat"),
				commandLines.BuildExternalPreprocessor(sourceFile, outputName, request.PreprocessOnly),
				options.ResourcesDirectory,
				null,
				output,
				toolFailures,
				request,
				cancellationToken).ConfigureAwait(false);

			if (!request.Batch)
			{
				output.Append('-', 22).Append(Environment.NewLine);
				if (success)
				{
					output.Append("Created preprocessing file: OK").Append(LineBreak);
					output.Append("[Done] Preprocessing script successfully completed.").Append(LineBreak);
				}
				else
				{
					output.Append("[Error] Preprocessing script failed...");
				}
			}

			return success;
		}

		private async Task<Boolean> RunSslcAsync(
			CompilationRequest request,
			String inputFile,
			String sourceDirectory,
			CompilerPaths paths,
			CompilerCommandLineBuilder commandLines,
			StringBuilder output,
			List<ToolFailure> toolFailures,
			CancellationToken cancellationToken)
		{
			CompilerOptions options = request.Options;
			String outputFile;
			try
			{
				outputFile = request.PreprocessOnly
					? paths.PreprocessFile
					: paths.GetOutputPath(inputFile, sourceDirectory, request.TemporaryOutput);
			}
			catch (InvalidOperationException ex)
			{
				output.Append(Environment.NewLine).Append("[Error] <none>:0: ").Append(ex.Message);
				return false;
			}

			Boolean existedBefore = File.Exists(outputFile);
			String? backupFile = request.PreprocessOnly || request.TemporaryOutput ? null : this.CreateBackup(outputFile, options);

			Boolean success = false;
			Boolean cancelled = false;
			try
			{
				success = await this.RunToolAsync(
					Path.Combine(options.ResourcesDirectory, "compile.exe"),
					commandLines.BuildSslc(inputFile, outputFile, request.PreprocessOnly, request.ShortCircuit),
					Path.GetDirectoryName(inputFile) ?? String.Empty,
					null,
					output,
					toolFailures,
					request,
					cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				cancelled = true;
				throw;
			}
			finally
			{
				this.FinishBackup(backupFile, outputFile, success);

				// The previous compiled script came back above. When there was none (or the output is a scratch file) the half-written
				// result of the killed compiler is deleted, otherwise it would look like a compiled script.
				if (cancelled && (request.PreprocessOnly || request.TemporaryOutput || (backupFile is null && !existedBefore)))
				{
					this.TryDelete(outputFile);
				}
			}

			return success;
		}

		private void TryDelete(String? file)
		{
			if (file is null)
			{
				return;
			}

			try
			{
				File.Delete(file);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LeftoverNotDeleted(ex, file);
			}
		}

		private String? CreateBackup(String outputFile, CompilerOptions options)
		{
			if (!File.Exists(outputFile))
			{
				return null;
			}

			String backupFile = Path.Combine(options.ScratchDirectory, Path.GetRandomFileName());
			try
			{
				File.Copy(outputFile, backupFile, true);

				// The copy keeps the read-only attribute of the script, which would make it impossible to delete or to clean up later.
				File.SetAttributes(backupFile, FileAttributes.Normal);
				return backupFile;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.BackupFailed(ex, outputFile);
				return null;
			}
		}

		private void FinishBackup(String? backupFile, String outputFile, Boolean success)
		{
			if (backupFile == null)
			{
				return;
			}

			try
			{
				if (success)
				{
					File.Delete(backupFile);
				}
				else if (File.Exists(backupFile))
				{
					// The compilation failed, so bring back the previous compiled script.
					File.Delete(outputFile);
					File.Move(backupFile, outputFile);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.RestoreFailed(ex, outputFile, backupFile);
			}
		}

		private async Task<Boolean> RunToolAsync(
			String toolPath,
			String arguments,
			String workingDirectory,
			Encoding? outputEncoding,
			StringBuilder output,
			List<ToolFailure> toolFailures,
			CompilationRequest request,
			CancellationToken cancellationToken)
		{
			if (!File.Exists(toolPath))
			{
				this._logger.ToolNotFound(toolPath);
				ToolFailure missing = new ToolFailure(ToolFailureKind.NotFound, toolPath);
				toolFailures.Add(missing);
				output.Append(Environment.NewLine).Append("[Error] <none>:0: ").Append(missing.LogMessage).Append(Environment.NewLine);
				return false;
			}

			ProcessRunResult result;
			try
			{
				if (this._logger.IsEnabled(LogLevel.Debug))
				{
					this._logger.ToolStarting(toolPath, arguments);
				}

				result = await this._processRunner.RunAsync(
					new ProcessRunRequest
					{
						FileName = toolPath,
						Arguments = arguments,
						WorkingDirectory = workingDirectory,
						OutputEncoding = outputEncoding,
						Environment = new Dictionary<String, String>
						{
							[ScratchDirectoryVariable] = Path.GetFullPath(request.Options.ScratchDirectory),
						},
					},
					cancellationToken).ConfigureAwait(false);
			}
			catch (Win32Exception ex)
			{
				this._logger.ToolStartFailed(ex, toolPath);
				ToolFailure notStarted = new ToolFailure(ToolFailureKind.StartFailed, toolPath, ex.Message);
				toolFailures.Add(notStarted);
				output.Append(Environment.NewLine).Append("[Error] <none>:0: ").Append(notStarted.LogMessage).Append(Environment.NewLine);
				return false;
			}

			output.Append(Environment.NewLine).Append(result.Output);
			if (!request.Batch && request.Options.Preprocessor != PreprocessorKind.Builtin)
			{
				output.Append(await ReadErrorLogAsync(request.Options, cancellationToken).ConfigureAwait(false));
			}

			return result.ExitCode == 0;
		}

		private static String GetErrorLogName(CompilerOptions options)
		{
			return options.Preprocessor == PreprocessorKind.Mcpp ? "mcpp.err" : "wcc.err";
		}

		private static async Task<String> ReadErrorLogAsync(CompilerOptions options, CancellationToken cancellationToken)
		{
			String file = Path.Combine(options.ResourcesDirectory, GetErrorLogName(options));
			try
			{
				if (!File.Exists(file))
				{
					return String.Empty;
				}

				// mcpp and wcc write their log in the ANSI code page of the system, like the rest of the chain: as UTF-8 an accented path is damaged
				// and the entries no longer lead to the script.
				String text = await File.ReadAllTextAsync(file, TextEncodings.Ansi, cancellationToken).ConfigureAwait(false);
				File.Delete(file);
				return text;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return String.Empty;
			}
		}

		private String? MovePreprocessedFile(CompilerPaths paths, String sourceFile)
		{
			try
			{
				if (!File.Exists(paths.PreprocessFile))
				{
					return null;
				}

				String target = paths.GetPreprocessedCopyPath(sourceFile);
				File.Delete(target);
				File.Move(paths.PreprocessFile, target);
				return target;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.FinalNameNotApplied(ex, sourceFile);
				return null;
			}
		}
	}
}