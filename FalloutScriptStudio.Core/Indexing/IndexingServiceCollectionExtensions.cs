namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;

	/// <summary>Dependency injection registration of the project index.</summary>
	public static class IndexingServiceCollectionExtensions
	{
		/// <summary>Registers <see cref="IProjectSymbolIndex"/> as a singleton.</summary>
		/// <param name="services">The service collection.</param>
		/// <param name="optionsFactory">
		/// Builds the options of a run from the application settings (the code page of the files). It is called at the start of every run,
		/// so a change of the settings takes effect without restarting.
		/// </param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddProjectIndex(this IServiceCollection services, Func<IServiceProvider, ProjectIndexOptions> optionsFactory)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(optionsFactory);

			services.AddSingleton<IProjectSymbolIndex>(provider => new ProjectSymbolIndex(
				() => optionsFactory(provider),
				provider.GetService<ILogger<ProjectSymbolIndex>>()));
			return services;
		}
	}
}