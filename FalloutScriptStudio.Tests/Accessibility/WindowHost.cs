namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Windows;
	using System.Windows.Threading;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Support;
	using Wpf.Ui.Appearance;
	using Wpf.Ui.Markup;

	/// <summary>
	/// Shows a real window of the application off-screen, with the WPF-UI theme and the English texts merged into the resources of the
	/// window itself. No <see cref="Application"/> is created: it would change the behavior of the other tests that run in the same
	/// process (<see cref="Loc"/> reads the application resources as soon as one exists). The windows are shown without being activated, so they
	/// never take the keyboard focus from the windows of the other tests that run at the same time.
	/// </summary>
	internal static class WindowHost
	{
		/// <summary>
		/// Runs an action on a new STA thread that owns its own dispatcher, and waits for it. The windows of these tests do not share the UI thread
		/// of the other tests: processing the queued work of a window (layout, Loaded events, bindings) must not run the work of another test, and
		/// an exception of a handler of the window must be reported by this test only.
		/// </summary>
		/// <param name="action">The action; it runs inside the dispatcher.</param>
		public static void RunOnOwnThread(Action action)
		{
			ExceptionDispatchInfo? failure = null;
			Thread thread = new Thread(() =>
			{
				Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
				dispatcher.UnhandledException += (_, e) =>
				{
					failure ??= ExceptionDispatchInfo.Capture(e.Exception);
					e.Handled = true;
				};
				dispatcher.BeginInvoke(new Action(() =>
				{
					try
					{
						action();
					}
					catch (Exception ex)
					{
						failure ??= ExceptionDispatchInfo.Capture(ex);
					}
					finally
					{
						dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
					}
				}));
				Dispatcher.Run();
			})
			{
				Name = "Accessibility tests UI thread",
			};
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			failure?.Throw();
		}

		/// <summary>Processes the queued work of the current dispatcher (layout, render, Loaded events, bindings) until it is idle.</summary>
		public static void Pump()
		{
			DispatcherFrame frame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
			Dispatcher.PushFrame(frame);
		}

		/// <summary>Creates, themes and shows a window on a thread of its own; the callback inspects it, then it is closed.</summary>
		/// <typeparam name="T">The type of the window.</typeparam>
		/// <param name="create">Creates the window.</param>
		/// <param name="inspect">Inspects the shown window.</param>
		/// <param name="theme">The theme of the window.</param>
		public static void Run<T>(Func<T> create, Action<T> inspect, ApplicationTheme theme = ApplicationTheme.Dark)
			where T : Window
		{
			RunOnOwnThread(() =>
			{
				T? window = null;
				try
				{
					window = create();
					window.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = theme });
					window.Resources.MergedDictionaries.Add(new ControlsDictionary());
					window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/FalloutScriptStudio;component/Resources/ListStyles.xaml", UriKind.Absolute) });
					foreach (String area in Loc.Areas)
					{
						window.Resources.MergedDictionaries.Add(new ResourceDictionary
						{
							Source = new Uri($"pack://application:,,,/FalloutScriptStudio;component/Localization/Strings/{area}.en.xaml", UriKind.Absolute),
						});
					}

					OffscreenWindow.Prepare(window);
					window.Show();
					Pump();
					inspect(window);
				}
				finally
				{
					window?.Close();
					Pump();
				}
			});
		}
	}
}