namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Configuration;
	using Core.Globals;
	using Core.Indexing;
	using Problems;

	/// <summary>
	/// Follows the global variables of the project index: it tells the panel (after a short pause, so that an index run that updates
	/// hundreds of files is one change) that the variables changed, runs the analysis with the ignore list of the settings, and lists its
	/// findings in the problems, on demand (Code &gt; Check Global Variables) or by itself after each update when the setting
	/// <see cref="EditorSettings.GlobalVariableFindings"/> is on.
	/// </summary>
	public sealed class GlobalVariablesService : IDisposable
	{
		/// <summary>The default pause between the end of an index update and the notification.</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(400);

		private readonly IProjectSymbolIndex _index;
		private readonly ISettingsService _settings;
		private readonly IProblemService _problems;
		private readonly TimeSpan _debounce;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private readonly Object _gate = new();
		private Timer? _timer;
		private Int32 _generation;
		private Boolean _disposed;

		/// <summary>Initializes the service.</summary>
		/// <param name="index">The project index.</param>
		/// <param name="settings">The settings (the findings option and the ignore list).</param>
		/// <param name="problems">The list the findings go to.</param>
		/// <param name="debounce">The pause before a change is announced; <see cref="DefaultDebounce"/> when <see langword="null"/>.</param>
		public GlobalVariablesService(IProjectSymbolIndex index, ISettingsService settings, IProblemService problems, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(index);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(problems);
			this._index = index;
			this._settings = settings;
			this._problems = problems;
			this._debounce = debounce ?? DefaultDebounce;
			this._index.Updated += this.OnIndexUpdated;
		}

		/// <summary>Raised on the thread the service was created on, a short while after the variables of the index changed.</summary>
		public event EventHandler? Changed;

		/// <summary>Gets the variables of the last complete index snapshot.</summary>
		public GlobalVariableIndex Current => this._index.GetGlobalVariables();

		/// <summary>Gets a value indicating whether some files were left out of the variables because the project holds more uses than the index keeps.</summary>
		public Boolean IsTruncated => this._index.AreGlobalsTruncated;

		/// <summary>Builds the options of the analysis from the settings.</summary>
		/// <returns>The options.</returns>
		public GlobalAnalysisOptions CreateOptions()
		{
			return new GlobalAnalysisOptions { Ignored = this._settings.Settings.Editor.GlobalVariableIgnore.ToArray() };
		}

		/// <summary>Analyzes the variables of the index (off the calling thread).</summary>
		/// <param name="cancellationToken">Cancels the analysis.</param>
		/// <returns>The findings.</returns>
		public Task<IReadOnlyList<GlobalFinding>> AnalyzeAsync(CancellationToken cancellationToken = default)
		{
			GlobalVariableIndex variables = this.Current;
			GlobalAnalysisOptions options = this.CreateOptions();
			return Task.Run(() => GlobalVariableAnalyzer.Analyze(variables, options), cancellationToken);
		}

		/// <summary>Analyzes the variables and lists the findings in the problems, replacing the ones of the previous check.</summary>
		/// <param name="cancellationToken">Cancels the check.</param>
		/// <returns>The findings that were listed.</returns>
		public async Task<IReadOnlyList<GlobalFinding>> CheckAsync(CancellationToken cancellationToken = default)
		{
			IReadOnlyList<GlobalFinding> findings = await this.AnalyzeAsync(cancellationToken).ConfigureAwait(true);
			this.Publish(findings);
			return findings;
		}

		/// <summary>Replaces the problems of the global variables by the problems of some findings.</summary>
		/// <param name="findings">The findings.</param>
		public void Publish(IReadOnlyList<GlobalFinding> findings)
		{
			ArgumentNullException.ThrowIfNull(findings);
			this._problems.ClearOrigin(ProblemOrigins.GlobalVariables);
			if (findings.Count > 0)
			{
				this._problems.ReportRange(findings.Select(ProblemFactory.GlobalVariable));
			}
		}

		/// <summary>Adds a variable to the ignore list of the settings.</summary>
		/// <param name="entry">The name or the number.</param>
		/// <returns><see langword="true"/> when the entry was added, <see langword="false"/> when the list had it already.</returns>
		public Boolean Ignore(String entry)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(entry);
			String trimmed = entry.Trim();
			if (this._settings.Settings.Editor.GlobalVariableIgnore.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
			{
				return false;
			}

			this._settings.Update(settings => settings.Editor.GlobalVariableIgnore.Add(trimmed));
			return true;
		}

		/// <summary>Stops following the index.</summary>
		public void Dispose()
		{
			lock (this._gate)
			{
				this._disposed = true;
				this._timer?.Dispose();
				this._timer = null;
			}

			this._index.Updated -= this.OnIndexUpdated;
		}

		private void OnIndexUpdated(Object? sender, EventArgs e)
		{
			lock (this._gate)
			{
				if (this._disposed)
				{
					return;
				}

				Int32 generation = ++this._generation;
				this._timer?.Dispose();
				this._timer = new Timer(_ => this.Post(generation), null, this._debounce, Timeout.InfiniteTimeSpan);
			}
		}

		private void Post(Int32 generation)
		{
			if (this._context is not null)
			{
				this._context.Post(_ => this.Announce(generation), null);
			}
			else
			{
				this.Announce(generation);
			}
		}

		private void Announce(Int32 generation)
		{
			lock (this._gate)
			{
				if (this._disposed || generation != this._generation)
				{
					return;
				}
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			if (this._settings.Settings.Editor.GlobalVariableFindings)
			{
				_ = this.CheckQuietlyAsync();
			}
		}

		private async Task CheckQuietlyAsync()
		{
			try
			{
				await this.CheckAsync().ConfigureAwait(true);
			}
			catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
			{
				// The analysis is a convenience: a failure leaves the previous findings in place.
			}
		}
	}
}