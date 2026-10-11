namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;

	/// <summary>Hands out <see cref="FakeParserHost"/> instances and records them.</summary>
	internal sealed class FakeParserHostLauncher : IParserHostLauncher
	{
		private readonly Func<Int32, FakeParserHost> _factory;

		public FakeParserHostLauncher(Func<Int32, FakeParserHost> factory)
		{
			this._factory = factory;
		}

		/// <summary>Gets the hosts started so far, in order.</summary>
		public List<FakeParserHost> Started { get; } = new List<FakeParserHost>();

		/// <summary>Gets the working directories the hosts were started in.</summary>
		public List<String> WorkingDirectories { get; } = new List<String>();

		/// <summary>Gets or sets an exception thrown by <see cref="Start"/> instead of starting a host.</summary>
		public Exception? StartException { get; set; }

		public IParserHostProcess Start(String hostPath, String workingDirectory)
		{
			if (this.StartException is not null)
			{
				throw this.StartException;
			}

			FakeParserHost host = this._factory(this.Started.Count);
			this.Started.Add(host);
			this.WorkingDirectories.Add(workingDirectory);
			return host;
		}
	}
}
