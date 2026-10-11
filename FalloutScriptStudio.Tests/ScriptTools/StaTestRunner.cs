namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Threading;

	/// <summary>
	/// Runs a test body on a single STA thread with a WPF dispatcher, like the UI thread of the application.
	/// The view model owns WPF collection views that must be used from one thread only.
	/// </summary>
	internal static class StaTestRunner
	{
		public static void Run(Func<Task> body)
		{
			ExceptionDispatchInfo? failure = null;
			Thread thread = new Thread(() =>
			{
				Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
				Task task = body();
				task.ContinueWith(
					completed =>
					{
						if (completed.Exception is not null)
						{
							failure = ExceptionDispatchInfo.Capture(completed.Exception.GetBaseException());
						}

						dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
					},
					TaskScheduler.Default);
				Dispatcher.Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			failure?.Throw();
		}

		public static void Run(Action body)
		{
			Run(() =>
			{
				body();
				return Task.CompletedTask;
			});
		}
	}
}
