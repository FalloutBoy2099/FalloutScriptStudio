namespace FalloutScriptStudio.Services
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Windows;

	/// <inheritdoc />
	public sealed class ShellService : IShellService
	{
		public void RevealInExplorer(String path)
		{
			this.StartProgram("explorer.exe", "/select,\"" + path + "\"");
		}

		public void OpenFolder(String directory)
		{
			this.StartProgram("explorer.exe", "\"" + directory + "\"");
		}

		public Boolean StartProgram(String programPath, String arguments)
		{
			try
			{
				using Process? process = Process.Start(new ProcessStartInfo(programPath, arguments) { UseShellExecute = true });
				return process is not null;
			}
			catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
			{
				return false;
			}
		}

		public Boolean OpenWithShell(String path)
		{
			try
			{
				using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
				return process is not null;
			}
			catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
			{
				return false;
			}
		}

		public void SetClipboardText(String text)
		{
			try
			{
				Clipboard.SetText(text);
			}
			catch (COMException)
			{
				// The clipboard is locked by another process; nothing sensible to do.
			}
		}

		public String GetClipboardText()
		{
			try
			{
				return Clipboard.ContainsText() ? Clipboard.GetText() : String.Empty;
			}
			catch (COMException)
			{
				return String.Empty;
			}
		}
	}
}