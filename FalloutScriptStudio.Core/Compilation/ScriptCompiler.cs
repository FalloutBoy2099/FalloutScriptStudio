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
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.IO;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// <see cref="IScriptCompiler"/> implementation that drives <c>compile.exe</c> (sslc), the external preprocessors,
	/// the user batch file; decompilation is delegated to an <see cref="IScriptDecompiler"/>.
	/// </summary>
	public sealed class ScriptCompiler : IScriptCompiler
	{
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

			if (!request.Batch)
			{
				output.Append("****** ")
					.Append(this._timeProvider.GetLocalNow().ToString("HH:mm:ss", CultureInfo.InvariantCulture))
					.Append(" ******").Append(LineBreak)
					.Append('-', 22);
			}

			Directory.CreateDirectory(options.ScratchDirectory);

			Boolean success;
			if (options.UseUserCommand && !request.PreprocessOnly)
			{
				success = await this.RunToolAsync(
					Path.Combine(options.ResourcesDirectory, "usercomp.bat"),
					commandLines.BuildUserCompiler(sourceFile, sourceDirectory, request.ShortCircuit),
					options.ResourcesDirectory,
					TextEncodings.Ansi.CodePage == 1251 ? TextEncodings.Oem866 : null,
					output,
					request,
					cancellationToken).ConfigureAwait(false);
			}
			else
			{
				success = await this.RunCompilationChainAsync(request, sourceFile, sourceDirectory, paths, commandLines, output, cancellationToken).ConfigureAwait(false);
			}

			String? preprocessedCopy = null;
			if (request.PreprocessOnly && success && !request.Batch)
			{
				preprocessedCopy = MovePreprocessedFile(paths, sourceFile);
			}

			String? producedFile = null;
			if (success && !request.PreprocessOnly && !options.UseUserCommand && TryGetOutputPath(paths, sourceFile, sourceDirectory, request, out String? path))
			{
				producedFile = path;
			}

			IReadOnlyList<BuildError> errors = options.UseUserCommand
				? Array.Empty<BuildError>()
				: this._buildLogParser.Parse(output.ToString(), sourceFile, options.Preprocessor);

			if (this._logger.IsEnabled(LogLevel.Information))
			{
				this._logger.LogInformation(
					"{Operation} of {Script} {Outcome} ({ErrorCount} log entries)",
					request.PreprocessOnly ? "Preprocessing" : "Compilation",
					sourceFile,
					success ? "succeeded" : "failed",
					errors.Count);
			}

			return new CompilationResult
			{
				Success = success,
				Output = output.ToString(),
				Errors = errors,
				OutputFilePath = producedFile,
				PreprocessedFilePath = preprocessedCopy,
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
			CancellationToken cancellationToken)
		{
			CompilerOptions options = request.Options;
			String inputFile = sourceFile;
			Boolean external = options.Preprocessor != PreprocessorKind.Builtin;

			if (external)
			{
				// A common preprocess file is used for interactive runs, a random one for parallel batch runs.
				String outputName = request.Batch ? Path.GetRandomFileName() : Path.GetFileName(paths.PreprocessFile);
				String preprocessed = Path.Combine(options.ScratchDirectory, outputName);

				Boolean preprocessSucceeded = await this.RunExternalPreprocessorAsync(request, sourceFile, outputName, commandLines, output, cancellationToken).ConfigureAwait(false);
				if (!preprocessSucceeded || request.PreprocessOnly)
				{
					return preprocessSucceeded;
				}

				// Rename preprocess.ssl to <scriptname>_[pre].ssl
				inputFile = paths.GetIntermediatePreprocessedPath(sourceFile);
				try
				{
					File.Delete(inputFile);
					File.Move(preprocessed, inputFile);
				}
				catch (IOException ex)
				{
					this._logger.LogError(ex, "The preprocessed file {File} could not be moved", preprocessed);
					output.Append(LineBreak).Append("[Error] <none>:0: The preprocessed file is missing: ").Append(ex.Message);
					return false;
				}
			}

			return await this.RunSslcAsync(request, inputFile, sourceDirectory, paths, commandLines, output, cancellationToken).ConfigureAwait(false);
		}

		private async Task<Boolean> RunExternalPreprocessorAsync(
			CompilationRequest request,
			String sourceFile,
			String outputName,
			CompilerCommandLineBuilder commandLines,
			StringBuilder output,
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
			File.Delete(Path.Combine(options.ScratchDirectory, outputName));

			Boolean success = await this.RunToolAsync(
				Path.Combine(options.ResourcesDirectory, watcom ? "wcc.bat" : "mcpp.bat"),
				commandLines.BuildExternalPreprocessor(sourceFile, outputName, request.PreprocessOnly),
				options.ResourcesDirectory,
				null,
				output,
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

			String? backupFile = request.PreprocessOnly || request.TemporaryOutput ? null : this.CreateBackup(outputFile, options);

			Boolean success = false;
			try
			{
				success = await this.RunToolAsync(
					Path.Combine(options.ResourcesDirectory, "compile.exe"),
					commandLines.BuildSslc(inputFile, outputFile, request.PreprocessOnly, request.ShortCircuit),
					Path.GetDirectoryName(inputFile) ?? String.Empty,
					null,
					output,
					request,
					cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				this.FinishBackup(backupFile, outputFile, success);
			}

			return success;
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
				return backupFile;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogWarning(ex, "The previous {File} could not be backed up", outputFile);
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
				this._logger.LogWarning(ex, "The previous {File} could not be restored from {Backup}", outputFile, backupFile);
			}
		}

		private async Task<Boolean> RunToolAsync(
			String toolPath,
			String arguments,
			String workingDirectory,
			Encoding? outputEncoding,
			StringBuilder output,
			CompilationRequest request,
			CancellationToken cancellationToken)
		{
			if (!File.Exists(toolPath))
			{
				this._logger.LogWarning("Required tool {Tool} was not found", toolPath);
				output.Append(Environment.NewLine).Append("[Error] <none>:0: Required tool not found: ").Append(toolPath).Append(Environment.NewLine);
				return false;
			}

			ProcessRunResult result;
			try
			{
				if (this._logger.IsEnabled(LogLevel.Debug))
				{
					this._logger.LogDebug("Starting {Tool} {Arguments}", toolPath, arguments);
				}

				result = await this._processRunner.RunAsync(
					new ProcessRunRequest
					{
						FileName = toolPath,
						Arguments = arguments,
						WorkingDirectory = workingDirectory,
						OutputEncoding = outputEncoding,
					},
					cancellationToken).ConfigureAwait(false);
			}
			catch (Win32Exception ex)
			{
				this._logger.LogError(ex, "Failed to start {Tool}", toolPath);
				output.Append(Environment.NewLine).Append("[Error] <none>:0: Failed to start ").Append(toolPath).Append(": ").Append(ex.Message).Append(Environment.NewLine);
				return false;
			}

			output.Append(Environment.NewLine).Append(result.Output);
			if (!request.Batch && request.Options.Preprocessor != PreprocessorKind.Builtin)
			{
				output.Append(await ReadErrorLogAsync(request.Options, cancellationToken).ConfigureAwait(false));
			}

			return result.ExitCode == 0;
		}

		private static async Task<String> ReadErrorLogAsync(CompilerOptions options, CancellationToken cancellationToken)
		{
			String file = Path.Combine(options.ResourcesDirectory, options.Preprocessor == PreprocessorKind.Mcpp ? "mcpp.err" : "wcc.err");
			if (!File.Exists(file))
			{
				return String.Empty;
			}

			String text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
			File.Delete(file);
			return text;
		}

		private static String? MovePreprocessedFile(CompilerPaths paths, String sourceFile)
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
	}
}
