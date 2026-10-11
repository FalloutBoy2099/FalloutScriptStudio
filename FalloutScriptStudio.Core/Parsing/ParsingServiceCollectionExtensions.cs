namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Dependency injection registration of the parsing services.
	/// </summary>
	public static class ParsingServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the parsing services as singletons: <see cref="IScriptParser"/>, <see cref="INativeParser"/>
		/// (<see cref="OutOfProcessNativeParser"/>: <c>parser.dll</c> never loads into the application process),
		/// <see cref="MacroRegistry"/>, <see cref="InternalParser"/>, <see cref="ProcedureLocator"/>,
		/// <see cref="FoldingGenerator"/>, <see cref="ProcedureBeginFixer"/> and an in-memory
		/// <see cref="ICollapsedProcedureStore"/> (unless the application registered its own).
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="optionsFactory">
		/// Builds the current <see cref="ParserOptions"/> from the application settings. It is called on every parse, so
		/// changes of the settings take effect without restarting.
		/// </param>
		/// <param name="hostOptions">
		/// Where the parser host executable is; the application folder (<see cref="AppContext.BaseDirectory"/>) when omitted.
		/// </param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddScriptParsing(this IServiceCollection services, Func<IServiceProvider, ParserOptions> optionsFactory, NativeParserHostOptions? hostOptions = null)
		{
			ArgumentNullException.ThrowIfNull(optionsFactory);

			services.AddSingleton<MacroRegistry>();
			services.AddSingleton(hostOptions ?? NativeParserHostOptions.ForDirectory(AppContext.BaseDirectory));
			services.TryAddSingleton<IParserHostLauncher, ProcessParserHostLauncher>();
			services.AddSingleton<OutOfProcessNativeParser>();
			services.AddSingleton<INativeParser>(provider => provider.GetRequiredService<OutOfProcessNativeParser>());
			services.AddSingleton<InternalParser>();
			services.AddSingleton<ProcedureLocator>();
			services.AddSingleton<FoldingGenerator>();
			services.AddSingleton<ProcedureBeginFixer>();
			services.TryAddSingleton<ICollapsedProcedureStore, InMemoryCollapsedProcedureStore>();
			services.AddSingleton<IScriptParser>(provider => new ScriptParser(
				provider.GetRequiredService<INativeParser>(),
				() => optionsFactory(provider),
				provider.GetRequiredService<MacroRegistry>(),
				provider.GetService<ILogger<ScriptParser>>()));

			return services;
		}
	}
}
