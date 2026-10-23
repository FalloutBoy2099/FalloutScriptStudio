namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// A running copy of the real program started in its UI test mode (<c>FSS_UI_TEST=1</c>: every window off-screen, nothing activated, private
	/// settings folder), together with what a test needs to drive it with UI Automation, to capture its windows and to check its log.
	/// Disposing it ends the program and deletes its data folder.
	/// </summary>
	internal sealed class AppSession : IDisposable
	{
		private static readonly String[] ParserStatusKeys =
		[
			"Str.Shell.Parser.Builtin",
			"Str.Shell.Parser.Done",
			"Str.Shell.Parser.FailedLog",
			"Str.Shell.Parser.FailedParserLog",
			"Str.Shell.Parser.Idle",
			"Str.Shell.Parser.NoFile",
			"Str.Shell.Parser.NotSsl",
			"Str.Shell.Parser.OffMacros",
			"Str.Shell.Parser.Running",
			"Str.Shell.Parser.SyntaxError",
			"Str.Shell.Parser.Waiting",
		];

		private static readonly String[] LogFailures = ["[Error]", "[Critical]", "Unhandled exception", "Fatal exception", "Unobserved task exception", "Start-up failed"];

		private readonly UiSmokeSuite _suite;
		private readonly String _test;
		private readonly TestFolder _data;
		private readonly Process _process;
		private readonly String _programDirectory;
		private readonly List<String> _captures = [];
		private AppWindow? _main;
		private Boolean _closed;

		private AppSession(UiSmokeSuite suite, String test, TestFolder data, Process process, String programDirectory, AppLaunch launch)
		{
			this._suite = suite;
			this._test = test;
			this._data = data;
			this._process = process;
			this._programDirectory = programDirectory;
			this.Launch = launch;
		}

		/// <summary>Gets how the program was started.</summary>
		public AppLaunch Launch { get; }

		/// <summary>Gets the process.</summary>
		public Process Process => this._process;

		/// <summary>Gets the data folder of the program (settings, scratch folder, log).</summary>
		public String DataDirectory => this._data.Path;

		/// <summary>Gets the folder of the program that runs.</summary>
		public String ProgramDirectory => this._programDirectory;

		/// <summary>Gets the log file of the program.</summary>
		public String LogPath => Path.Combine(this._data.Path, "sse.log");

		/// <summary>Gets the names of the pictures taken so far.</summary>
		public IReadOnlyList<String> Captures => this._captures;

		/// <summary>Gets the main window.</summary>
		public AppWindow MainWindow => this._main ?? throw new InvalidOperationException("The main window is not known yet.");

		/// <summary>Starts the program.</summary>
		/// <param name="suite">The shared state of the run.</param>
		/// <param name="test">The name of the test (the folder of its pictures).</param>
		/// <param name="launch">How to start the program; the defaults when <see langword="null"/>.</param>
		/// <returns>The session, with the main window open.</returns>
		public static AppSession Start(UiSmokeSuite suite, String test, AppLaunch? launch = null)
		{
			ArgumentNullException.ThrowIfNull(suite);
			ArgumentNullException.ThrowIfNull(test);
			NativeMethods.MakeDpiAware();
			launch ??= new AppLaunch();

			String program = launch.ProgramDirectory ?? (launch.WithoutParser ? suite.ProgramCopyWithoutParser : suite.ProgramCopy);
			TestFolder data = new("ui-smoke-data");
			try
			{
				Seed(data, launch);
				ProcessStartInfo start = new(Path.Combine(program, launch.Executable))
				{
					UseShellExecute = false,
					WorkingDirectory = program,
				};
				foreach (String argument in launch.Arguments)
				{
					start.ArgumentList.Add(argument);
				}

				start.Environment["FSS_UI_TEST"] = "1";
				start.Environment["FSS_UI_TEST_DATA"] = data.Path;
				start.Environment["FSS_UI_TEST_LANGUAGE"] = launch.Language;
				start.Environment["FSS_UI_TEST_THEME"] = launch.Theme;

				// A single-file executable unpacks its native libraries to a folder of %TEMP% unless told otherwise: the folder is inside the data folder.
				start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = Path.Combine(data.Path, "bundle");
				Process process = Process.Start(start) ?? throw new InvalidOperationException("The program did not start.");
				AppSession session = new(suite, test, data, process, program, launch);
				try
				{
					session.WaitForMainWindow();
					session.Step("started " + launch.Executable + " " + String.Join(' ', launch.Arguments.Select(Path.GetFileName)) + " language=" + launch.Language + " theme=" + launch.Theme);
					return session;
				}
				catch
				{
					session.Dispose();
					throw;
				}
			}
			catch
			{
				data.Dispose();
				throw;
			}
		}

		/// <summary>Lists the top-level windows of the program (not the popups and menus).</summary>
		/// <returns>The windows.</returns>
		public IReadOnlyList<AppWindow> Windows()
		{
			List<AppWindow> windows = [];
			foreach (IntPtr handle in NativeMethods.GetProcessWindows(this._process.Id))
			{
				try
				{
					AutomationElement element = AutomationElement.FromHandle(handle);
					if (element.Current.ControlType == ControlType.Window)
					{
						windows.Add(new AppWindow(element, handle));
					}
				}
				catch (Exception ex) when (ex is ElementNotAvailableException or COMException)
				{
					// The window closed meanwhile.
				}
			}

			return windows;
		}

		/// <summary>Lists the top-level elements of the program as UI Automation sees them: the windows, and the popups (drop-down lists, tool tips) that are not windows.</summary>
		/// <returns>The elements.</returns>
		public IReadOnlyList<AutomationElement> TopLevelElements()
		{
			return AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, this._process.Id)).Cast<AutomationElement>().ToList();
		}

		/// <summary>Lists every visible top-level window of the program, popups and menus included.</summary>
		/// <returns>The window handles.</returns>
		public IReadOnlyList<IntPtr> AllWindowHandles()
		{
			return NativeMethods.GetProcessWindows(this._process.Id);
		}

		/// <summary>Waits for a window whose title contains a text.</summary>
		/// <param name="title">The text.</param>
		/// <param name="timeout">The time limit.</param>
		/// <returns>The window.</returns>
		public AppWindow WaitForWindow(String title, TimeSpan? timeout = null)
		{
			title = LocalizedStrings.Translate(title, this.Launch.Language);
			return Uia.Wait(
				() => this.Windows().FirstOrDefault(window => window.Title.Contains(title, StringComparison.OrdinalIgnoreCase) && !ReferenceEquals(window.Element, this._main?.Element)),
				"a window titled '" + title + "' (open: " + String.Join(" | ", this.Windows().Select(window => window.Title)) + ")",
				timeout);
		}

		/// <summary>Waits until no window with a title containing a text is open any more.</summary>
		/// <param name="title">The text.</param>
		/// <param name="timeout">The time limit.</param>
		public void WaitForWindowClosed(String title, TimeSpan? timeout = null)
		{
			Uia.WaitUntil(() => !this.Windows().Any(window => window.Title.Contains(title, StringComparison.OrdinalIgnoreCase) && window.Handle != this.MainWindow.Handle), "the window '" + title + "' to close", timeout);
		}

		/// <summary>Invokes a menu item of the main window through UI Automation (no mouse, no keyboard focus).</summary>
		/// <param name="path">The names of the menus and of the item, for example <c>Dialogue</c>, <c>Compare Message Files</c>.</param>
		public void InvokeMenu(params String[] path)
		{
			ArgumentNullException.ThrowIfNull(path);
			AutomationElement? parent = null;
			for (Int32 index = 0; index < path.Length; index++)
			{
				String wanted = Uia.Normalize(LocalizedStrings.Translate(path[index], this.Launch.Language));
				AutomationElement? previous = parent;
				AutomationElement item = this.FindMenuItemOrReopen(previous, wanted, path[index]);
				if (index < path.Length - 1)
				{
					Uia.Expand(item);
					parent = item;
				}
				else
				{
					if (!item.Current.IsEnabled)
					{
						this.CloseMenus();
						throw new InvalidOperationException("The menu item '" + String.Join(" > ", path) + "' is disabled.");
					}

					Uia.Invoke(item);
				}
			}
		}

		/// <summary>
		/// Finds a menu item below an expanded menu. A menu that was expanded through UI Automation while the program was busy is now and then
		/// closed again before its items can be found: the menu is then expanded again, up to three times.
		/// </summary>
		private AutomationElement FindMenuItemOrReopen(AutomationElement? parent, String wanted, String description)
		{
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					return Uia.Wait(() => this.FindMenuItem(parent, wanted), "the menu item '" + description + "'", TimeSpan.FromSeconds(parent is null ? 8 : 2.5));
				}
				catch (TimeoutException) when (parent is not null && attempt < 3)
				{
					this.Step("the menu closed before '" + description + "' was found: opening it again");
					Uia.Collapse(parent);
					System.Threading.Thread.Sleep(200);
					Uia.Expand(parent);
				}
			}
		}

		/// <summary>Gets whether a menu item exists and is enabled.</summary>
		/// <param name="path">The names of the menus and of the item.</param>
		/// <returns><see langword="true"/> when the item can be invoked.</returns>
		public Boolean IsMenuItemEnabled(params String[] path)
		{
			ArgumentNullException.ThrowIfNull(path);
			AutomationElement? parent = null;
			AutomationElement? item = null;
			for (Int32 index = 0; index < path.Length; index++)
			{
				String wanted = Uia.Normalize(LocalizedStrings.Translate(path[index], this.Launch.Language));
				AutomationElement? previous = parent;
				item = Uia.Wait(() => this.FindMenuItem(previous, wanted), "the menu item '" + path[index] + "'", TimeSpan.FromSeconds(8));
				if (index < path.Length - 1)
				{
					Uia.Expand(item);
					parent = item;
				}
			}

			Boolean enabled = item is not null && item.Current.IsEnabled;
			this.CloseMenus();
			return enabled;
		}

		/// <summary>Closes the open menus (collapses the top level menu items).</summary>
		public void CloseMenus()
		{
			try
			{
				foreach (AutomationElement item in this.MainWindow.Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)))
				{
					if (item.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out Object? pattern) && ((ExpandCollapsePattern)pattern).Current.ExpandCollapseState == ExpandCollapseState.Expanded)
					{
						((ExpandCollapsePattern)pattern).Collapse();
					}
				}
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
			{
				// A menu that closes by itself is what is wanted.
			}
		}

		/// <summary>Gets the text of the parser button of the status bar (<c>Parser: Done</c>, <c>Parser: Parsing...</c>).</summary>
		/// <returns>The text, or <see langword="null"/> when the status bar shows none yet.</returns>
		public String? ParserStatus()
		{
			HashSet<String> texts = ParserStatusKeys.Select(key => LocalizedStrings.Get(this.Launch.Language, key)).OfType<String>().ToHashSet(StringComparer.Ordinal);
			AutomationElement? button = this.MainWindow.Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
				.Cast<AutomationElement>()
				.FirstOrDefault(candidate => texts.Contains(candidate.Current.Name));
			return button?.Current.Name;
		}

		/// <summary>Waits until the status bar says that the parser finished.</summary>
		/// <param name="timeout">The time limit.</param>
		public void WaitForParserDone(TimeSpan? timeout = null)
		{
			String done = LocalizedStrings.Get(this.Launch.Language, "Str.Shell.Parser.Done") ?? "Parser: Done";
			Uia.Wait(() => this.ParserStatus() == done ? done : null, "the parser to finish (status: " + this.ParserStatus() + ")", timeout ?? TimeSpan.FromSeconds(40));
		}

		/// <summary>Waits until the parser has finished, with any result (a script with a deliberate mistake ends with <c>Parser: Syntax error in the script!</c>).</summary>
		/// <param name="timeout">The time limit.</param>
		/// <returns>The status text.</returns>
		public String WaitForParserFinished(TimeSpan? timeout = null)
		{
			HashSet<String> pending = new[] { "Str.Shell.Parser.Idle", "Str.Shell.Parser.Waiting", "Str.Shell.Parser.Running", "Str.Shell.Parser.NoFile" }
				.Select(key => LocalizedStrings.Get(this.Launch.Language, key))
				.OfType<String>()
				.ToHashSet(StringComparer.Ordinal);
			return Uia.Wait(
				() => this.ParserStatus() is { } status && !pending.Contains(status) ? status : null,
				"the parser to finish (status: " + this.ParserStatus() + ")",
				timeout ?? TimeSpan.FromSeconds(40));
		}

		/// <summary>Selects a tab of the main window (the right browser, the output panel) and waits until it is selected.</summary>
		/// <param name="name">The name of the tab.</param>
		/// <returns>The tab item.</returns>
		public AutomationElement SelectTab(String name)
		{
			name = LocalizedStrings.Translate(name, this.Launch.Language);
			AutomationElement tab = Uia.WaitFor(this.MainWindow.Element, name, ControlType.TabItem, null, TimeSpan.FromSeconds(8));
			Uia.Select(tab);
			Uia.WaitUntil(() => Uia.IsSelected(tab), "the tab '" + name + "' to be selected", TimeSpan.FromSeconds(8));
			return tab;
		}

		/// <summary>Invokes a menu item and waits for the window it opens.</summary>
		/// <param name="title">A text of the title of the window that opens.</param>
		/// <param name="path">The menu path.</param>
		/// <returns>The window.</returns>
		public AppWindow OpenWindow(String title, params String[] path)
		{
			this.InvokeMenu(path);
			AppWindow window = this.WaitForWindow(title, TimeSpan.FromSeconds(15));
			Uia.WaitUntil(() => window.Snapshot().Count > 3, "the window '" + title + "' to be filled", TimeSpan.FromSeconds(10));
			return window;
		}

		/// <summary>Invokes a menu item and waits for a window that was not open before (whatever its title).</summary>
		/// <param name="path">The menu path.</param>
		/// <returns>The new window, filled.</returns>
		public AppWindow OpenNewWindow(params String[] path)
		{
			HashSet<IntPtr> before = this.Windows().Select(window => window.Handle).ToHashSet();
			this.InvokeMenu(path);
			AppWindow window = Uia.Wait(
				() => this.Windows().FirstOrDefault(candidate => !before.Contains(candidate.Handle)),
				"a new window after " + String.Join(" > ", path) + " (open: " + String.Join(" | ", this.Windows().Select(candidate => candidate.Title)) + ")",
				TimeSpan.FromSeconds(15));
			Uia.WaitUntil(() => window.Snapshot().Count > 3, "the window '" + window.Title + "' to be filled", TimeSpan.FromSeconds(10));
			return window;
		}

		/// <summary>Waits until a window is gone (it closed itself, for example after its default button was invoked).</summary>
		/// <param name="window">The window.</param>
		/// <param name="timeout">The time limit.</param>
		public void WaitForClosed(AppWindow window, TimeSpan? timeout = null)
		{
			ArgumentNullException.ThrowIfNull(window);
			Uia.WaitUntil(() => this.Windows().All(candidate => candidate.Handle != window.Handle), "the window to close", timeout ?? TimeSpan.FromSeconds(10));
		}

		/// <summary>Closes a window through its window pattern and waits until it is gone.</summary>
		/// <param name="window">The window.</param>
		public void CloseWindow(AppWindow window)
		{
			ArgumentNullException.ThrowIfNull(window);
			this.RequestClose(window);
			Uia.WaitUntil(() => this.Windows().All(candidate => candidate.Handle != window.Handle), "the window to close", TimeSpan.FromSeconds(10));
		}

		/// <summary>
		/// Asks a window to close, the way a person does with the close button (the window pattern). The request is now and then lost while a
		/// popup or a menu of the program is open (the program is never activated): after a short wait the same request is sent as
		/// <c>WM_CLOSE</c>, which is what the close button sends.
		/// </summary>
		/// <param name="window">The window.</param>
		public void RequestClose(AppWindow window)
		{
			ArgumentNullException.ThrowIfNull(window);
			IntPtr handle = window.Handle;
			try
			{
				window.Close();
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
			{
				// The window is gone, or cannot take the request through its pattern: the message below does.
			}

			DateTime limit = DateTime.UtcNow.AddSeconds(2.5);
			while (DateTime.UtcNow < limit)
			{
				if (this.Windows().All(candidate => candidate.Handle != handle))
				{
					return;
				}

				Thread.Sleep(100);
			}

			this.Step("the window did not react to the request of its window pattern: sending WM_CLOSE");
			NativeMethods.Post(handle, NativeMethods.Close, IntPtr.Zero, IntPtr.Zero);
		}

		/// <summary>Captures the popups (menus, drop-downs) that are open, to PNG files.</summary>
		/// <param name="name">The name of the pictures.</param>
		public void ShotPopups(String name)
		{
			Int32 index = 0;
			IReadOnlyList<AppWindow> windows = this.Windows();
			foreach (IntPtr handle in this.AllWindowHandles())
			{
				// A popup (drop-down list, menu, tool tip) is a window without a title.
				if (windows.Any(window => window.Handle == handle && window.Title.Length > 0))
				{
					continue;
				}

				WindowImage? image = WindowImage.Capture(handle, TimeSpan.FromSeconds(2));
				if (image is not null)
				{
					image.Save(Path.Combine(this._suite.OutputDirectory, this._test, name + "-popup" + index++ + ".png"));
					this.Step("captured popup " + name + ": " + image.Width + "x" + image.Height + ", " + image.DistinctColors + " colors");
				}
			}
		}

		/// <summary>
		/// Presses a key in the editor of the active document (<c>Tab</c>, <c>Return</c>, <c>Right</c>: the name of a WPF key). The program is in the
		/// background and has no keyboard focus, so the key does not come from the keyboard: the program raises it at the editor (see
		/// <c>FalloutScriptStudio.UiTesting.UiTestInput</c>); modifier keys cannot be sent, use the menu or the command palette for those commands.
		/// </summary>
		/// <param name="key">The name of the key.</param>
		public void PressInEditor(String key)
		{
			NativeMethods.SendInputCommand(this.MainWindow.Handle, "key:" + key);
			System.Threading.Thread.Sleep(150);
		}

		/// <summary>Types text in the editor of the active document, one character after the other as a person types it (see <see cref="PressInEditor"/>).</summary>
		/// <param name="text">The text.</param>
		public void TypeInEditor(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			foreach (Char character in text)
			{
				NativeMethods.SendInputCommand(this.MainWindow.Handle, "text:" + character);
				System.Threading.Thread.Sleep(60);
			}

			System.Threading.Thread.Sleep(300);
		}

		/// <summary>Starts a second program with the same data folder: it hands its command line to the running one and ends.</summary>
		/// <param name="arguments">The files or folders.</param>
		public void ForwardToRunningInstance(params String[] arguments)
		{
			ProcessStartInfo start = new(Path.Combine(this._programDirectory, this.Launch.Executable))
			{
				UseShellExecute = false,
				WorkingDirectory = this._programDirectory,
			};
			foreach (String argument in arguments)
			{
				start.ArgumentList.Add(argument);
			}

			start.Environment["FSS_UI_TEST"] = "1";
			start.Environment["FSS_UI_TEST_DATA"] = this._data.Path;
			start.Environment["FSS_UI_TEST_LANGUAGE"] = this.Launch.Language;
			start.Environment["FSS_UI_TEST_THEME"] = this.Launch.Theme;
			using Process? second = Process.Start(start);
			second?.WaitForExit(30000);
		}

		/// <summary>Captures a window to a PNG file in the output folder and checks that it is not blank.</summary>
		/// <param name="window">The window.</param>
		/// <param name="name">The name of the picture (a file name without extension).</param>
		/// <param name="mayBeBlank">Whether a blank picture is acceptable.</param>
		/// <returns>The picture.</returns>
		public WindowImage Shot(AppWindow window, String name, Boolean mayBeBlank = false)
		{
			ArgumentNullException.ThrowIfNull(window);
			WindowImage? image = WindowImage.Capture(window.Handle, TimeSpan.FromSeconds(5));
			Assert.True(image is not null, "The window '" + window.Title + "' could not be captured.");
			String path = Path.Combine(this._suite.OutputDirectory, this._test, name + ".png");
			image.Save(path);
			this._captures.Add(name);
			this.Step("captured " + name + ": " + image.Width + "x" + image.Height + ", " + image.DistinctColors + " colors, title '" + window.Title + "'" + (image.IsBlank ? ", BLANK" : String.Empty));
			if (!mayBeBlank)
			{
				Assert.False(image.IsBlank, "The picture of '" + window.Title + "' (" + name + ") is blank: " + path);
			}

			this.AssertInvisible();
			return image;
		}

		/// <summary>Writes the elements that stick out of a window to the report and fails when there are any.</summary>
		/// <param name="window">The window.</param>
		public void AssertNothingClipped(AppWindow window)
		{
			ArgumentNullException.ThrowIfNull(window);
			IReadOnlyList<String> clipped = window.FindClippedElements();
			foreach (String text in clipped)
			{
				this.Step("CLIPPED in '" + window.Title + "': " + text);
			}

			Assert.True(clipped.Count == 0, "Elements stick out of the window '" + window.Title + "':" + Environment.NewLine + String.Join(Environment.NewLine, clipped.Take(10)));
		}

		/// <summary>Checks that no window of the program is on a monitor and that the program does not own the foreground: the developer is never disturbed.</summary>
		public void AssertInvisible()
		{
			foreach (IntPtr handle in NativeMethods.GetProcessWindows(this._process.Id))
			{
				(Int32 left, Int32 top, _, _) = NativeMethods.GetBounds(handle);
				Assert.True(left < -10000 && top < -10000, "A window of the program is on screen at " + left + "," + top + ": '" + NativeMethods.GetTitle(handle) + "' (" + NativeMethods.GetClass(handle) + ")");
			}

			Assert.False(NativeMethods.OwnsForeground(this._process.Id), "The program took the foreground.");
		}

		/// <summary>Writes the UI Automation tree of a window to a text file beside the pictures.</summary>
		/// <param name="window">The window.</param>
		/// <param name="name">The name of the file (without extension).</param>
		public void Dump(AppWindow window, String name)
		{
			ArgumentNullException.ThrowIfNull(window);
			Directory.CreateDirectory(Path.Combine(this._suite.OutputDirectory, this._test));
			File.WriteAllText(Path.Combine(this._suite.OutputDirectory, this._test, name + ".txt"), Uia.Dump(window.Element, 4000));
		}

		/// <summary>Writes a line to the report.</summary>
		/// <param name="text">The line.</param>
		public void Step(String text)
		{
			this._suite.Report(this._test, text);
		}

		/// <summary>Gets the lines of the log of the program.</summary>
		/// <returns>The lines (empty when there is no log yet).</returns>
		public IReadOnlyList<String> ReadLog()
		{
			if (!File.Exists(this.LogPath))
			{
				return [];
			}

			using FileStream stream = new(this.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader reader = new(stream);
			return reader.ReadToEnd().Split('\n').Select(line => line.TrimEnd('\r')).ToList();
		}

		/// <summary>Fails when the log of the program holds an error, an unhandled exception or a failed start.</summary>
		public void AssertCleanLog()
		{
			List<String> bad = this.ReadLog().Where(line => LogFailures.Any(failure => line.Contains(failure, StringComparison.Ordinal))).ToList();
			foreach (String line in bad)
			{
				this.Step("LOG: " + line);
			}

			Assert.True(bad.Count == 0, "The log of the program holds errors:" + Environment.NewLine + String.Join(Environment.NewLine, bad.Take(10)) + Environment.NewLine + "(" + this.LogPath + ")");
		}

		/// <summary>Closes the program the way a user does (all other windows, then the main window) and checks that it exits by itself.</summary>
		/// <returns>The exit code.</returns>
		public Int32 Close()
		{
			if (this._closed)
			{
				return this._process.HasExited ? this._process.ExitCode : -1;
			}

			this._closed = true;
			foreach (AppWindow window in this.Windows().Where(candidate => this._main is null || candidate.Handle != this._main.Handle).Reverse().ToList())
			{
				this.RequestClose(window);
			}

			if (this._main is not null)
			{
				try
				{
					this._main.Close();
				}
				catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
				{
					this.Step("could not close the main window: " + ex.Message);
				}
			}

			Boolean exited = this._process.WaitForExit(3000);
			if (!exited && this.DiscardUnsavedChanges())
			{
				exited = this._process.WaitForExit(8000);
			}

			if (!exited)
			{
				exited = this._process.WaitForExit(3000);
			}

			for (Int32 retry = 1; retry <= 2 && !exited; retry++)
			{
				this.Step("RETRY CLOSE " + retry + ": the program did not react to the first request to close its main window (retrying with WM_CLOSE)");
				try
				{
					NativeMethods.Post(this.MainWindow.Handle, NativeMethods.Close, IntPtr.Zero, IntPtr.Zero);
				}
				catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
				{
					this.Step("could not close the main window: " + ex.Message);
				}

				exited = this._process.WaitForExit(8000);
			}

			this.Step("closed: exited=" + exited + (exited ? " code=" + this._process.ExitCode.ToString(CultureInfo.InvariantCulture) : String.Empty));
			if (!exited)
			{
				this.ReportStuckClose();
			}

			Assert.True(exited, "The program did not exit after its main window was closed.");
			Thread.Sleep(200);
			AssertNoParserHostLeft(this._programDirectory);
			return this._process.ExitCode;
		}

		/// <summary>Ends the program (killing it when it is still running) and deletes its data folder.</summary>
		public void Dispose()
		{
			try
			{
				if (!this._process.HasExited)
				{
					this._process.Kill(entireProcessTree: true);
					this._process.WaitForExit(10000);
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
			{
				// The process is gone already.
			}

			this._process.Dispose();
			if (UiSmokeEnvironment.KeepData)
			{
				return;
			}

			this._data.Dispose();
		}

		/// <summary>Answers the question that a script with typed text asks when the program closes: the changes are not saved (the files of the tests are thrown away).</summary>
		/// <returns><see langword="true"/> when the question was there and was answered.</returns>
		private Boolean DiscardUnsavedChanges()
		{
			String language = this.Launch.Language;
			String title = LocalizedStrings.Get(language, "Str.Shell.Dlg.UnsavedTitle") ?? "Unsaved changes";
			String cancel = LocalizedStrings.Get(language, "Str.Common.Cancel") ?? "Cancel";

			// The name of a message box is its text, not its title: the question is the other window that has a Cancel button.
			AppWindow? question = this.Windows().FirstOrDefault(window => this._main is not null && window.Handle != this._main.Handle && Uia.Find(window.Element, cancel, ControlType.Button) is not null);
			if (question is null)
			{
				return false;
			}

			String[] keep = ["Str.Common.Save", "Str.Common.Yes", "Str.Common.Cancel"];
			HashSet<String> kept = keep.Select(key => LocalizedStrings.Get(language, key)).OfType<String>().ToHashSet(StringComparer.OrdinalIgnoreCase);
			AutomationElement? discard = question.Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
				.Cast<AutomationElement>()
				.FirstOrDefault(button => button.Current.Name.Length > 0 && !kept.Contains(button.Current.Name) && button.Current.AutomationId != "TitleBarCloseButton");
			this.Step("closing: the program asks '" + title + "'; answering with '" + discard?.Current.Name + "'");
			if (discard is null)
			{
				return false;
			}

			Uia.Invoke(discard);
			return true;
		}

		private void ReportStuckClose()
		{
			this.Step("STUCK: main window enabled=" + this.MainWindow.Element.Current.IsEnabled);
			foreach (AutomationElement top in AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, this._process.Id)))
			{
				this.Step("STUCK: top level element " + top.Current.ControlType.ProgrammaticName + " '" + top.Current.Name + "' enabled=" + top.Current.IsEnabled + " offscreen=" + top.Current.IsOffscreen + " class=" + top.Current.ClassName);
			}

			foreach (IntPtr handle in this.AllWindowHandles())
			{
				this.Step("STUCK: window '" + NativeMethods.GetTitle(handle) + "' " + NativeMethods.GetClass(handle) + " " + NativeMethods.GetBounds(handle));
			}

			if (File.Exists(Path.Combine(this._data.Path, "dbg.log")))
			{
				this.Step("STUCK dbg: " + File.ReadAllText(Path.Combine(this._data.Path, "dbg.log")).Replace("\n", " | "));
			}

			foreach (String line in this.ReadLog().TakeLast(15))
			{
				this.Step("STUCK log: " + line);
			}

			try
			{
				Directory.CreateDirectory(Path.Combine(this._suite.OutputDirectory, this._test));
				File.WriteAllText(Path.Combine(this._suite.OutputDirectory, this._test, "stuck-tree.txt"), Uia.Dump(this.MainWindow.Element, 400));
			}
			catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
			{
				this.Step("STUCK: the main window cannot be read: " + ex.Message);
			}
		}

		private static void AssertNoParserHostLeft(String programDirectory)
		{
			foreach (Process host in Process.GetProcessesByName("FalloutScriptStudio.ParserHost"))
			{
				using (host)
				{
					try
					{
						if (host.MainModule?.FileName is { } path && path.StartsWith(programDirectory, StringComparison.OrdinalIgnoreCase) && !host.WaitForExit(5000))
						{
							host.Kill();
							Assert.Fail("A parser host process was left running after the program exited.");
						}
					}
					catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
					{
						// Not ours, or already gone.
					}
				}
			}
		}

		private static void Seed(TestFolder data, AppLaunch launch)
		{
			if (launch.SettingsJson is not null)
			{
				data.Write("settings\\settings.json", launch.SettingsJson);
			}

			foreach ((String relative, String content) in launch.DataFiles)
			{
				data.Write(relative, content);
			}
		}

		private AutomationElement? FindMenuItem(AutomationElement? parent, String wanted)
		{
			// Menus are searched below the parent item first, then in the whole window and in the popups of the program.
			List<AutomationElement> scopes = [];
			if (parent is not null)
			{
				scopes.Add(parent);
			}

			scopes.Add(this.MainWindow.Element);
			foreach (AppWindow window in this.Windows())
			{
				scopes.Add(window.Element);
			}

			foreach (AutomationElement scope in scopes)
			{
				foreach (AutomationElement item in Uia.FindByNameContaining(scope, wanted, ControlType.MenuItem))
				{
					if (Uia.Normalize(item.Current.Name) == wanted)
					{
						return item;
					}
				}
			}

			return null;
		}

		private void WaitForMainWindow()
		{
			AppWindow main = Uia.Wait(
				() => this.Windows().FirstOrDefault(window => window.Title.StartsWith("Fallout Script Studio", StringComparison.Ordinal)),
				"the main window of the program",
				TimeSpan.FromSeconds(40));
			this._main = main;
			this.AssertInvisible();
		}
	}
}