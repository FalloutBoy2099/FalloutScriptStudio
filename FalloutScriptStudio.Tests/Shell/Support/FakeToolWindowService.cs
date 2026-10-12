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

		public List<String> Calls { get; } = new List<String>();

		/// <summary>Gets or sets a value indicating whether a mass compilation waits until its token is cancelled (like a batch window of hanging tools).</summary>
		public Boolean BatchHangs { get; set; }

		/// <summary>Gets the scripts the fake batch run reports as failed.</summary>
		public List<BatchCompileFailure> BatchFailures { get; } = new List<BatchCompileFailure>();

		/// <summary>Gets the progress notifications the fake batch run reports before it finishes or hangs.</summary>
		public List<BatchCompileProgress> BatchProgress { get; } = new List<BatchCompileProgress>();

		/// <summary>Gets the token of the last mass compilation.</summary>
		public System.Threading.CancellationToken BatchToken { get; private set; }

		/// <summary>Gets the settings pages that were opened.</summary>
		public List<FalloutScriptStudio.ViewModels.ToolWindows.SettingsPage> SettingsPages { get; } = new List<FalloutScriptStudio.ViewModels.ToolWindows.SettingsPage>();

		public List<IReadOnlyList<String>> BatchRuns { get; } = new List<IReadOnlyList<String>>();

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

			return new BatchCompileResult(scriptPaths.Count, scriptPaths.Count - this.BatchFailures.Count, this.BatchFailures.ToArray(), false, 1);
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
		public List<String> KeepOpenHeaders { get; } = new List<String>();

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
