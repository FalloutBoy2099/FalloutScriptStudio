namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>The start-up services taken from the container of the application; disposing it disposes the container and with it the singletons.</summary>
	public sealed class ContainerStartupServices : IStartupServices
	{
		private readonly ServiceProvider _provider;

		/// <summary>Initializes a new instance of the <see cref="ContainerStartupServices"/> class.</summary>
		/// <param name="provider">The container, built from <see cref="AppServices.ConfigureServices"/>.</param>
		public ContainerStartupServices(ServiceProvider provider)
		{
			ArgumentNullException.ThrowIfNull(provider);
			this._provider = provider;
		}

		/// <inheritdoc />
		public IServiceProvider Provider => this._provider;

		/// <inheritdoc />
		public void LoadSettings()
		{
			this._provider.GetRequiredService<ISettingsService>().Load();
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
			AppTheme theme = StartupTheme.Resolve(exists, exists ? layoutStore.Load().Theme : AppTheme.System, settings.ImportedLegacyData, settings.Settings.Editor.Theme);
			this._provider.GetRequiredService<IThemeService>().Apply(theme);
		}

		/// <inheritdoc />
		public String? GetStoredLanguage()
		{
			return this._provider.GetRequiredService<ShellLayoutStore>().Load().Language;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this._provider.Dispose();
		}
	}
}
