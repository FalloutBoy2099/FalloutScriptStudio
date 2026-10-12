namespace FalloutScriptStudio.Startup
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Views.Editor;
	using FalloutScriptStudio.Views.Shell;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>The WPF side of the start-up: message boxes, the main window, the message editor and the end of the application.</summary>
	public sealed class ApplicationStartupHost : IStartupHost
	{
		private readonly Application _application;
		private readonly String _applicationName;

		/// <summary>Initializes a new instance of the <see cref="ApplicationStartupHost"/> class.</summary>
		/// <param name="application">The application.</param>
		/// <param name="applicationName">The title of the message boxes.</param>
		public ApplicationStartupHost(Application application, String applicationName)
		{
			ArgumentNullException.ThrowIfNull(application);
			ArgumentNullException.ThrowIfNull(applicationName);
			this._application = application;
			this._applicationName = applicationName;
		}

		/// <inheritdoc />
		public void InitializeLanguage(String? languageCode)
		{
			Loc.Initialize(languageCode ?? Loc.DetectSystemLanguage());
		}

		/// <inheritdoc />
		public void ShowInformation(String message)
		{
			MessageBox.Show(message, this._applicationName, MessageBoxButton.OK, MessageBoxImage.Information);
		}

		/// <inheritdoc />
		public void ShowError(String message)
		{
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
			window.Show();

			CommandLineHandler handler = services.Provider.GetRequiredService<CommandLineHandler>();
			singleInstance.ArgumentsReceived += (_, forwarded) => this._application.Dispatcher.BeginInvoke(new Action(async () =>
			{
				if (await handler.HandleAsync(forwarded))
				{
					BringToFront(window);
				}
			}));
			singleInstance.StartListening();
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
