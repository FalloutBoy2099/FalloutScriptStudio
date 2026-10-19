namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using System.Windows;

	/// <summary>
	/// Describes how a Message Editor window should be opened.
	/// </summary>
	public sealed record MessageEditorOpenOptions
	{
		/// <summary>Gets the message file to open, or <see langword="null"/> for an empty unsaved document.</summary>
		public String? FilePath { get; init; }

		/// <summary>Gets the owner of the lines the editor sends, or <see langword="null"/> for a stand-alone editor.</summary>
		public IMessageLineTarget? Target { get; init; }

		/// <summary>Gets the number of the message to select after loading, or <see langword="null"/> to keep the first row.</summary>
		public Int32? SelectMessageId { get; init; }

		/// <summary>Gets a value indicating whether the window closes right after a line was sent (used when a message is picked for a flowchart node).</summary>
		public Boolean CloseOnSend { get; init; }

		/// <summary>Gets a value indicating whether the window is shown modally; <see cref="IMessageEditorService.Open(MessageEditorOpenOptions)"/> then returns when it is closed.</summary>
		public Boolean Modal { get; init; }

		/// <summary>
		/// Gets the window the editor belongs to. The editor stays above it while "Window On Top" is checked.
		/// When <see langword="null"/> and a <see cref="Target"/> is given, the application main window is used.
		/// </summary>
		public Window? Owner { get; init; }

		/// <summary>Gets a value indicating whether the window starts maximized (the legacy editor did this for a message file opened from the command line).</summary>
		public Boolean StartMaximized { get; init; }
	}
}