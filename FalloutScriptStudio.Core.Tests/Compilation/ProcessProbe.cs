namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;

	/// <summary>Looks for running processes. A process that ended but is still listed because someone holds a handle to it does not count.</summary>
	internal static class ProcessProbe
	{
		/// <summary>Counts the running processes of a name.</summary>
		/// <param name="name">The process name without extension.</param>
		/// <param name="folder">When given, only processes whose program file is below this folder count (other tests may run the same tool).</param>
		/// <returns>The number of processes that are still running.</returns>
		public static Int32 CountAlive(String name, String? folder = null)
		{
			Int32 count = 0;
			foreach (Process process in Process.GetProcessesByName(name))
			{
				using (process)
				{
					try
					{
						if (process.HasExited)
						{
							continue;
						}

						if (folder is not null && !(process.MainModule?.FileName ?? String.Empty).StartsWith(folder, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						count++;
					}
					catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
					{
						// It ended while it was being looked at.
					}
				}
			}

			return count;
		}

		/// <summary>Kills every running process of a name (clean-up of a test).</summary>
		/// <param name="name">The process name without extension.</param>
		public static void KillAll(String name)
		{
			foreach (Process process in Process.GetProcessesByName(name))
			{
				using (process)
				{
					try
					{
						if (!process.HasExited)
						{
							process.Kill(true);
						}
					}
					catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
					{
						// It ended meanwhile.
					}
				}
			}
		}
	}
}