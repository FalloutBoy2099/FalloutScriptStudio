namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.ViewModels.MessageEditor;

	/// <summary>
	/// An <see cref="IMessageEditorPlatform"/> that works on the real files but records sounds and started programs.
	/// </summary>
	internal sealed class FakePlatform : IMessageEditorPlatform
	{
		public List<MessageEditorBeep> Beeps { get; } = new List<MessageEditorBeep>();

		public List<(String Program, String File)> StartedEditors { get; } = new List<(String, String)>();

		public FileStamp? GetFileStamp(String path)
		{
			FileInfo info = new FileInfo(path);
			return info.Exists ? new FileStamp(info.LastWriteTimeUtc, info.Length) : null;
		}

		public Boolean FileExists(String path)
		{
			return File.Exists(path);
		}

		public void StartEditor(String programPath, String filePath)
		{
			this.StartedEditors.Add((programPath, filePath));
		}

		public void Beep(MessageEditorBeep beep)
		{
			this.Beeps.Add(beep);
		}
	}
}
