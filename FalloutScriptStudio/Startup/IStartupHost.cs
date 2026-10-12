namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading.Tasks;

	/// <summary>What the start-up sequence needs from the user interface: message boxes, windows and the end of the application.</summary>
	public interface IStartupHost
	{
		/// <summary>Sets the language of the user interface.</summary>
		/// <param name="languageCode">The language code, or <see langword="null"/> for the language of Windows.</param>
		void InitializeLanguage(String? languageCode);

		/// <summary>Shows an information message.</summary>
		/// <param name="message">The text.</param>
		void ShowInformation(String message);

		/// <summary>Shows an error message.</summary>
		/// <param name="message">The text.</param>
		void ShowError(String message);

		/// <summary>Ends the application.</summary>
		/// <param name="exitCode">The exit code of the process.</param>
		void Shutdown(Int32 exitCode);

		/// <summary>Opens the main window, receives the command line of later instances and handles the command line of this one.</summary>
		/// <param name="services">The services.</param>
		/// <param name="singleInstance">The lock of the first instance.</param>
		/// <param name="arguments">The command-line arguments.</param>
		/// <returns>A task that completes when the command line is handled.</returns>
		Task RunMainEditorAsync(IStartupServices services, ISingleInstance singleInstance, String[] arguments);

		/// <summary>Opens the message editor alone and ends the application when its last window closes.</summary>
		/// <param name="services">The services.</param>
		/// <param name="path">The message file.</param>
		void RunStandaloneMessageEditor(IStartupServices services, String path);
	}
}
