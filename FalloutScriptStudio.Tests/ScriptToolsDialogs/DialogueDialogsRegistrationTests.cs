namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Dialogs;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public sealed class DialogueDialogsRegistrationTests
	{
		[Fact]
		public void AddDialogueDialogs_RegistersTheServiceAsSingletonAndTheViewModelsAsTransient()
		{
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton<IDialogService>(new FakeDialogService());
			services.AddSingleton<IDialogueRulesService>(new FakeDialogueRulesService());
			services.AddDialogueDialogs();
			using ServiceProvider provider = services.BuildServiceProvider();

			IDialogueDialogsService first = provider.GetRequiredService<IDialogueDialogsService>();
			IDialogueDialogsService second = provider.GetRequiredService<IDialogueDialogsService>();

			Assert.IsType<DialogueDialogsService>(first);
			Assert.Same(first, second);
			Assert.NotSame(provider.GetRequiredService<DialogueRulesViewModel>(), provider.GetRequiredService<DialogueRulesViewModel>());
			Assert.NotSame(provider.GetRequiredService<UserFunctionViewModel>(), provider.GetRequiredService<UserFunctionViewModel>());
			Assert.NotSame(provider.GetRequiredService<MacroRenamePreviewViewModel>(), provider.GetRequiredService<MacroRenamePreviewViewModel>());
			Assert.NotNull(provider.GetRequiredService<HighlightingProvider>());
		}
	}
}
