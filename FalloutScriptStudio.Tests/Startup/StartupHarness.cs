namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;

	/// <summary>A start-up coordinator wired to fakes, with the record of everything that happened.</summary>
	internal sealed class StartupHarness
	{
		public StartupHarness(params Boolean[] acquireResults)
		{
			this.Events = new StartupEvents();
			this.Host = new FakeStartupHost(this.Events);
			this.Folders = new FakeStartupFolders(this.Events);
			this.SingleInstance = new FakeSingleInstance(this.Events, acquireResults);
			this.Services = new FakeStartupServices(this.Events);
			this.Session = new StartupSession();
			this.Coordinator = new StartupCoordinator(
				this.Host,
				this.Folders,
				() => this.SingleInstance,
				_ =>
				{
					this.Events.Add("services.Create");
					return this.Services;
				},
				this.Session,
				line => this.Log.Add(line));
			this.Shutdown = new ShutdownCoordinator(this.Session, this.Folders, line => this.Log.Add(line));
		}

		public StartupEvents Events { get; }

		public FakeStartupHost Host { get; }

		public FakeStartupFolders Folders { get; }

		public FakeSingleInstance SingleInstance { get; }

		public FakeStartupServices Services { get; }

		public StartupSession Session { get; }

		public StartupCoordinator Coordinator { get; }

		public ShutdownCoordinator Shutdown { get; }

		public List<String> Log { get; } = [];

		public Task RunAsync(params String[] arguments)
		{
			return this.Coordinator.RunAsync(arguments);
		}
	}
}