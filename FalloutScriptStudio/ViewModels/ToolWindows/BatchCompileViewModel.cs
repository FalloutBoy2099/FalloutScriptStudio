namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using Core.Compilation;
	using Localization;

	/// <summary>
	/// View model of the batch compiler window: runs a mass compilation and reports its progress, the failed scripts and their output.
	/// </summary>
	public sealed partial class BatchCompileViewModel : ToolWindowViewModelBase
	{
		private readonly IBatchCompileService _batchCompileService;
		private readonly StringBuilder _log = new();
		private CancellationTokenSource? _cancellation;
		private String _statusKey = "Str.ToolWindows.Batch.Status.Ready";
		private Object?[] _statusArguments = Array.Empty<Object?>();

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ProgressText))]
		private Int32 _total;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Compiled))]
		[NotifyPropertyChangedFor(nameof(CompiledText))]
		[NotifyPropertyChangedFor(nameof(ProgressText))]
		private Int32 _completed;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Compiled))]
		[NotifyPropertyChangedFor(nameof(CompiledText))]
		[NotifyPropertyChangedFor(nameof(FailedText))]
		private Int32 _failed;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasUpToDate))]
		[NotifyPropertyChangedFor(nameof(UpToDateText))]
		private Int32 _upToDate;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Title))]
		private Int32 _threads = 1;

		[ObservableProperty]
		private String _currentFile = String.Empty;

		[ObservableProperty]
		private String _logText = String.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanClose))]
		[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
		private Boolean _isRunning;

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
		private Boolean _isCancelling;

		[ObservableProperty]
		private Boolean _isFinished;

		[ObservableProperty]
		private BatchCompileFailureItem? _selectedFailure;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchCompileViewModel"/> class.
		/// </summary>
		/// <param name="batchCompileService">The service that compiles the scripts.</param>
		public BatchCompileViewModel(IBatchCompileService batchCompileService)
		{
			ArgumentNullException.ThrowIfNull(batchCompileService);
			this._batchCompileService = batchCompileService;
		}

		/// <summary>Gets the scripts that failed to compile.</summary>
		public ObservableCollection<BatchCompileFailureItem> Failures { get; } = [];

		/// <summary>Gets the number of scripts that compiled successfully so far.</summary>
		public Int32 Compiled => this.Completed - this.Failed;

		/// <summary>Gets the status line, in the language in use.</summary>
		public String StatusText => Loc.Format(this._statusKey, this._statusArguments);

		/// <summary>Gets the "done n of m" caption.</summary>
		public String ProgressText => Loc.Format("Str.ToolWindows.Batch.Done", this.Completed, this.Total);

		/// <summary>Gets the caption of the number of scripts that compiled.</summary>
		public String CompiledText => Loc.Format("Str.ToolWindows.Batch.CompiledCount", this.Compiled);

		/// <summary>Gets the caption of the number of scripts that failed.</summary>
		public String FailedText => Loc.Format("Str.ToolWindows.Batch.FailedCount", this.Failed);

		/// <summary>Gets a value indicating whether scripts were skipped because they are up to date.</summary>
		public Boolean HasUpToDate => this.UpToDate > 0;

		/// <summary>Gets the caption of the number of scripts that were skipped because they are up to date.</summary>
		public String UpToDateText => Loc.Format("Str.ToolWindows.Batch.UpToDateCount", this.UpToDate);

		/// <summary>Gets the window title.</summary>
		public String Title => this.Threads > 1
			? Loc.Format("Str.ToolWindows.Batch.TitleThreads", this.Threads)
			: Loc.Get("Str.ToolWindows.Batch.Title");

		/// <summary>Gets a value indicating whether the window may be closed without cancelling anything.</summary>
		public Boolean CanClose => !this.IsRunning;

		/// <summary>
		/// Compiles the scripts and reports the progress. The returned task completes when the run is finished or cancelled.
		/// </summary>
		/// <param name="scriptPaths">The scripts to compile.</param>
		/// <param name="options">The compiler options.</param>
		/// <param name="batchOptions">Parallelism options; defaults are used when <see langword="null"/>.</param>
		/// <param name="observer">Also receives the notification of every finished script (the status bar of the main window), or <see langword="null"/>.</param>
		/// <returns>The summary of the run.</returns>
		public async Task<BatchCompileResult> RunAsync(IReadOnlyList<String> scriptPaths, CompilerOptions options, BatchCompileOptions? batchOptions = null, IProgress<BatchCompileProgress>? observer = null)
		{
			ArgumentNullException.ThrowIfNull(scriptPaths);
			ArgumentNullException.ThrowIfNull(options);

			this.Reset(scriptPaths.Count, GetThreadCount(scriptPaths.Count, batchOptions));
			this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.Found", scriptPaths.Count));
			this.SetStatus("Str.ToolWindows.Batch.Status.Compiling");
			this.IsRunning = true;

			using CancellationTokenSource cancellation = new();
			this._cancellation = cancellation;
			Progress<BatchCompileProgress> progress = new(item =>
			{
				this.ReportProgress(item);
				observer?.Report(item);
			});

			BatchCompileResult result;
			try
			{
				result = await this._batchCompileService.CompileAsync(scriptPaths, options, batchOptions, progress, cancellation.Token).ConfigureAwait(true);
			}
			catch
			{
				this.SetStatus("Str.ToolWindows.Batch.Status.Aborted");
				this.IsRunning = false;
				this.IsFinished = true;
				throw;
			}
			finally
			{
				this._cancellation = null;
			}

			this.Finish(result);
			return result;
		}

		/// <summary>
		/// Computes how many parallel compilations a run uses (the same rule as <see cref="BatchCompileService"/>).
		/// </summary>
		/// <param name="scriptCount">The number of scripts.</param>
		/// <param name="batchOptions">The parallelism options.</param>
		/// <returns>The number of threads.</returns>
		public static Int32 GetThreadCount(Int32 scriptCount, BatchCompileOptions? batchOptions)
		{
			BatchCompileOptions options = batchOptions ?? new BatchCompileOptions();
			if (!options.MultiThreaded)
			{
				return 1;
			}

			return Math.Max(1, Math.Min(options.MaxDegreeOfParallelism ?? Environment.ProcessorCount, scriptCount));
		}

		/// <summary>
		/// Requests the cancellation of a running compilation (also used when the window is closed during a run).
		/// </summary>
		public void RequestCancel()
		{
			if (!this.IsRunning || this.IsCancelling)
			{
				return;
			}

			this.IsCancelling = true;
			this.SetStatus("Str.ToolWindows.Batch.Status.Cancelling");
			this._cancellation?.Cancel();
		}

		/// <summary>
		/// Handles the notification of one finished script.
		/// </summary>
		/// <param name="progress">The notification.</param>
		public void ReportProgress(BatchCompileProgress progress)
		{
			ArgumentNullException.ThrowIfNull(progress);

			if (this.IsFinished)
			{
				return;
			}

			this.Total = progress.Total;
			this.Completed = progress.Completed;
			this.Failed = progress.Failed;
			this.UpToDate = progress.UpToDate;
			this.CurrentFile = Path.GetFileName(progress.ScriptPath);
			this.SetStatus("Str.ToolWindows.Batch.Status.Progress", progress.Completed, progress.Total);

			if (!progress.Succeeded && !this.Failures.Any(item => PathEquals(item.ScriptPath, progress.ScriptPath)))
			{
				BatchCompileFailureItem failure = new(progress.ScriptPath, String.Empty);
				this.Failures.Add(failure);
				this.SelectedFailure ??= failure;
				this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.Failed", Path.GetFileName(progress.ScriptPath)));
			}
		}

		private static Boolean PathEquals(String left, String right)
		{
			return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
		}

		private void Reset(Int32 total, Int32 threads)
		{
			this._log.Clear();
			this.LogText = String.Empty;
			this.Failures.Clear();
			this.SelectedFailure = null;
			this.Total = total;
			this.Completed = 0;
			this.Failed = 0;
			this.UpToDate = 0;
			this.CurrentFile = String.Empty;
			this.IsCancelling = false;
			this.IsFinished = false;
			this.Threads = threads;
		}

		private void Finish(BatchCompileResult result)
		{
			// The result is authoritative: it also carries the compiler output of every failure.
			foreach (BatchCompileFailure failure in result.Failures.OrderBy(item => item.ScriptPath, StringComparer.OrdinalIgnoreCase))
			{
				BatchCompileFailureItem? item = this.Failures.FirstOrDefault(entry => PathEquals(entry.ScriptPath, failure.ScriptPath));
				if (item is null)
				{
					this.Failures.Add(new BatchCompileFailureItem(failure.ScriptPath, failure.Output));
				}
				else
				{
					item.Output = failure.Output;
				}
			}

			this.UpToDate = result.UpToDate;
			this.Total = result.Found - result.UpToDate;
			this.Failed = result.Failed;
			this.Completed = result.Compiled + result.Failed;

			if (this.SelectedFailure is null && this.Failures.Count > 0)
			{
				this.SelectedFailure = this.Failures[0];
			}

			this.AppendLog("--------------------");
			this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.Compiled", result.Compiled));
			this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.FailedToCompile", result.Failed));
			if (result.UpToDate > 0)
			{
				this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.UpToDate", result.UpToDate));
			}

			this.AppendLog(Loc.Format("Str.ToolWindows.Batch.Log.Skipped", result.Cancelled ? result.Skipped : 0));

			this.SetStatus(result.Cancelled
				? "Str.ToolWindows.Batch.Status.Cancelled"
				: result.Failed == 0 ? "Str.ToolWindows.Batch.Status.Success" : "Str.ToolWindows.Batch.Status.Errors");
			this.IsRunning = false;
			this.IsCancelling = false;
			this.IsFinished = true;
		}

		/// <inheritdoc />
		protected override void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.StatusText));
			this.OnPropertyChanged(nameof(this.ProgressText));
			this.OnPropertyChanged(nameof(this.CompiledText));
			this.OnPropertyChanged(nameof(this.FailedText));
			this.OnPropertyChanged(nameof(this.UpToDateText));
		}

		private void SetStatus(String key, params Object?[] arguments)
		{
			this._statusKey = key;
			this._statusArguments = arguments;
			this.OnPropertyChanged(nameof(this.StatusText));
		}

		private void AppendLog(String line)
		{
			this._log.AppendLine(line);
			this.LogText = this._log.ToString();
		}

		[RelayCommand(CanExecute = nameof(CanCancel))]
		private void Cancel()
		{
			this.RequestCancel();
		}

		[RelayCommand]
		private void Close()
		{
			this.RequestClose(true);
		}

		private Boolean CanCancel()
		{
			return this.IsRunning && !this.IsCancelling;
		}
	}
}