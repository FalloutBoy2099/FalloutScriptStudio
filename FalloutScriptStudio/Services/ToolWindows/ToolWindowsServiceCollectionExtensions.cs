namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Core.Messages;
	using ViewModels.ToolWindows;
	using Views.ToolWindows;

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

			services.TryAddSingleton<IFileAssociationService, FileAssociationService>();
			services.TryAddSingleton<IEditorFontCatalog, EditorFontCatalog>();
			services.TryAddSingleton<IToolWindowService, ToolWindowService>();
			services.TryAddSingleton<IMessageComparisonService, MessageComparisonService>();

			services.AddTransient<SettingsViewModel>();
			services.AddTransient<ProjectSettingsViewModel>();
			services.AddTransient<AboutViewModel>();
			services.AddTransient<GoToLineViewModel>();
			services.AddTransient<BatchCompileViewModel>();
			services.AddTransient<HeadersViewModel>();
			services.AddTransient<SymbolPickerViewModel>();
			services.AddTransient<CommandPaletteViewModel>();
			services.AddTransient<FormatProjectViewModel>();
			services.AddTransient<SnippetPickerViewModel>();
			services.AddTransient<SnippetManagerViewModel>();
			services.AddTransient<CompareMessagesViewModel>();

			services.AddTransient<SettingsWindow>();
			services.AddTransient<ProjectSettingsWindow>();
			services.AddTransient<AboutWindow>();
			services.AddTransient<GoToLineWindow>();
			services.AddTransient<BatchCompileWindow>();
			services.AddTransient<HeadersWindow>();
			services.AddTransient<SymbolPickerWindow>();
			services.AddTransient<CommandPaletteWindow>();
			services.AddTransient<FormatProjectWindow>();
			services.AddTransient<SnippetPickerWindow>();
			services.AddTransient<SnippetManagerWindow>();
			services.AddTransient<CompareMessagesWindow>();

			return services;
		}
	}
}