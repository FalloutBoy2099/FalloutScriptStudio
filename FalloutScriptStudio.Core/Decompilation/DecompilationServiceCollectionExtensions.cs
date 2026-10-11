namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>
	/// Dependency injection registration of the decompilers.
	/// </summary>
	public static class DecompilationServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="BuiltInScriptDecompiler"/>, <see cref="ExternalInt2SslDecompiler"/> and, as <see cref="IScriptDecompiler"/>,
		/// the <see cref="ScriptDecompilerSelector"/> that picks one of them from the compiler options of each request.
		/// <see cref="ExternalInt2SslDecompiler"/> needs an <c>IProcessRunner</c> to be registered. Services already registered are kept.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddDecompilation(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.TryAddSingleton<BuiltInScriptDecompiler>();
			services.TryAddSingleton<ExternalInt2SslDecompiler>();
			services.TryAddSingleton<IScriptDecompiler>(provider => new ScriptDecompilerSelector(
				provider.GetRequiredService<BuiltInScriptDecompiler>(),
				provider.GetRequiredService<ExternalInt2SslDecompiler>()));
			return services;
		}
	}
}
