namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>
	/// Runs the build commands of the main window: compile, preprocess, round trip, compile all open scripts and mass compile.
	/// Results are stored in the documents (<see cref="DocumentViewModel.BuildLog"/>, <see cref="DocumentViewModel.BuildErrors"/>)
	/// and announced through the output panel, the status bar, a toast and a sound.
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
			IToolWindowService toolWindows)
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
		}

		/// <summary>Raised when <see cref="IsBusy"/> changed.</summary>
		public event EventHandler? BusyChanged;

		/// <summary>Gets a value indicating whether a build command is running.</summary>
		public Boolean IsBusy
		{
			get => this._busy;
			private set
			{
				if (this._busy != value)
				{
					this._busy = value;
					this.BusyChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Compiles a script (menu "Compile Script", F8).</summary>
		public async Task<Boolean> CompileAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this.IsBusy)
			{
				return false;
			}

			this.IsBusy = true;
			try
			{
				this._output.ClearErrors();
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, true, false, false));
				if (outcome.Success)
				{
					document.ParserErrors = Array.Empty<BuildError>();
				}

				return outcome.Success;
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Compiles every open script (menu "Compile All Open").</summary>
		public async Task<Boolean> CompileAllOpenAsync()
		{
			if (this.IsBusy)
			{
				return false;
			}

			this.IsBusy = true;
			try
			{
				this._output.ClearErrors();
				StringBuilder full = new StringBuilder();
				List<BuildError> allErrors = new List<BuildError>();
				Int32 compiled = 0;
				Int32 total = 0;
				foreach (DocumentViewModel document in this._workspace.Documents.ToList())
				{
					if (document.Kind != DocumentKind.Script)
					{
						continue;
					}

					total++;
					BuildOutcome outcome = await this.RunAsync(document, new BuildMode(false, false, false, false));
					if (outcome.Success)
					{
						compiled++;
					}

					if (outcome.Result is not null)
					{
						foreach (BuildErrorType type in new[] { BuildErrorType.Error, BuildErrorType.Warning, BuildErrorType.Message })
						{
							allErrors.AddRange(outcome.Result.Errors.Where(error => error.Type == type));
						}
					}

					full.AppendLine(document.BuildLog).AppendLine();
				}

				// Like the legacy editor, the error list holds the errors of every compiled script, not only those of the active one.
				this._output.ShowBuildErrors(allErrors);
				this._output.SetBuildText(full.ToString());
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.CompiledAll", compiled, total));
				if (total > 0 && compiled == total)
				{
					this._notifications.Show(Loc.Get("Str.Shell.Build.CompiledTitle"), Loc.Format("Str.Shell.Build.AllCompiled", total), NotificationKind.Success);
				}

				return compiled == total;
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Preprocesses a script and opens the preprocessed copy (menu "Preprocess", F4).</summary>
		public async Task<Boolean> PreprocessAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this.IsBusy)
			{
				return false;
			}

			this.IsBusy = true;
			try
			{
				this._output.ClearErrors();
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, false, true, false));
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
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Compiles a script, decompiles the result and opens it (menu "Roundtrip", F5).</summary>
		public async Task<Boolean> RoundTripAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this.IsBusy)
			{
				return false;
			}

			if (this._settings.Settings.Compiler.UseUserCommand)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Menu.Roundtrip"), Loc.Get("Str.Shell.Build.RoundtripUserCommand"));
				return false;
			}

			this.IsBusy = true;
			try
			{
				this._output.ClearErrors();
				BuildOutcome outcome = await this.RunAsync(document, new BuildMode(true, false, false, true));
				if (!outcome.Success || document.FilePath is null)
				{
					return false;
				}

				CompilerOptions options = this._settings.Settings.ToCompilerOptions(this._settings.Paths);
				String compiled = this._compiler.GetOutputPath(document.FilePath, options, true);
				await this._workspace.OpenAsync(compiled, new OpenDocumentOptions { AddToRecent = false, TemporaryDecompile = true, ClearBuildLog = false });
				return true;
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>Compiles all scripts of the project folder (or a folder chosen by the user) in a batch.</summary>
		public async Task MassCompileAsync()
		{
			if (this.IsBusy)
			{
				return;
			}

			PathSettings paths = this._settings.Settings.Paths;
			if (paths.OutputDirectory is null)
			{
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

			this.IsBusy = true;
			try
			{
				BatchCompileResult result = await this._toolWindows.RunBatchCompileAsync(scripts);
				this._output.ReportStatus(Loc.Format("Str.Shell.Build.MassCompileStatus", result.Compiled, result.Found, result.Failed));
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		private async Task<BuildOutcome> RunAsync(DocumentViewModel document, BuildMode mode)
		{
			if (document.Kind != DocumentKind.Script)
			{
				if (mode.ShowMessages)
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Build.CompileTitle"), Loc.Get("Str.Shell.Build.NotSsl"));
				}

				return new BuildOutcome(false, null);
			}

			CompilerSettings compiler = this._settings.Settings.Compiler;
			if (!mode.Preprocess && !compiler.CompileToSourceDirectory && this._settings.Settings.Paths.OutputDirectory is null)
			{
				if (mode.ShowMessages)
				{
					await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Build.CompileTitle"), Loc.Get("Str.Shell.Build.OutputFolderNotSet"));
				}

				return new BuildOutcome(false, null);
			}

			if (document.IsModified && !await this._workspace.SaveAsync(document))
			{
				return new BuildOutcome(false, null);
			}

			if (document.FilePath is null || document.IsModified)
			{
				return new BuildOutcome(false, null);
			}

			// A parse that was waiting for the typing pause must not compete with the compiler, but it must not be lost either:
			// it runs again when the compilation is over (otherwise the parser data stays old until the next keystroke).
			Boolean parseWasPending = document.NeedsParse || this._parser.IsPending(document);
			this._parser.Cancel(document);
			document.NeedsParse = false;

			CompilationRequest request = new CompilationRequest
			{
				ScriptPath = document.FilePath,
				Options = this._settings.Settings.ToCompilerOptions(this._settings.Paths),
				ShortCircuit = document.ProgramInfo?.ShortCircuitEvaluation ?? false,
				TemporaryOutput = mode.Temporary,
				PreprocessOnly = mode.Preprocess,
			};

			CompilationResult result;
			try
			{
				result = mode.Preprocess
					? await this._compiler.PreprocessAsync(request)
					: await this._compiler.CompileAsync(request);
			}
			finally
			{
				if (parseWasPending && document.IsParsable && !this._parser.IsPending(document) && this._workspace.Documents.Contains(document))
				{
					_ = this._parser.ParseAsync(document, true);
				}
			}

			document.BuildErrors = result.Errors;
			if (mode.Preprocess)
			{
				document.BuildLog = result.Output;
				this._output.ShowTab(OutputTab.Build);
				return new BuildOutcome(result.Success, result);
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
					this._output.ShowTab(compiler.UseUserCommand ? OutputTab.Build : OutputTab.Errors);
				}
			}

			return new BuildOutcome(result.Success, result);
		}

		private readonly record struct BuildMode(Boolean ShowMessages, Boolean ShowSuccess, Boolean Preprocess, Boolean Temporary);

		private readonly record struct BuildOutcome(Boolean Success, CompilationResult? Result);
	}
}
