namespace FalloutScriptStudio.Services.ScriptTools.Dialogs
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Editor.Highlighting;
	using ViewModels.ScriptTools.Dialogs;
	using Views.ScriptTools.Dialogs;

	/// <summary>
	/// Registration of the dialogue dialogs in the service collection.
	/// </summary>
	public static class DialogueDialogsServiceCollectionExtensions
	{
		/// <summary>
		/// Registers <see cref="IDialogueDialogsService"/> (singleton), the three windows and their view models (transient).
		/// <c>IDialogService</c>, <c>IThemeService</c> and <c>IDialogueRulesService</c> must be registered by the caller;
		/// <see cref="HighlightingProvider"/> is added when nobody registered it yet.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The same service collection.</returns>
		public static IServiceCollection AddDialogueDialogs(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.TryAddSingleton<HighlightingProvider>();
			services.AddSingleton<IDialogueDialogsService, DialogueDialogsService>();

			services.AddTransient<DialogueRulesViewModel>();
			services.AddTransient<UserFunctionViewModel>();
			services.AddTransient<MacroRenamePreviewViewModel>();

			services.AddTransient<DialogueRulesWindow>();
			services.AddTransient<UserFunctionWindow>();
			services.AddTransient<MacroRenamePreviewWindow>();

			return services;
		}
	}
}