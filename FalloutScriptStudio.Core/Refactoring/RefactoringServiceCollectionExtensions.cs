namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Dependency injection registration of the refactoring services.
	/// </summary>
	public static class RefactoringServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IRefactoringService"/> as a singleton.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddRefactoring(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddSingleton<IRefactoringService, RefactoringService>();
			return services;
		}
	}
}
