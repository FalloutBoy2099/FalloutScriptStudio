namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Parsing;

	/// <summary>
	/// Dependency injection registration of the text tools.
	/// </summary>
	public static class TextToolsServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="ICodeFormatter"/>, <see cref="ITextSearcher"/>, <see cref="IFileSearcher"/>, <see cref="IProcedureTextService"/>,
		/// <see cref="KeywordExpander"/>, <see cref="AutoPairHandler"/> and <see cref="IncludeInserter"/> as singletons. The procedure parser and locator
		/// registered by <c>AddScriptParsing</c> are reused when present.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddTextTools(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddSingleton<ICodeFormatter, CodeFormatter>();
			services.AddSingleton<ITextSearcher, TextSearcher>();
			services.AddSingleton<IFileSearcher, FileSearcher>();
			services.AddSingleton<AutoPairHandler>();
			services.AddSingleton<KeywordExpander>();
			services.AddSingleton<IProcedureTextService>(provider => new ProcedureTextService(
				provider.GetService<InternalParser>() ?? new InternalParser(),
				provider.GetService<ProcedureLocator>() ?? new ProcedureLocator()));
			services.AddSingleton(provider => new IncludeInserter(
				provider.GetService<InternalParser>() ?? new InternalParser(),
				provider.GetService<ProcedureLocator>() ?? new ProcedureLocator()));
			return services;
		}
	}
}