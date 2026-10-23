namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Windows.Automation;
	using Xunit;

	/// <summary>
	/// Runs the named steps of a smoke test one after the other: a step that fails is reported (with a picture of the windows that are open)
	/// and the stray windows it left are closed, so that the later steps still run and one run shows every defect. <see cref="Finish"/> fails
	/// the test when a step failed.
	/// </summary>
	internal sealed class SmokeSteps
	{
		private readonly AppSession _app;
		private readonly List<String> _failures = [];

		/// <summary>Initializes a new instance of the <see cref="SmokeSteps"/> class.</summary>
		/// <param name="app">The program under test.</param>
		public SmokeSteps(AppSession app)
		{
			this._app = app;
		}

		/// <summary>
		/// Runs a step. A step that fails is run once more after the stray windows and menus are closed: driving a program through UI Automation
		/// without ever giving it the focus loses a request now and then (a menu that closed by itself); a step that fails twice fails the test.
		/// </summary>
		/// <param name="name">The name of the step.</param>
		/// <param name="action">The step.</param>
		public void Run(String name, Action action)
		{
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					action();
					this._app.Step("ok: " + name + (attempt > 1 ? " (second attempt)" : String.Empty));
					return;
				}
				catch (Exception ex) when (ex is not OutOfMemoryException)
				{
					String message = name + ": " + ex.GetType().Name + ": " + ex.Message.Replace('\r', ' ').Replace('\n', ' ');
					if (attempt == 1)
					{
						this._app.Step("RETRY " + message);
						this.CloseStrayWindows();
						System.Threading.Thread.Sleep(500);
						continue;
					}

					this._failures.Add(message);
					this._app.Step("FAILED " + message);
					this.CaptureOpenWindows("failed-" + Sanitize(name));
					this.CloseStrayWindows();
					return;
				}
			}
		}

		/// <summary>Fails the test when a step failed.</summary>
		public void Finish()
		{
			Assert.True(this._failures.Count == 0, "Steps failed (" + this._failures.Count + "):" + Environment.NewLine + String.Join(Environment.NewLine, this._failures));
		}

		/// <summary>Closes every window of the program except the main window, and collapses the menus.</summary>
		public void CloseStrayWindows()
		{
			try
			{
				this._app.CloseMenus();
				foreach (AppWindow window in this._app.Windows().Where(candidate => candidate.Handle != this._app.MainWindow.Handle).Reverse().ToList())
				{
					this._app.RequestClose(window);
				}

				Uia.WaitUntil(() => this._app.Windows().All(candidate => candidate.Handle == this._app.MainWindow.Handle), "the stray windows to close", TimeSpan.FromSeconds(6));
			}
			catch (Exception ex) when (ex is TimeoutException or ElementNotAvailableException or COMException or InvalidOperationException)
			{
				this._app.Step("could not close the stray windows: " + ex.Message);
			}
		}

		private static String Sanitize(String name)
		{
			return new String(name.Select(character => Char.IsLetterOrDigit(character) ? character : '-').ToArray());
		}

		private void CaptureOpenWindows(String prefix)
		{
			try
			{
				Int32 index = 0;
				foreach (AppWindow window in this._app.Windows())
				{
					this._app.Shot(window, prefix + "-" + index++, mayBeBlank: true);
				}
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				this._app.Step("could not capture the windows of the failed step: " + ex.Message);
			}
		}
	}
}