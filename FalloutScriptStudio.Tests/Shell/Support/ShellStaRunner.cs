namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Threading;

	/// <summary>
	/// Runs a test body on one STA thread with a WPF dispatcher, like the UI thread of the application: the AvalonEdit
	/// documents and the view models must be used from a single thread, while xunit would resume awaits on any pool thread.
	/// </summary>
	internal static class ShellStaRunner
	{
		public static void Run(Func<Task> body)
		{
			ExceptionDispatchInfo? failure = null;
			Thread thread = new Thread(() =>
			{
				Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
				dispatcher.BeginInvoke(new Action(async () =>
				{
					try
					{
						await body();
					}
					catch (Exception ex)
					{
						failure = ExceptionDispatchInfo.Capture(ex);
					}
					finally
					{
						dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
					}
				}));
				Dispatcher.Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			failure?.Throw();
		}
	}
}