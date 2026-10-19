namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Collections.Generic;

	/// <summary>Writes the problems of the folder operations to the log.</summary>
	internal static class StartupLogging
	{
		public static void LogProblems(Action<String> log, String operation, IReadOnlyList<String> problems)
		{
			foreach (String problem in problems)
			{
				log(operation + ": " + problem);
			}
		}
	}
}