namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Dependency injection registration of the functions tree services.
	/// </summary>
	public static class FunctionsServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IUserKeywordStore"/> and <see cref="IFunctionCatalogService"/> as singletons.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="pathsFactory">Supplies the paths of the two ini files.</param>
		/// <param name="keywordFileFactory">Supplies the path of the user keyword JSON file.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddFunctionCatalog(
			this IServiceCollection services,
			Func<IServiceProvider, FunctionCatalogPaths> pathsFactory,
			Func<IServiceProvider, String> keywordFileFactory)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(pathsFactory);
			ArgumentNullException.ThrowIfNull(keywordFileFactory);

			services.AddSingleton<IUserKeywordStore>(provider => new UserKeywordStore(keywordFileFactory(provider)));
			services.AddSingleton<IFunctionCatalogService>(provider => new FunctionCatalogService(
				pathsFactory(provider),
				provider.GetRequiredService<IUserKeywordStore>()));
			return services;
		}
	}
}