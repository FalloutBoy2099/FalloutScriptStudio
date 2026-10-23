namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading.Tasks;
	using Microsoft.Extensions.DependencyInjection;
	using Core.Configuration;
	using Core.Dialogue;
	using Core.Functions;
	using Services;
	using UiTesting;
	using Workspace;

	/// <summary>The start-up services taken from the container of the application; disposing it disposes the container and with it the singletons.</summary>
	public sealed class ContainerStartupServices : IStartupServices
	{
		private readonly ServiceProvider _provider;
		private readonly UiTestMode _uiTest;

		/// <summary>Initializes a new instance of the <see cref="ContainerStartupServices"/> class.</summary>
		/// <param name="provider">The container, built from <see cref="AppServices.ConfigureServices"/>.</param>
		/// <param name="uiTest">The UI test mode (first run skipped, language and color mode fixed); <see langword="null"/> for a normal run.</param>
		public ContainerStartupServices(ServiceProvider provider, UiTestMode? uiTest = null)
		{
			ArgumentNullException.ThrowIfNull(provider);
			this._provider = provider;
			this._uiTest = uiTest ?? UiTestMode.Off;
		}

		/// <inheritdoc />
		public IServiceProvider Provider => this._provider;

		/// <inheritdoc />
		public void LoadSettings()
		{
			ISettingsService settings = this._provider.GetRequiredService<ISettingsService>();
			settings.Load();
			if (!this._uiTest.OpensFirstRunSettings && !settings.Settings.IsFirstRunCompleted)
			{
				// Under the UI tests the first-run Settings window never opens (it would block the start of the main window).
				settings.Update(current => current.IsFirstRunCompleted = true);
			}
		}

		/// <inheritdoc />
		public void SaveSettings()
		{
			this._provider.GetService<ISettingsService>()?.Save();
		}

		/// <inheritdoc />
		public void ActivateProject()
		{
			_ = this._provider.GetRequiredService<IProjectService>();
		}

		/// <inheritdoc />
		public async Task LoadStoresAsync()
		{
			await this._provider.GetRequiredService<IUserKeywordStore>().LoadAsync();
			await this._provider.GetRequiredService<IFunctionCatalogService>().LoadAsync();
			await this._provider.GetRequiredService<IDialogueRulesService>().LoadAsync();
			await this._provider.GetRequiredService<INodeCodeSnippetStore>().LoadAsync();
		}

		/// <inheritdoc />
		public void ApplyStartupTheme()
		{
			ISettingsService settings = this._provider.GetRequiredService<ISettingsService>();
			ShellLayoutStore layoutStore = this._provider.GetRequiredService<ShellLayoutStore>();
			Boolean exists = layoutStore.Exists;
			AppTheme theme = this._uiTest.OverridesLayout
				? this._uiTest.Theme
				: StartupTheme.Resolve(exists, exists ? layoutStore.Load().Theme : AppTheme.System, settings.ImportedLegacyData, settings.Settings.Editor.Theme);
			this._provider.GetRequiredService<IThemeService>().Apply(theme);
		}

		/// <inheritdoc />
		public String? GetStoredLanguage()
		{
			return this._uiTest.OverridesLayout ? this._uiTest.Language : this._provider.GetRequiredService<ShellLayoutStore>().Load().Language;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this._provider.Dispose();
		}
	}
}