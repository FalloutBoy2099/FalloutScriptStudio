namespace FalloutScriptStudio.Editor
{
	using FalloutScriptStudio.Views.Editor;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>Dependency injection registration of the script editor.</summary>
	public static class EditorServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="ScriptEditorServices"/> and <see cref="ScriptEditorViewFactory"/> as singletons.
		/// The application must also register <c>ISettingsService</c>, <c>IThemeService</c>, <c>IWorkspace</c> and
		/// <c>ICollapsedProcedureStore</c> (the latter is registered by <c>AddScriptParsing</c>).
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddScriptEditor(this IServiceCollection services)
		{
			services.TryAddSingleton<ScriptEditorServices>();
			services.TryAddSingleton<ScriptEditorViewFactory>();
			return services;
		}
	}
}
