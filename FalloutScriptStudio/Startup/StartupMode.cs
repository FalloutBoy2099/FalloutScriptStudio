namespace FalloutScriptStudio.Startup
{
	/// <summary>What this process has to do after it was started.</summary>
	public enum StartupMode
	{
		/// <summary>Another instance owns the installation and the command line has arguments: hand them over and exit.</summary>
		ForwardToRunningInstance,

		/// <summary>Another instance owns the installation and there is nothing to hand over: tell the user and exit.</summary>
		AlreadyRunning,

		/// <summary>This is the first instance: open the main editor window.</summary>
		MainEditor,

		/// <summary>This is the first instance and the command line names a message (<c>.msg</c>) file: run the message editor alone.</summary>
		StandaloneMessageEditor,
	}
}
