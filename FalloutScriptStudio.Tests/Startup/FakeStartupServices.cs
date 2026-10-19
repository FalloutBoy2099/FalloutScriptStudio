namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>Start-up services that record the calls and throw what the test configured.</summary>
	internal sealed class FakeStartupServices : IStartupServices
	{
		private readonly StartupEvents _events;

		public FakeStartupServices(StartupEvents events)
		{
			this._events = events;
		}

		public IServiceProvider Provider { get; } = new ServiceCollection().BuildServiceProvider();

		public Exception? LoadSettingsFailure { get; set; }

		public Exception? LoadStoresFailure { get; set; }

		public Exception? SaveFailure { get; set; }

		public String? StoredLanguage { get; set; }

		public Int32 DisposeCount { get; private set; }

		public void LoadSettings()
		{
			this._events.Add("services.LoadSettings");
			if (this.LoadSettingsFailure is not null)
			{
				throw this.LoadSettingsFailure;
			}
		}

		public void SaveSettings()
		{
			this._events.Add("services.SaveSettings");
			if (this.SaveFailure is not null)
			{
				throw this.SaveFailure;
			}
		}

		public void ActivateProject()
		{
			this._events.Add("services.ActivateProject");
		}

		public Task LoadStoresAsync()
		{
			this._events.Add("services.LoadStores");
			return this.LoadStoresFailure is null ? Task.CompletedTask : Task.FromException(this.LoadStoresFailure);
		}

		public void ApplyStartupTheme()
		{
			this._events.Add("services.ApplyTheme");
		}

		public String? GetStoredLanguage()
		{
			this._events.Add("services.GetStoredLanguage");
			return this.StoredLanguage;
		}

		public void Dispose()
		{
			this._events.Add("services.Dispose");
			this.DisposeCount++;
		}
	}
}