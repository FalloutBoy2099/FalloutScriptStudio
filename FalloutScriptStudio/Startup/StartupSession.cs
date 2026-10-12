namespace FalloutScriptStudio.Startup
{
	using System;

	/// <summary>What the start-up of this process produced and the exit has to release or decide on.</summary>
	public sealed class StartupSession : IDisposable
	{
		/// <summary>Gets or sets the single-instance lock this process holds (<see langword="null"/> when it holds none).</summary>
		public ISingleInstance? SingleInstance { get; set; }

		/// <summary>Gets or sets the services created by the start-up.</summary>
		public IStartupServices? Services { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the settings were read: until then the service holds blank defaults that must never reach the files.
		/// </summary>
		public Boolean SettingsLoaded { get; set; }

		/// <summary>Gets or sets a value indicating whether this process prepared the scratch folder and so alone may delete it.</summary>
		public Boolean OwnsScratch { get; set; }

		/// <summary>Releases the single instance lock and the services (which disposes the singletons they created).</summary>
		public void Dispose()
		{
			this.SingleInstance?.Dispose();
			this.SingleInstance = null;
			this.Services?.Dispose();
			this.Services = null;
		}
	}
}
