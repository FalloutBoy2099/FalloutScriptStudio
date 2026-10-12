namespace FalloutScriptStudio.Startup
{
	using System;
	using System.IO;

	/// <summary>
	/// The decision about what a freshly started process does, computed from the command line and from whether this process owns the
	/// single-instance lock. Pure and immutable.
	/// </summary>
	/// <param name="Mode">What the process does.</param>
	/// <param name="MessageFilePath">The message file named by the command line (only for <see cref="StartupMode.StandaloneMessageEditor"/>).</param>
	public sealed record StartupPlan(StartupMode Mode, String? MessageFilePath = null)
	{
		/// <summary>Gets a value indicating whether the process continues with the start-up (it is the first instance).</summary>
		public Boolean IsPrimary => this.Mode is StartupMode.MainEditor or StartupMode.StandaloneMessageEditor;

		/// <summary>
		/// Gets a value indicating whether this process prepares the scratch folder and so alone may delete it when it exits. The
		/// stand-alone message editor shares the folder with a main editor that owns it, so it neither purges nor deletes it.
		/// </summary>
		public Boolean OwnsScratch => this.Mode == StartupMode.MainEditor;

		/// <summary>Decides what the process does.</summary>
		/// <param name="arguments">The command-line arguments.</param>
		/// <param name="isPrimaryInstance">Whether this process owns the single-instance lock.</param>
		/// <returns>The plan.</returns>
		public static StartupPlan Create(String[] arguments, Boolean isPrimaryInstance)
		{
			ArgumentNullException.ThrowIfNull(arguments);
			if (!isPrimaryInstance)
			{
				return new StartupPlan(arguments.Length > 0 ? StartupMode.ForwardToRunningInstance : StartupMode.AlreadyRunning);
			}

			return IsMessageFile(arguments) ? new StartupPlan(StartupMode.StandaloneMessageEditor, arguments[0]) : new StartupPlan(StartupMode.MainEditor);
		}

		private static Boolean IsMessageFile(String[] arguments)
		{
			return arguments.Length > 0 && String.Equals(Path.GetExtension(arguments[0]), ".msg", StringComparison.OrdinalIgnoreCase);
		}
	}
}
