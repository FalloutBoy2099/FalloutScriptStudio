namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Windows;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Startup;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.Views.Shell;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>Builds the real container of the application (the registrations of <see cref="AppServices"/>) over a scratch installation.</summary>
	public sealed class CompositionTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("composition");

		public void Dispose()
		{
			this._folder.Dispose();
		}

		private static readonly ServiceProviderOptions _strict = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };

		private ServiceCollection CreateCollection(out SingleInstanceService singleInstance)
		{
			AppPaths paths = AppPaths.ForInstallation(this._folder.Path);
			singleInstance = new SingleInstanceService(this._folder.Path);
			ServiceCollection services = new ServiceCollection();
			AppServices.ConfigureServices(services, paths, NullLoggerProvider.Instance, singleInstance);
			return services;
		}

		private static List<Type> GetClosedServiceTypes(ServiceCollection services)
		{
			return services
				.Select(descriptor => descriptor.ServiceType)
				.Where(type => !type.ContainsGenericParameters)
				.Distinct()
				.ToList();
		}

		/// <summary>
		/// Resolves a service. A window is not constructed: its XAML needs the resources of a running <c>App</c> (the WPF-UI styles
		/// of <c>App.xaml</c>) and the pack resources of the application assembly, neither of which exists in the test host, and
		/// WPF allows a single <c>Application</c> per process, which would change <c>Loc</c> and the theme for every other test.
		/// For a window the constructor is resolved instead: every dependency it asks the container for is created.
		/// </summary>
		private static void Resolve(ServiceProvider provider, Type type)
		{
			if (!typeof(Window).IsAssignableFrom(type))
			{
				Assert.NotEmpty(provider.GetServices(type));
				return;
			}

			ConstructorInfo constructor = type.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
			foreach (ParameterInfo parameter in constructor.GetParameters())
			{
				Object? dependency = provider.GetService(parameter.ParameterType);
				Assert.True(dependency is not null || parameter.HasDefaultValue, "no service " + parameter.ParameterType.FullName + " for the parameter " + parameter.Name);
			}
		}

		[Fact]
		public void Container_IsValidOnBuild()
		{
			UiThread.Run(() =>
			{
				ServiceCollection services = this.CreateCollection(out SingleInstanceService singleInstance);

				using (singleInstance)
				using (ServiceProvider provider = services.BuildServiceProvider(_strict))
				{
					Assert.NotNull(provider);
				}
			});
		}

		[Fact]
		public void EveryRegisteredService_CanBeResolved()
		{
			UiThread.Run(() =>
			{
				ServiceCollection services = this.CreateCollection(out SingleInstanceService singleInstance);
				List<String> failures = [];

				using (singleInstance)
				using (ServiceProvider provider = services.BuildServiceProvider(_strict))
				{
					foreach (Type type in GetClosedServiceTypes(services))
					{
						try
						{
							Resolve(provider, type);
						}
						catch (Exception ex)
						{
							failures.Add(type.FullName + ": " + ex.GetBaseException().Message);
						}
					}
				}

				Assert.True(failures.Count == 0, String.Join(Environment.NewLine, failures));
			});
		}

		[Fact]
		public void EveryViewModelAndWindow_IsRegisteredAndCanBeResolved()
		{
			UiThread.Run(() =>
			{
				ServiceCollection services = this.CreateCollection(out SingleInstanceService singleInstance);
				List<Type> types = GetClosedServiceTypes(services)
					.Where(type => type.Namespace is not null && type.Namespace.StartsWith("FalloutScriptStudio", StringComparison.Ordinal))
					.Where(type => type.Name.EndsWith("ViewModel", StringComparison.Ordinal) || typeof(Window).IsAssignableFrom(type))
					.ToList();
				List<String> failures = [];

				using (singleInstance)
				using (ServiceProvider provider = services.BuildServiceProvider(_strict))
				{
					Assert.Contains(typeof(MainViewModel), types);
					Assert.Contains(typeof(MainWindow), types);
					foreach (Type type in types)
					{
						try
						{
							Resolve(provider, type);
						}
						catch (Exception ex)
						{
							failures.Add(type.FullName + ": " + ex.GetBaseException().Message);
						}
					}
				}

				Assert.True(failures.Count == 0, String.Join(Environment.NewLine, failures));
			});
		}

		[Fact]
		public void MainWindowAndItsViewModel_AreRegisteredAsSingletons()
		{
			UiThread.Run(() =>
			{
				ServiceCollection services = this.CreateCollection(out SingleInstanceService singleInstance);

				using (singleInstance)
				using (ServiceProvider provider = services.BuildServiceProvider(_strict))
				{
					Assert.Same(provider.GetRequiredService<MainViewModel>(), provider.GetRequiredService<MainViewModel>());
					Assert.All(services.Where(descriptor => descriptor.ServiceType == typeof(MainWindow) || descriptor.ServiceType == typeof(MainViewModel)), descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
				}
			});
		}

		[Fact]
		public void SingleInstance_IsTheOneOfTheProcess()
		{
			UiThread.Run(() =>
			{
				ServiceCollection services = this.CreateCollection(out SingleInstanceService singleInstance);

				using (singleInstance)
				using (ServiceProvider provider = services.BuildServiceProvider(_strict))
				{
					Assert.Same(singleInstance, provider.GetRequiredService<ISingleInstance>());
				}
			});
		}
	}
}