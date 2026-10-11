namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using FalloutScriptStudio.Views.ScriptTools;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Registers the script tool windows.
	/// </summary>
	public static class ScriptToolsServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IScriptToolService"/>, its stateless helpers, and the transient windows and view models of the
		/// procedure name dialog, the script registration window and the advanced search window.
		/// The windows need <c>IWorkspace</c>, <c>ISettingsService</c> and <c>IDialogService</c> from the composition root;
		/// <c>ArchiveScriptExtractor</c> is optional.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddScriptTools(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddSingleton<IScriptToolService, ScriptToolService>();
			services.AddSingleton<ScriptRegistryStore>();
			services.AddSingleton<ScriptSearcher>();

			services.AddTransient<ProcedureNameViewModel>();
			services.AddTransient<ProcedureNameWindow>();
			services.AddTransient<RegisterScriptViewModel>();
			services.AddTransient<RegisterScriptWindow>();
			services.AddTransient<AdvancedSearchViewModel>();
			services.AddTransient<AdvancedSearchWindow>();
			return services;
		}
	}
}
