namespace FalloutScriptStudio.Services
{
	using System;

	/// <summary>
	/// Talks to the Windows shell: Explorer, external programs and the clipboard. View models use this instead of
	/// touching <c>Process</c> or <c>Clipboard</c> directly.
	/// </summary>
	public interface IShellService
	{
		/// <summary>Opens Explorer with a file selected.</summary>
		void RevealInExplorer(String path);

		/// <summary>Opens a folder in Explorer.</summary>
		void OpenFolder(String directory);

		/// <summary>Starts a program with arguments.</summary>
		/// <returns><see langword="false"/> when the program could not be started.</returns>
		Boolean StartProgram(String programPath, String arguments);

		/// <summary>Opens a file or folder with its associated program.</summary>
		/// <returns><see langword="false"/> when nothing could be opened.</returns>
		Boolean OpenWithShell(String path);

		/// <summary>Puts text on the clipboard.</summary>
		void SetClipboardText(String text);

		/// <summary>Reads text from the clipboard; empty when there is none.</summary>
		String GetClipboardText();
	}
}
