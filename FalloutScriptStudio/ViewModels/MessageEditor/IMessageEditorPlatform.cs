namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;

	/// <summary>
	/// The few operating system facilities the Message Editor needs besides file reading and writing.
	/// Keeping them behind an interface keeps the view model testable.
	/// </summary>
	public interface IMessageEditorPlatform
	{
		/// <summary>
		/// Gets the current version stamp of a file.
		/// </summary>
		/// <param name="path">The file path.</param>
		/// <returns>The stamp, or <see langword="null"/> if the file does not exist.</returns>
		FileStamp? GetFileStamp(String path);

		/// <summary>
		/// Checks whether a file exists.
		/// </summary>
		/// <param name="path">The file path.</param>
		/// <returns><see langword="true"/> if the file exists.</returns>
		Boolean FileExists(String path);

		/// <summary>
		/// Starts a program (the external text editor).
		/// </summary>
		/// <param name="programPath">The program to start.</param>
		/// <param name="filePath">The file passed to the program.</param>
		void StartEditor(String programPath, String filePath);

		/// <summary>
		/// Plays a short system sound.
		/// </summary>
		/// <param name="beep">The kind of sound.</param>
		void Beep(MessageEditorBeep beep);
	}
}