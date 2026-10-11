namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>
	/// Dependency injection registration of the archive services.
	/// </summary>
	public static class ArchivesServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IDatArchiveReaderFactory"/> and <see cref="IDatArchiveExtractor"/> as singletons.
		/// Services that are already registered are kept.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddArchives(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.TryAddSingleton<IDatArchiveReaderFactory, DatArchiveReaderFactory>();
			services.TryAddSingleton<IDatArchiveExtractor, DatArchiveExtractor>();
			return services;
		}
	}
}
