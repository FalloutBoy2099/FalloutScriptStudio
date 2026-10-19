namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Services;

	/// <summary>A shell service that records what it was asked to do.</summary>
	internal sealed class FakeShellService : IShellService
	{
		public List<String> Revealed { get; } = [];

		public List<String> Folders { get; } = [];

		public List<String> Programs { get; } = [];

		/// <summary>Gets the files, folders and addresses opened with their associated program.</summary>
		public List<String> Opened { get; } = [];

		/// <summary>Gets or sets the answer of <see cref="OpenWithShell"/>.</summary>
		public Boolean OpenWithShellResult { get; set; } = true;

		/// <summary>Gets or sets the answer of <see cref="StartProgram"/>.</summary>
		public Boolean StartProgramResult { get; set; } = true;

		public String Clipboard { get; set; } = String.Empty;

		public void RevealInExplorer(String path)
		{
			this.Revealed.Add(path);
		}

		public void OpenFolder(String directory)
		{
			this.Folders.Add(directory);
		}

		public Boolean StartProgram(String programPath, String arguments)
		{
			this.Programs.Add(programPath + " " + arguments);
			return this.StartProgramResult;
		}

		public Boolean OpenWithShell(String path)
		{
			this.Opened.Add(path);
			return this.OpenWithShellResult;
		}

		public void SetClipboardText(String text)
		{
			this.Clipboard = text;
		}

		public String GetClipboardText()
		{
			return this.Clipboard;
		}
	}
}