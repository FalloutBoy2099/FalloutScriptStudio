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

		public List<IReadOnlyList<String>> BatchRuns { get; } = new List<IReadOnlyList<String>>();

		public Task<Boolean> ShowSettingsAsync()
		{
			this.Calls.Add("settings");
			return Task.FromResult(this.SettingsAnswer);
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
