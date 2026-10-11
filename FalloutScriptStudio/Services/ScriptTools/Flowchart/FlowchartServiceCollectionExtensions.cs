namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.Views.DialoguePreview;
	using FalloutScriptStudio.Views.Flowchart;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Registers the dialogue flowchart, the node code editor and the dialogue preview.
	/// </summary>
	public static class FlowchartServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the services (singletons) and the windows with their view models (transient) of the flowchart, the node code
		/// editor and the dialogue preview. The windows need <c>IWorkspace</c>, <c>ISettingsService</c>, <c>IDialogService</c>,
		/// <c>IMessageFileService</c>, <c>IMessageEditorService</c>, <c>IScriptToolService</c> and <c>ScriptEditorServices</c> from the
		/// composition root, and the dialogue tools (<c>AddDialogueTools</c>) and text tools (<c>AddTextTools</c>) of the Core library.
		/// The node snippet store (<c>INodeCodeSnippetStore</c>) is optional.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection.</returns>
		public static IServiceCollection AddFlowchartTools(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddSingleton<IDialogueMessageService, DialogueMessageService>();
			services.AddSingleton<DialogueScriptEditor>();
			services.AddSingleton<IFlowchartDialogService, FlowchartDialogService>();
			services.AddSingleton<INodeCodeEditorService, NodeCodeEditorService>();

			services.AddTransient<FlowchartViewModel>();
			services.AddTransient<FlowchartWindow>();
			services.AddTransient<NodeCodeEditorViewModel>();
			services.AddTransient<NodeCodeEditorWindow>();
			services.AddTransient<NewNodeViewModel>();
			services.AddTransient<NewNodeWindow>();
			services.AddTransient<MessageTextViewModel>();
			services.AddTransient<MessageTextWindow>();
			services.AddTransient<DialoguePreviewViewModel>();
			services.AddTransient<DialoguePreviewWindow>();
			return services;
		}
	}
}
