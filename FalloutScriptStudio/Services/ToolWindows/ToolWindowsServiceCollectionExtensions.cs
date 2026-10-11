namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.ToolWindows;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>
	/// Registration of the tool windows in the service collection.
	/// </summary>
	public static class ToolWindowsServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IToolWindowService"/>, the tool windows (transient) and their view models (transient).
		/// The services <see cref="IDialogService"/>, <see cref="IThemeService"/> and the Core services
		/// (<c>ISettingsService</c>, <c>IBatchCompileService</c>) must be registered by the caller.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The same service collection.</returns>
		public static IServiceCollection AddToolWindows(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.TryAddSingleton<FileAssociationService>();
			services.TryAddSingleton<IEditorFontCatalog, EditorFontCatalog>();
			services.TryAddSingleton<IToolWindowService, ToolWindowService>();

			services.AddTransient<SettingsViewModel>();
			services.AddTransient<AboutViewModel>();
			services.AddTransient<GoToLineViewModel>();
			services.AddTransient<BatchCompileViewModel>();
			services.AddTransient<HeadersViewModel>();

			services.AddTransient<SettingsWindow>();
			services.AddTransient<AboutWindow>();
			services.AddTransient<GoToLineWindow>();
			services.AddTransient<BatchCompileWindow>();
			services.AddTransient<HeadersWindow>();

			return services;
		}
	}
}
