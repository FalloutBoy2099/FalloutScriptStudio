namespace FalloutScriptStudio.Services.MessageEditor
{
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Core.Messages;
	using Editor.Highlighting;
	using ViewModels.MessageEditor;
	using Views.MessageEditor;

	/// <summary>
	/// Registers the Message Editor.
	/// </summary>
	public static class MessageEditorServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the Message Editor: <see cref="IMessageEditorService"/> (singleton), the window and its view model (transient)
		/// and the Core services it needs. Services that are already registered are kept.
		/// </summary>
		/// <remarks>
		/// The application has to register <c>ISettingsService</c> itself; <see cref="IDialogService"/> and <see cref="IThemeService"/>
		/// are registered here only if nobody did it before.
		/// </remarks>
		/// <param name="services">The service collection.</param>
		/// <returns>The same collection.</returns>
		public static IServiceCollection AddMessageEditor(this IServiceCollection services)
		{
			services.AddLogging();
			services.TryAddSingleton<IMessageFileService, MessageFileService>();
			services.TryAddSingleton<IMessageStructureChecker, MessageStructureChecker>();
			services.TryAddSingleton<IDialogService, DialogService>();
			services.TryAddSingleton<IThemeService, ThemeService>();
			services.TryAddSingleton<HighlightingProvider>();
			services.TryAddSingleton<IMessageEditorPlatform, MessageEditorPlatform>();
			services.TryAddSingleton<IMessageEditorService, MessageEditorService>();
			services.TryAddTransient<MessageEditorViewModel>();
			services.TryAddTransient<MessageEditorWindow>();
			return services;
		}
	}
}