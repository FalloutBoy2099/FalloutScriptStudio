namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.Threading;
	using System.Windows.Threading;

	/// <summary>
	/// Runs WPF code on one dedicated STA thread with a dispatcher that lives as long as the test process, because WPF
	/// controls can only be created on an STA thread.
	/// </summary>
	public static class UiThread
	{
		private static readonly Lazy<Dispatcher> _dispatcher = new Lazy<Dispatcher>(Start);

		/// <summary>Runs an action on the UI thread and waits for it; exceptions are rethrown.</summary>
		/// <param name="action">The action.</param>
		public static void Run(Action action)
		{
			_dispatcher.Value.Invoke(action);
		}

		/// <summary>Runs a function on the UI thread and returns its result.</summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="function">The function.</param>
		/// <returns>The result.</returns>
		public static T Run<T>(Func<T> function)
		{
			return _dispatcher.Value.Invoke(function);
		}

		/// <summary>Processes the queued dispatcher work (input, loaded, render priority) on the UI thread.</summary>
		public static void Flush()
		{
			_dispatcher.Value.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
		}

		private static Dispatcher Start()
		{
			Dispatcher? dispatcher = null;
			using ManualResetEventSlim ready = new ManualResetEventSlim();
			Thread thread = new Thread(() =>
			{
				dispatcher = Dispatcher.CurrentDispatcher;
				ready.Set();
				Dispatcher.Run();
			})
			{
				IsBackground = true,
				Name = "Editor tests UI thread",
			};
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			ready.Wait();
			return dispatcher!;
		}
	}
}
