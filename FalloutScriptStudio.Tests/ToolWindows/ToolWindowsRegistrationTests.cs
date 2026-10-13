namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Views.ToolWindows;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public sealed class ToolWindowsRegistrationTests
	{
		[Fact]
		public void AddToolWindows_RegistersTheServiceWindowsAndViewModels()
		{
			ServiceCollection services = new ServiceCollection();

			services.AddToolWindows();

			ServiceDescriptor service = Assert.Single(services, d => d.ServiceType == typeof(IToolWindowService));
			Assert.Equal(typeof(ToolWindowService), service.ImplementationType);
			Assert.Equal(ServiceLifetime.Singleton, service.Lifetime);

			foreach (Type type in new[]
			{
				typeof(SettingsViewModel), typeof(AboutViewModel), typeof(GoToLineViewModel), typeof(BatchCompileViewModel), typeof(HeadersViewModel), typeof(ProjectSettingsViewModel),
				typeof(SettingsWindow), typeof(AboutWindow), typeof(GoToLineWindow), typeof(BatchCompileWindow), typeof(HeadersWindow), typeof(ProjectSettingsWindow),
			})
			{
				ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == type);
				Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
			}
		}

		[Fact]
		public void AddToolWindows_ViewModelsResolveWithTheCoreServices()
		{
			ServiceCollection services = new ServiceCollection();
			FakeSettingsService settings = new FakeSettingsService(@"C:\fss");
			services.AddSingleton<ISettingsService>(settings);
			services.AddSingleton<IProjectService>(new ProjectService(settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled));
			services.AddSingleton<IThemeService>(new FakeThemeService());
			services.AddSingleton<IDialogService>(new FakeDialogService());
			services.AddSingleton<IBatchCompileService>(new FakeBatchCompileService());
			services.AddToolWindows();

			using ServiceProvider provider = services.BuildServiceProvider();

			Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());
			Assert.NotNull(provider.GetRequiredService<AboutViewModel>());
			Assert.NotNull(provider.GetRequiredService<GoToLineViewModel>());
			Assert.NotNull(provider.GetRequiredService<BatchCompileViewModel>());
			Assert.NotNull(provider.GetRequiredService<HeadersViewModel>());
			Assert.NotNull(provider.GetRequiredService<ProjectSettingsViewModel>());
			Assert.NotNull(provider.GetRequiredService<IToolWindowService>());
			Assert.NotSame(provider.GetRequiredService<GoToLineViewModel>(), provider.GetRequiredService<GoToLineViewModel>());
		}

		[Fact]
		public void AddToolWindows_KeepsExistingRegistrations()
		{
			ServiceCollection services = new ServiceCollection();
			IFileAssociationService association = new FakeFileAssociationService();
			services.AddSingleton(association);

			services.AddToolWindows();

			Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IFileAssociationService)));
		}
	}
}
