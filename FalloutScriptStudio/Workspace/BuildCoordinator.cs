namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>
	/// Runs the build commands of the main window: compile, preprocess, round trip, compile all open scripts and mass compile.
	/// Results are stored in the documents (<see cref="DocumentViewModel.BuildLog"/>, <see cref="DocumentViewModel.BuildErrors"/>)
	/// and announced through the output panel, the status bar, a toast and a sound; tools that are missing or cannot be started become
	/// <see cref="Problem"/>s with a fix hint. Every command is one cancellable operation of the <see cref="IOperationTracker"/>: only one
	/// runs at a time, the status bar shows what it does, and <see cref="Cancel"/> stops it (the tool processes are killed and the files
	/// they were writing are cleaned up or restored).
	/// </summary>
	public sealed class BuildCoordinator
	{
		private readonly ISettingsService _settings;
		private readonly IScriptCompiler _compiler;
		private readonly IBatchCompileService _batch;
		private readonly IWorkspaceManager _workspace;
		private readonly ParseCoordinator _parser;
		private readonly IDialogService _dialogs;
		private readonly IOutputSink _output;
		private readonly IShellNotificationService _notifications;
		private readonly ISoundService _sounds;
		private readonly IToolWindowService _toolWindows;
		private readonly IOperationTracker _operations;
		private readonly IProblemService _problems;
		private Boolean _busy;

		/// <summary>Initializes the coordinator.</summary>
		public BuildCoordinator(
			ISettingsService settings,
			IScriptCompiler compiler,
			IBatchCompileService batch,
			IWorkspaceManager workspace,
			ParseCoordinator parser,
			IDialogService dialogs,
			IOutputSink output,
			IShellNotificationService notifications,
			ISoundService sounds,
			IToolWindowService toolWindows,
			IOperationTracker? operations = null,
			IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(compiler);
			ArgumentNullException.ThrowIfNull(batch);
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(output);
			ArgumentNullException.ThrowIfNull(notifications);
			ArgumentNullException.ThrowIfNull(sounds);
			ArgumentNullException.ThrowIfNull(toolWindows);
			this._settings = settings;
			this._compiler = compiler;
			this._batch = batch;
			this._workspace = workspace;
			this._parser = parser;
			this._dialogs = dialogs;
			this._output = output;
			this._notifications = notifications;
			this._sounds = sounds;
			this._toolWindows = toolWindows;
			this._operations = operations ?? new OperationTracker();
			this._problems = problems ?? new ProblemService();
			this._operations.Changed += (_, _) => this.UpdateBusy();
		}

		/// <summary>Raised when <see cref="IsBusy"/> changed.</summary>
		public event EventHandler? BusyChanged;

		/// <summary>Gets a value indicating whether a long operation (a build command or a decompilation) is running.</summary>
		public Boolean IsBusy => this._operations.IsRunning;

		/// <summary>Gets the tracker of the running operation, for the status bar.</summary>
		public IOperationTracker Operations => this._operations;

		/// <summary>Cancels the running operation, if any.</summary>
		/// <returns><see langword="true"/> when an operation was asked to stop.</returns>
		public Boolean Cancel()
		{
			return this._operations.Cancel();
		}

		/// <summary>Compiles a script (menu "Compile Script", F8).</summary>
		public async Task<Boolean> CompileAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			using IOperation? operation = this.Begin(OperationKind.Compile, document.FileName, 0);
			if (operation is null)
			{
				return false;
			}

			try
			{
				this.PrepareBuild(document);
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, true, false, false), operation.Token);
				if (outcome.Success)
				{
					document.ParserErrors = Array.Empty<BuildError>();
				}

				return outcome.Success;
			}
			catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
			{
				this.ReportCancelled(document, "Str.Shell.Build.CompileCancelled");
				return false;
			}
		}

		/// <summary>Compiles every open script (menu "Compile All Open").</summary>
		public async Task<Boolean> CompileAllOpenAsync()
		{
			List<DocumentViewModel> scripts = this._workspace.Documents.Where(document => document.Kind == DocumentKind.Script).ToList();
			using IOperation? operation = this.Begin(OperationKind.CompileAll, String.Empty, scripts.Count);
			if (operation is null)
			{
				return false;
			}

			this._output.ClearErrors();
			this._problems.ClearOrigin(ProblemOrigins.BuildTool);
			StringBuilder full = new StringBuilder();
			List<BuildError> allErrors = new List<BuildError>();
			Int32 compiled = 0;
			Int32 attempted = 0;
			Boolean cancelled = false;
			try
			{
				foreach (DocumentViewModel document in scripts)
				{
					operation.Token.ThrowIfCancellationRequested();
					if (!this._workspace.Documents.Contains(document))
					{
						continue;
					}

					attempted++;
					operation.Report(document.FileName, attempted, scripts.Count);
					this._problems.ClearOwner(document, ProblemSource.Build, ProblemSource.Settings);
					BuildOutcome outcome = await this.RunAsync(document, new BuildMode(false, false, false, false), operation.Token);
					if (outcome.Success)
					{
						compiled++;
					}

					foreach (BuildErrorType type in new[] { BuildErrorType.Error, BuildErrorType.Warning, BuildErrorType.Message })
					{
						allErrors.AddRange(outcome.Errors.Where(error => error.Type == type));
					}

					full.AppendLine(document.BuildLog).AppendLine();
				}
			}
			catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
			{
				cancelled = true;
			}

			// Like the legacy editor, the error list holds the errors of every compiled script, not only those of the active one.
			this._output.ShowBuildErrors(allErrors);
			this._output.SetBuildText(full.ToString());
			if (cancelled)
			{
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.CompileAllCancelled", compiled, scripts.Count));
				return false;
			}

			this._output.ReportStatus(Loc.Format("Str.Shell.Build.CompiledAll", compiled, attempted));
			if (attempted > 0 && compiled == attempted)
			{
				this._notifications.Show(Loc.Get("Str.Shell.Build.CompiledTitle"), Loc.Format("Str.Shell.Build.AllCompiled", attempted), NotificationKind.Success);
			}

			return compiled == attempted;
		}

		/// <summary>Preprocesses a script and opens the preprocessed copy (menu "Preprocess", F4).</summary>
		public async Task<Boolean> PreprocessAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			using IOperation? operation = this.Begin(OperationKind.Preprocess, document.FileName, 0);
			if (operation is null)
			{
				return false;
			}

			try
			{
				this.PrepareBuild(document);
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, false, true, false), operation.Token);
				if (!outcome.Success)
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Menu.Preprocess"), Loc.Get("Str.Shell.Build.PreprocessFailed"));
					return false;
				}

				if (outcome.Result?.PreprocessedFilePath is not { } file || !File.Exists(file))
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Menu.Preprocess"), Loc.Get("Str.Shell.Build.PreprocessedMissing"));
					return false;
				}

				await this._workspace.OpenAsync(file, new OpenDocumentOptions { AddToRecent = false });
				return true;
			}
			catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
			{
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.PreprocessCancelled", document.FileName));
				return false;
			}
		}

		/// <summary>Compiles a script, decompiles the result and opens it (menu "Roundtrip", F5).</summary>
		public async Task<Boolean> RoundTripAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this.IsBusy)
			{
				this.ReportBusy();
				return false;
			}

			if (this._settings.Settings.Compiler.UseUserCommand)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Menu.Roundtrip"), Loc.Get("Str.Shell.Build.RoundtripUserCommand"));
				return false;
			}

			using IOperation? operation = this.Begin(OperationKind.RoundTrip, document.FileName, 0);
			if (operation is null)
			{
				return false;
			}

			try
			{
				this.PrepareBuild(document);
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, false, false, true), operation.Token);
				if (!outcome.Success || document.FilePath is null)
				{
					return false;
				}

				CompilerOptions options = this._settings.Effective.ToCompilerOptions(this._settings.Paths);
				String compiled = this._compiler.GetOutputPath(document.FilePath, options, true);
				DocumentViewModel? decompiled = await this._workspace.OpenAsync(
					compiled,
					new OpenDocumentOptions { AddToRecent = false, TemporaryDecompile = true, ClearBuildLog = false, CancellationToken = operation.Token });
				operation.Token.ThrowIfCancellationRequested();
				return decompiled is not null;
			}
			catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
			{
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.RoundTripCancelled", document.FileName));
				return false;
			}
		}

		/// <summary>Compiles all scripts of the project folder (or a folder chosen by the user) in a batch.</summary>
		public async Task MassCompileAsync()
		{
			if (this.IsBusy)
			{
				this.ReportBusy();
				return;
			}

			PathSettings paths = this._settings.Effective.Settings.Paths;
			if (paths.OutputDirectory is null)
			{
				this._problems.Report(ProblemFactory.OutputFolderNotSet(null));
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Build.MassCompileTitle"), Loc.Get("Str.Shell.Build.ScriptsFolderNotSet"));
				return;
			}

			String? folder = paths.SolutionFolder;
			if (folder is null || !Directory.Exists(folder))
			{
				folder = this._dialogs.PickFolder(Loc.Get("Str.Shell.Build.SelectMassFolder"), paths.LastMassCompileDirectory);
				if (folder is null)
				{
					return;
				}

				this._settings.Update(settings => settings.Paths.LastMassCompileDirectory = folder);
			}

			IReadOnlyList<String> scripts = this._batch.FindScripts(folder);
			if (scripts.Count == 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Build.MassCompileTitle"), Loc.Format("Str.Shell.Build.NoScripts", folder));
				return;
			}

			using IOperation? operation = this.Begin(OperationKind.MassCompile, String.Empty, scripts.Count);
			if (operation is null)
			{
				return;
			}

			this._problems.ClearOrigin(ProblemOrigins.MassCompile);
			this._problems.ClearOrigin(ProblemOrigins.BuildTool);
			Progress<BatchCompileProgress> progress = new Progress<BatchCompileProgress>(
				item => operation.Report(Path.GetFileName(item.ScriptPath), item.Completed, item.Total));
			BatchCompileResult result = await this._toolWindows.RunBatchCompileAsync(scripts, progress, operation.Token);
			this.ReportBatchFailures(result);
			this._output.ReportStatus(result.Cancelled
				? Loc.Format("Str.Shell.Build.MassCompileCancelled", result.Compiled, result.Found, result.Failed)
				: Loc.Format("Str.Shell.Build.MassCompileStatus", result.Compiled, result.Found, result.Failed));
		}

		private void UpdateBusy()
		{
			Boolean busy = this._operations.IsRunning;
			if (busy != this._busy)
			{
				this._busy = busy;
				this.BusyChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private IOperation? Begin(OperationKind kind, String subject, Int32 total)
		{
			IOperation? operation = this._operations.TryBegin(kind, subject, total);
			if (operation is null)
			{
				this.ReportBusy();
			}

			return operation;
		}

		private void ReportBusy()
		{
			this._output.ReportStatus(Loc.Get("Str.Shell.Build.AlreadyRunning"));
		}

		private void PrepareBuild(DocumentViewModel document)
		{
			this._output.ClearErrors();
			this._problems.ClearOrigin(ProblemOrigins.BuildTool);
			this._problems.ClearOwner(document, ProblemSource.Build, ProblemSource.Settings);
		}

		private void ReportCancelled(DocumentViewModel document, String statusKey)
		{
			document.BuildLog = Loc.Get("Str.Shell.Build.LogCancelled");
			this._output.ReportStatus(Loc.Format(statusKey, document.FileName));
		}

		private void ReportBatchFailures(BatchCompileResult result)
		{
			CompilerOptions options = this._settings.Effective.ToCompilerOptions(this._settings.Paths);
			BuildLogParser logParser = new BuildLogParser();
			List<Problem> reported = new List<Problem>();
			foreach (BatchCompileFailure failure in result.Failures)
			{
				IReadOnlyList<BuildError> errors = logParser.Parse(failure.Output, failure.ScriptPath, options.Preprocessor);
				List<BuildError> failures = errors.Where(error => error.Type == BuildErrorType.Error).Take(10).ToList();
				if (failures.Count == 0)
				{
					reported.Add(ProblemFactory.BuildFailedWithoutDetails(failure.ScriptPath, false, null) with { Origin = ProblemOrigins.MassCompile });
					continue;
				}

				foreach (BuildError error in failures)
				{
					reported.Add(Problem.FromBuildError(error, ProblemSource.Build) with { Origin = ProblemOrigins.MassCompile });
				}
			}

			this._problems.ReportRange(reported);
		}

		/// <summary>Removes the log entries that only repeat a tool failure: the failure is reported as a problem with a hint instead.</summary>
		private static IReadOnlyList<BuildError> WithoutToolFailureEntries(CompilationResult result)
		{
			if (result.ToolFailures.Count == 0)
			{
				return result.Errors;
			}

			return result.Errors
				.Where(error => !(error.Type == BuildErrorType.Error && result.ToolFailures.Any(failure => error.Message.Contains(failure.LogMessage, StringComparison.Ordinal))))
				.ToList();
		}

		private async Task<BuildOutcome> RunAsync(DocumentViewModel document, BuildMode mode, CancellationToken cancellationToken)
		{
			if (document.Kind != DocumentKind.Script)
			{
				this._problems.Report(ProblemFactory.NotAScript(document.FileName, document));
				if (mode.ShowMessages)
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Build.CompileTitle"), Loc.Get("Str.Shell.Build.NotSsl"));
				}

				return new BuildOutcome(false, null, Array.Empty<BuildError>());
			}

			CompilerSettings compiler = this._settings.Effective.Settings.Compiler;
			if (!mode.Preprocess && !compiler.CompileToSourceDirectory && this._settings.Effective.Settings.Paths.OutputDirectory is null)
			{
				this._problems.Report(ProblemFactory.OutputFolderNotSet(document));
				if (mode.ShowMessages)
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Build.CompileTitle"), Loc.Get("Str.Shell.Build.OutputFolderNotSet"));
				}

				return new BuildOutcome(false, null, Array.Empty<BuildError>());
			}

			if (document.IsModified && !await this._workspace.SaveAsync(document))
			{
				return new BuildOutcome(false, null, Array.Empty<BuildError>());
			}

			if (document.FilePath is null || document.IsModified)
			{
				return new BuildOutcome(false, null, Array.Empty<BuildError>());
			}

			// A parse that was waiting for the typing pause must not compete with the compiler, but it must not be lost either:
			// it runs again when the compilation is over (otherwise the parser data stays old until the next keystroke).
			Boolean parseWasPending = document.NeedsParse || this._parser.IsPending(document);
			this._parser.Cancel(document);
			document.NeedsParse = false;

			CompilationRequest request = new CompilationRequest
			{
				ScriptPath = document.FilePath,
				Options = this._settings.Effective.ToCompilerOptions(this._settings.Paths),
				ShortCircuit = document.ProgramInfo?.ShortCircuitEvaluation ?? false,
				TemporaryOutput = mode.Temporary,
				PreprocessOnly = mode.Preprocess,
			};

			CompilationResult result;
			try
			{
				result = mode.Preprocess
					? await this._compiler.PreprocessAsync(request, cancellationToken)
					: await this._compiler.CompileAsync(request, cancellationToken);
			}
			finally
			{
				if (parseWasPending && document.IsParsable && !this._parser.IsPending(document) && this._workspace.Documents.Contains(document))
				{
					_ = this._parser.ParseAsync(document, true);
				}
			}

			IReadOnlyList<BuildError> errors = WithoutToolFailureEntries(result);
			foreach (ToolFailure failure in result.ToolFailures)
			{
				this._problems.Report(ProblemFactory.FromToolFailure(failure));
			}

			if (!result.Success && result.ToolFailures.Count == 0 && !errors.Any(error => error.Type == BuildErrorType.Error))
			{
				// The tool failed without saying where (a user batch file, for example): the list must still say that something failed.
				this._problems.Report(ProblemFactory.BuildFailedWithoutDetails(document.FilePath, mode.Preprocess, document));
			}

			document.BuildErrors = errors;
			if (mode.Preprocess)
			{
				document.BuildLog = result.Output;
				this._output.ShowTab(OutputTab.Build);
				return new BuildOutcome(result.Success, result, errors);
			}

			if (result.Success)
			{
				document.BuildLog = result.Output + "\r\n " + Loc.Get("Str.Shell.Build.LogSuccess") + "\r\n";
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.CompiledOne", document.FileName, DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
				if (mode.ShowMessages && mode.ShowSuccess)
				{
					this._notifications.Show(Loc.Get("Str.Shell.Build.CompiledTitle"), Loc.Format("Str.Shell.Build.CompiledOk", document.FileName), NotificationKind.Success);
				}
			}
			else
			{
				document.BuildLog = result.Output + "\r\n " + Loc.Get("Str.Shell.Build.LogFailure");
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.CompileFailed", document.FileName));
				this._sounds.PlayCompileFailed();
				if (mode.ShowMessages && compiler.WarnOnFailedCompile)
				{
					this._output.ShowTab(compiler.UseUserCommand && result.ToolFailures.Count == 0 ? OutputTab.Build : OutputTab.Errors);
				}
			}

			return new BuildOutcome(result.Success, result, errors);
		}

		private readonly record struct BuildMode(Boolean ShowMessages, Boolean ShowSuccess, Boolean Preprocess, Boolean Temporary);

		private readonly record struct BuildOutcome(Boolean Success, CompilationResult? Result, IReadOnlyList<BuildError> Errors);
	}
}
