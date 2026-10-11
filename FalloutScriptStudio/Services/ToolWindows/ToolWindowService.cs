namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.ToolWindows;
	using Microsoft.Extensions.DependencyInjection;

	/// <inheritdoc />
	public sealed class ToolWindowService : IToolWindowService
	{
		private readonly IServiceProvider _services;
		private readonly ISettingsService _settingsService;
		private readonly IDialogService _dialogService;

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolWindowService"/> class.
		/// </summary>
		/// <param name="services">Creates the windows and view models.</param>
		/// <param name="settingsService">Provides the settings and paths.</param>
		/// <param name="dialogService">Shows the messages.</param>
		public ToolWindowService(IServiceProvider services, ISettingsService settingsService, IDialogService dialogService)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(settingsService);
			ArgumentNullException.ThrowIfNull(dialogService);
			this._services = services;
			this._settingsService = settingsService;
			this._dialogService = dialogService;
		}

		/// <inheritdoc />
		public Task<Boolean> ShowSettingsAsync()
		{
			SettingsViewModel viewModel = this._services.GetRequiredService<SettingsViewModel>();
			SettingsWindow window = ActivatorUtilities.CreateInstance<SettingsWindow>(this._services, viewModel);
			ShowModal(window);
			return Task.FromResult(viewModel.Applied);
		}

		/// <inheritdoc />
		public void ShowAbout()
		{
			AboutViewModel viewModel = this._services.GetRequiredService<AboutViewModel>();
			AboutWindow window = ActivatorUtilities.CreateInstance<AboutWindow>(this._services, viewModel);
			ShowModal(window);
		}

		/// <inheritdoc />
		public Task<Int32?> PromptGoToLineAsync(Int32 currentLine, Int32 lineCount)
		{
			GoToLineViewModel viewModel = this._services.GetRequiredService<GoToLineViewModel>();
			viewModel.Initialize(currentLine, lineCount);
			GoToLineWindow window = ActivatorUtilities.CreateInstance<GoToLineWindow>(this._services, viewModel);
			ShowModal(window);
			return Task.FromResult(viewModel.SelectedLine);
		}

		/// <inheritdoc />
		public async Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths)
		{
			ArgumentNullException.ThrowIfNull(scriptPaths);

			BatchCompileResult nothingDone = new BatchCompileResult(0, 0, Array.Empty<BatchCompileFailure>(), false, 0);
			if (scriptPaths.Count == 0)
			{
				await this._dialogService.ShowInfoAsync(Loc.Get("Str.ToolWindows.Batch.Title"), Loc.Get("Str.ToolWindows.Batch.NothingToCompile")).ConfigureAwait(true);
				return nothingDone;
			}

			AppSettings settings = this._settingsService.Settings;
			if (String.IsNullOrWhiteSpace(settings.Paths.OutputDirectory))
			{
				await this._dialogService.ShowErrorAsync(
					Loc.Get("Str.ToolWindows.Batch.Title"),
					Loc.Get("Str.ToolWindows.Batch.OutputFolderNotSet") + "\n" + Loc.Get("Str.ToolWindows.Batch.OutputFolderHint")).ConfigureAwait(true);
				return nothingDone;
			}

			CompilerOptions options = settings.ToCompilerOptions(this._settingsService.Paths) with { CompileToSourceDirectory = false };
			BatchCompileOptions batchOptions = settings.ToBatchCompileOptions();

			BatchCompileViewModel viewModel = this._services.GetRequiredService<BatchCompileViewModel>();
			BatchCompileWindow window = ActivatorUtilities.CreateInstance<BatchCompileWindow>(this._services, viewModel);
			Window? owner = FindOwner();
			if (owner != null)
			{
				window.Owner = owner;
			}

			window.Show();
			return await viewModel.RunAsync(scriptPaths, options, batchOptions).ConfigureAwait(true);
		}

		/// <inheritdoc />
		public async Task<String?> PickHeaderAsync(Func<String, Task>? chooseAndKeepOpen = null)
		{
			String? directory = this._settingsService.Settings.Paths.HeadersDirectory;
			if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			{
				await this._dialogService.ShowErrorAsync(
					Loc.Get("Str.ToolWindows.Headers.Title"),
					Loc.Get("Str.ToolWindows.Headers.FolderMissing") + "\n" + Loc.Get("Str.ToolWindows.Headers.FolderHint")).ConfigureAwait(true);
				return null;
			}

			HeadersViewModel viewModel = this._services.GetRequiredService<HeadersViewModel>();
			viewModel.KeepOpenHandler = chooseAndKeepOpen;
			await viewModel.LoadAsync(directory).ConfigureAwait(true);
			HeadersWindow window = ActivatorUtilities.CreateInstance<HeadersWindow>(this._services, viewModel);
			ShowModal(window);
			return viewModel.SelectedPath;
		}

		private static Window? FindOwner()
		{
			Application? application = Application.Current;
			if (application is null)
			{
				return null;
			}

			Window? active = application.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive && window.IsVisible);
			if (active != null)
			{
				return active;
			}

			return application.MainWindow is { IsVisible: true } main ? main : null;
		}

		private static void ShowModal(Window window)
		{
			Window? owner = FindOwner();
			if (owner != null && !ReferenceEquals(owner, window))
			{
				window.Owner = owner;
			}

			window.ShowDialog();
		}
	}
}
