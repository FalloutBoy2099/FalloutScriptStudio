namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>A tool window service with scripted answers.</summary>
	internal sealed class FakeToolWindowService : IToolWindowService
	{
		public Int32? GoToLineAnswer { get; set; }

		public String? HeaderAnswer { get; set; }

		public Boolean SettingsAnswer { get; set; } = true;

		public List<String> Calls { get; } = [];

		/// <summary>Gets or sets what the symbol list answers; it chooses nothing when it is <see langword="null"/>.</summary>
		public Func<SymbolPickerRequest, FalloutScriptStudio.Core.Indexing.IndexedSymbol?>? SymbolAnswer { get; set; }

		/// <summary>Gets the requests of the symbol list that were shown.</summary>
		public List<SymbolPickerRequest> SymbolRequests { get; } = [];

		public Task<FalloutScriptStudio.Core.Indexing.IndexedSymbol?> PickSymbolAsync(SymbolPickerRequest request)
		{
			this.SymbolRequests.Add(request);
			return Task.FromResult(this.SymbolAnswer?.Invoke(request));
		}

		/// <summary>Gets or sets what the command palette answers; it chooses nothing when it is <see langword="null"/>.</summary>
		public Func<CommandPaletteRequest, FalloutScriptStudio.Commands.CommandEntry?>? CommandAnswer { get; set; }

		/// <summary>Gets the requests of the command palette that were shown.</summary>
		public List<CommandPaletteRequest> CommandRequests { get; } = [];

		public Task<FalloutScriptStudio.Commands.CommandEntry?> PickCommandAsync(CommandPaletteRequest request)
		{
			this.CommandRequests.Add(request);
			return Task.FromResult(this.CommandAnswer?.Invoke(request));
		}

		/// <summary>Gets or sets a value indicating whether a mass compilation waits until its token is cancelled (like a batch window of hanging tools).</summary>
		public Boolean BatchHangs { get; set; }

		/// <summary>Gets the scripts the fake batch run reports as failed.</summary>
		public List<BatchCompileFailure> BatchFailures { get; } = [];

		/// <summary>Gets the progress notifications the fake batch run reports before it finishes or hangs.</summary>
		public List<BatchCompileProgress> BatchProgress { get; } = [];

		/// <summary>Gets the token of the last mass compilation.</summary>
		public System.Threading.CancellationToken BatchToken { get; private set; }

		/// <summary>Gets the settings pages that were opened.</summary>
		public List<FalloutScriptStudio.ViewModels.ToolWindows.SettingsPage> SettingsPages { get; } = [];

		public List<IReadOnlyList<String>> BatchRuns { get; } = [];

		public Task<Boolean> ShowSettingsAsync()
		{
			this.Calls.Add("settings");
			return Task.FromResult(this.SettingsAnswer);
		}

		public Task<Boolean> ShowProjectSettingsAsync()
		{
			this.Calls.Add("project-settings");
			return Task.FromResult(true);
		}

		public Task<Boolean> ShowSettingsAsync(FalloutScriptStudio.ViewModels.ToolWindows.SettingsPage page)
		{
			this.SettingsPages.Add(page);
			return this.ShowSettingsAsync();
		}

		/// <summary>Gets, for every mass compilation, whether it was asked to compile everything (Rebuild All).</summary>
		public List<Boolean> RebuildAllRequests { get; } = [];

		/// <summary>Gets the scripts the fake batch run reports as skipped because they are up to date.</summary>
		public List<String> BatchUpToDate { get; } = [];

		public Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths, IProgress<BatchCompileProgress>? progress, Boolean rebuildAll, System.Threading.CancellationToken cancellationToken)
		{
			this.RebuildAllRequests.Add(rebuildAll);
			return this.RunBatchCompileAsync(scriptPaths, progress, cancellationToken);
		}

		public async Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths, IProgress<BatchCompileProgress>? progress, System.Threading.CancellationToken cancellationToken)
		{
			this.BatchRuns.Add(scriptPaths);
			this.BatchToken = cancellationToken;
			Int32 done = 0;
			foreach (BatchCompileProgress item in this.BatchProgress)
			{
				progress?.Report(item);
				done++;
			}

			if (this.BatchHangs)
			{
				try
				{
					await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return new BatchCompileResult(scriptPaths.Count, done, Array.Empty<BatchCompileFailure>(), true, 1);
				}
			}

			return new BatchCompileResult(scriptPaths.Count, scriptPaths.Count - this.BatchFailures.Count - this.BatchUpToDate.Count, this.BatchFailures.ToArray(), false, 1) { UpToDateScripts = this.BatchUpToDate.ToArray() };
		}

		public Task ShowFormatProjectAsync()
		{
			this.Calls.Add("FormatProject");
			return Task.CompletedTask;
		}

		public Task ShowSnippetManagerAsync()
		{
			this.Calls.Add("SnippetManager");
			return Task.CompletedTask;
		}

		public void ShowAbout()
		{
			this.Calls.Add("about");
		}

		public Task<Int32?> PromptGoToLineAsync(Int32 currentLine, Int32 lineCount)
		{
			this.Calls.Add("goto:" + currentLine + "/" + lineCount);
			return Task.FromResult(this.GoToLineAnswer);
		}

		public Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths)
		{
			this.BatchRuns.Add(scriptPaths);
			return Task.FromResult(new BatchCompileResult(scriptPaths.Count, scriptPaths.Count, Array.Empty<BatchCompileFailure>(), false, 1));
		}

		/// <summary>Gets the headers the fake "chooses with Ctrl" (handed to the keep-open callback) before it answers.</summary>
		public List<String> KeepOpenHeaders { get; } = [];

		public async Task<String?> PickHeaderAsync(Func<String, Task>? chooseAndKeepOpen = null)
		{
			this.Calls.Add("pick-header");
			if (chooseAndKeepOpen is not null)
			{
				foreach (String header in this.KeepOpenHeaders)
				{
					await chooseAndKeepOpen(header);
				}
			}

			return this.HeaderAnswer;
		}
	}
}