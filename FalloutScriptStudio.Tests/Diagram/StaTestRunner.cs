namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Windows.Threading;

	/// <summary>
	/// Runs a test body on a dedicated STA thread, as WPF objects require.
	/// </summary>
	internal static class StaTestRunner
	{
		public static void Run(Action action)
		{
			Exception? error = null;
			Thread thread = new Thread(() =>
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					error = ex;
				}
				finally
				{
					Dispatcher.CurrentDispatcher.InvokeShutdown();
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (error != null)
			{
				ExceptionDispatchInfo.Capture(error).Throw();
			}
		}
	}
}
