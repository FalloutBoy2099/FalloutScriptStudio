namespace FalloutScriptStudio.Startup
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using Microsoft.Extensions.DependencyInjection;
	using Editor;
	using Localization;
	using Services.MessageEditor;
	using UiTesting;
	using ViewModels.Main;
	using Views.Editor;
	using Views.Shell;
	using Workspace;

	/// <summary>The WPF side of the start-up: message boxes, the main window, the message editor and the end of the application.</summary>
	public sealed class ApplicationStartupHost : IStartupHost
	{
		private readonly Application _application;
		private readonly String _applicationName;
		private readonly UiTestMode _uiTest;
		private readonly Action<String> _log;

		/// <summary>Initializes a new instance of the <see cref="ApplicationStartupHost"/> class.</summary>
		/// <param name="application">The application.</param>
		/// <param name="applicationName">The title of the message boxes.</param>
		/// <param name="uiTest">The UI test mode: the main window is not activated, and the message boxes (native windows that nothing could keep out of sight) are written to the log; <see langword="null"/> for a normal run.</param>
		/// <param name="log">Receives the messages that are not shown under test.</param>
		public ApplicationStartupHost(Application application, String applicationName, UiTestMode? uiTest = null, Action<String>? log = null)
		{
			ArgumentNullException.ThrowIfNull(application);
			ArgumentNullException.ThrowIfNull(applicationName);
			this._application = application;
			this._applicationName = applicationName;
			this._uiTest = uiTest ?? UiTestMode.Off;
			this._log = log ?? (_ => { });
		}

		/// <inheritdoc />
		public void InitializeLanguage(String? languageCode)
		{
			Loc.Initialize(languageCode ?? Loc.DetectSystemLanguage());
		}

		/// <inheritdoc />
		public void ShowInformation(String message)
		{
			if (!this._uiTest.ActivatesWindows)
			{
				this._log("Message: " + message);
				return;
			}

			MessageBox.Show(message, this._applicationName, MessageBoxButton.OK, MessageBoxImage.Information);
		}

		/// <inheritdoc />
		public void ShowError(String message)
		{
			if (!this._uiTest.ActivatesWindows)
			{
				this._log("Error message: " + message);
				return;
			}

			MessageBox.Show(message, this._applicationName, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		/// <inheritdoc />
		public void Shutdown(Int32 exitCode)
		{
			this._application.Shutdown(exitCode);
		}

		/// <inheritdoc />
		public async Task RunMainEditorAsync(IStartupServices services, ISingleInstance singleInstance, String[] arguments)
		{
			MainWindow window = services.Provider.GetRequiredService<MainWindow>();
			this._application.MainWindow = window;

			// INTEGRATION SEAM: gives the editor views (created by the DataTemplate) their shared services.
			ScriptEditorView.SetServices(window, services.Provider.GetRequiredService<ScriptEditorServices>());
			window.Closed += (_, _) => this._application.Shutdown();
			window.ShowActivated = this._uiTest.ActivatesWindows;
			window.Show();

			CommandLineHandler handler = services.Provider.GetRequiredService<CommandLineHandler>();
			singleInstance.ArgumentsReceived += (_, forwarded) => this._application.Dispatcher.BeginInvoke(new Action(async () =>
			{
				if (await handler.HandleAsync(forwarded) && this._uiTest.ActivatesWindows)
				{
					BringToFront(window);
				}
			}));
			singleInstance.StartListening();
			if (arguments.Length == 0)
			{
				// Without files on the command line the project of the last session gets its documents back.
				await services.Provider.GetRequiredService<MainViewModel>().RestoreStartupProjectAsync();
			}

			await handler.HandleAsync(arguments);
		}

		/// <inheritdoc />
		public void RunStandaloneMessageEditor(IStartupServices services, String path)
		{
			IMessageEditorService editor = services.Provider.GetRequiredService<IMessageEditorService>();
			editor.AllWindowsClosed += (_, _) => this._application.Shutdown();
			if (!editor.Open(new MessageEditorOpenOptions { FilePath = Path.GetFullPath(path), StartMaximized = true }))
			{
				this._application.Shutdown();
			}
		}

		private static void BringToFront(Window window)
		{
			if (window.WindowState == WindowState.Minimized)
			{
				window.WindowState = WindowState.Normal;
			}

			window.Activate();
		}
	}
}