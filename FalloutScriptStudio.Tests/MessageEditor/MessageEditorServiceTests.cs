namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public sealed class MessageEditorServiceTests
	{
		private static ServiceProvider BuildProvider(FakeSettingsService settings, FakeDialogService dialogs)
		{
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton<ISettingsService>(settings);
			services.AddSingleton<IDialogService>(dialogs);
			services.AddMessageEditor();
			return services.BuildServiceProvider();
		}

		[Fact]
		public void RegistrationProvidesTheServiceAndTransientViewModels()
		{
			StaTestRunner.Run(() =>
			{
				using TestFolder folder = new TestFolder("msgeditor-app");
				using ServiceProvider provider = BuildProvider(new FakeSettingsService(folder.Path), new FakeDialogService());

				IMessageEditorService service = provider.GetRequiredService<IMessageEditorService>();
				MessageEditorViewModel first = provider.GetRequiredService<MessageEditorViewModel>();
				MessageEditorViewModel second = provider.GetRequiredService<MessageEditorViewModel>();

				Assert.Same(service, provider.GetRequiredService<IMessageEditorService>());
				Assert.NotSame(first, second);
				Assert.Equal(0, service.OpenWindowCount);
				Assert.NotNull(provider.GetRequiredService<IMessageFileService>());
				Assert.NotNull(provider.GetRequiredService<IMessageStructureChecker>());
				Assert.NotNull(provider.GetRequiredService<IMessageEditorPlatform>());
				Assert.NotNull(provider.GetRequiredService<HighlightingProvider>());
			});
		}

		[Fact]
		public void RegistrationKeepsServicesTheApplicationRegisteredFirst()
		{
			StaTestRunner.Run(() =>
			{
				FakeDialogService dialogs = new FakeDialogService();
				using TestFolder folder = new TestFolder("msgeditor-app");
				using ServiceProvider provider = BuildProvider(new FakeSettingsService(folder.Path), dialogs);

				Assert.Same(dialogs, provider.GetRequiredService<IDialogService>());
			});
		}

		[Fact]
		public void OpeningAScriptWithoutAMessageFileAndWithoutCreationDoesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				using ServiceProvider provider = BuildProvider(harness.Settings, harness.Dialogs);
				IMessageEditorService service = provider.GetRequiredService<IMessageEditorService>();
				String script = Path.Combine(harness.Folder, "hakunin.ssl");

				Boolean opened = await service.OpenForScriptAsync(script, null, new FakeLineTarget(), createIfMissing: false);

				Assert.False(opened);
				Assert.Equal(0, service.OpenWindowCount);
				Assert.Equal(0, harness.Dialogs.ConfirmCount);
			});
		}

		[Fact]
		public void DecliningTheCreationOfTheMessageFileCreatesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				harness.Dialogs.ConfirmAnswer = false;
				using ServiceProvider provider = BuildProvider(harness.Settings, harness.Dialogs);
				IMessageEditorService service = provider.GetRequiredService<IMessageEditorService>();
				String script = Path.Combine(harness.Folder, "hakunin.ssl");

				Boolean opened = await service.OpenForScriptAsync(script, null, new FakeLineTarget());

				Assert.False(opened);
				Assert.Equal(1, harness.Dialogs.ConfirmCount);
				Assert.False(File.Exists(Path.Combine(harness.Folder, "hakunin.msg")));
			});
		}

		[Fact]
		public void AMissingScriptsListIsReported()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness(settings =>
				{
					settings.Settings.Messages.AssociateByScriptId = true;
				});
				harness.Settings.Settings.Paths.OutputDirectory = harness.Folder;
				using ServiceProvider provider = BuildProvider(harness.Settings, harness.Dialogs);
				IMessageEditorService service = provider.GetRequiredService<IMessageEditorService>();

				Boolean opened = await service.OpenForScriptAsync(Path.Combine(harness.Folder, "hakunin.ssl"), 5, new FakeLineTarget(), createIfMissing: false);

				Assert.False(opened);
				Assert.Contains(harness.Dialogs.Messages, message => message.Contains("scripts.lst", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void ABadScriptsListEntryStopsTheLookup()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness(settings =>
				{
					settings.Settings.Messages.AssociateByScriptId = true;
				});
				harness.Settings.Settings.Paths.OutputDirectory = harness.Folder;
				File.WriteAllLines(Path.Combine(harness.Folder, "scripts.lst"), new[] { "hakunin.int ; Hakunin" });
				using ServiceProvider provider = BuildProvider(harness.Settings, harness.Dialogs);
				IMessageEditorService service = provider.GetRequiredService<IMessageEditorService>();

				Boolean opened = await service.OpenForScriptAsync(Path.Combine(harness.Folder, "hakunin.ssl"), 99, new FakeLineTarget());

				Assert.False(opened);
				Assert.Equal(0, harness.Dialogs.ConfirmCount);
				Assert.Single(harness.Dialogs.Messages);
			});
		}
	}
}