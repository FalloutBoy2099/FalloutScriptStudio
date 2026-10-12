namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;

	/// <summary>Folder operations that return the problems the test configured.</summary>
	internal sealed class FakeStartupFolders : IStartupFolders
	{
		private readonly StartupEvents _events;

		public FakeStartupFolders(StartupEvents events)
		{
			this._events = events;
		}

		public IReadOnlyList<String> EnsureCreatedProblems { get; set; } = Array.Empty<String>();

		public IReadOnlyList<String> PrepareScratchProblems { get; set; } = Array.Empty<String>();

		public IReadOnlyList<String> DeleteScratchProblems { get; set; } = Array.Empty<String>();

		public IReadOnlyList<String> EnsureCreated()
		{
			this._events.Add("folders.EnsureCreated");
			return this.EnsureCreatedProblems;
		}

		public IReadOnlyList<String> PrepareScratch()
		{
			this._events.Add("folders.PrepareScratch");
			return this.PrepareScratchProblems;
		}

		public IReadOnlyList<String> DeleteScratch()
		{
			this._events.Add("folders.DeleteScratch");
			return this.DeleteScratchProblems;
		}
	}
}
