namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Media;
	using FalloutScriptStudio.ViewModels.MessageEditor;

	/// <summary>
	/// <see cref="IMessageEditorPlatform"/> implementation for Windows.
	/// </summary>
	public sealed class MessageEditorPlatform : IMessageEditorPlatform
	{
		/// <inheritdoc/>
		public FileStamp? GetFileStamp(String path)
		{
			try
			{
				FileInfo info = new FileInfo(path);
				return info.Exists ? new FileStamp(info.LastWriteTimeUtc, info.Length) : null;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
			{
				return null;
			}
		}

		/// <inheritdoc/>
		public Boolean FileExists(String path)
		{
			return File.Exists(path);
		}

		/// <inheritdoc/>
		public void StartEditor(String programPath, String filePath)
		{
			ProcessStartInfo start = new ProcessStartInfo(programPath) { UseShellExecute = false };
			start.ArgumentList.Add(filePath);
			using Process? process = Process.Start(start);
		}

		/// <inheritdoc/>
		public void Beep(MessageEditorBeep beep)
		{
			(beep == MessageEditorBeep.NotFound ? SystemSounds.Exclamation : SystemSounds.Question).Play();
		}
	}
}
