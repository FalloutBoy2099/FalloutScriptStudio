namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;

	/// <summary>A host that records what the start-up asked the user interface to do.</summary>
	internal sealed class FakeStartupHost : IStartupHost
	{
		private readonly StartupEvents _events;

		public FakeStartupHost(StartupEvents events)
		{
			this._events = events;
		}

		public List<String> Information { get; } = new List<String>();

		public List<String> Errors { get; } = new List<String>();

		public List<Int32> ShutdownCodes { get; } = new List<Int32>();

		public String? Language { get; private set; }

		public String[]? MainEditorArguments { get; private set; }

		public String? MessagePath { get; private set; }

		public Action? OnRunMainEditor { get; set; }

		public void InitializeLanguage(String? languageCode)
		{
			this._events.Add("host.InitializeLanguage");
			this.Language = languageCode;
		}

		public void ShowInformation(String message)
		{
			this._events.Add("host.ShowInformation");
			this.Information.Add(message);
		}

		public void ShowError(String message)
		{
			this._events.Add("host.ShowError");
			this.Errors.Add(message);
		}

		public void Shutdown(Int32 exitCode)
		{
			this._events.Add("host.Shutdown");
			this.ShutdownCodes.Add(exitCode);
		}

		public Task RunMainEditorAsync(IStartupServices services, ISingleInstance singleInstance, String[] arguments)
		{
			this._events.Add("host.RunMainEditor");
			this.MainEditorArguments = arguments;
			this.OnRunMainEditor?.Invoke();
			return Task.CompletedTask;
		}

		public void RunStandaloneMessageEditor(IStartupServices services, String path)
		{
			this._events.Add("host.RunStandaloneMessageEditor");
			this.MessagePath = path;
		}
	}
}
