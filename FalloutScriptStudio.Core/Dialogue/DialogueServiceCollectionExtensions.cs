namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Dependency injection registration of the dialogue tools.
	/// </summary>
	public static class DialogueServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the dialogue services: the rules service, the node parser, the flowchart builder and document store,
		/// the node factory and editor (singletons) and <see cref="DialoguePreview"/> (transient, it holds the state of one window).
		/// <c>AddTextTools</c> must be called as well: the services need <c>IProcedureTextService</c>.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="rulesFileFactory">Supplies the path of the dialogue rules file (<c>settings\DialogFunc.cfg</c>).</param>
		/// <param name="snippetFileFactory">Supplies the path of the node code snippet file (<c>settings\CustomCode.ini</c>); the snippet store is not registered when <c>null</c>.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddDialogueTools(this IServiceCollection services, Func<IServiceProvider, String> rulesFileFactory, Func<IServiceProvider, String>? snippetFileFactory = null)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(rulesFileFactory);

			services.AddSingleton<IDialogueRulesService>(provider => new DialogueRulesService(rulesFileFactory(provider)));
			services.AddSingleton<IDialogueRulesSource>(provider => provider.GetRequiredService<IDialogueRulesService>());
			services.AddSingleton<IDialogueNodeParser, DialogueNodeParser>();
			services.AddSingleton<IFlowchartBuilder, FlowchartBuilder>();
			services.AddSingleton<IFlowchartDocumentStore, FlowchartDocumentStore>();
			services.AddSingleton<DialogueNodeFactory>();
			services.AddSingleton<DialogueNodeEditor>();
			services.AddTransient<DialoguePreview>();
			if (snippetFileFactory is not null)
			{
				services.AddSingleton<INodeCodeSnippetStore>(provider => new NodeCodeSnippetStore(snippetFileFactory(provider)));
			}

			return services;
		}
	}
}