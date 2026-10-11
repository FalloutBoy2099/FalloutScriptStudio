namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;

	/// <summary>A uniquely named temporary folder that is deleted when disposed.</summary>
	internal sealed class TempFolder : IDisposable
	{
		public TempFolder()
		{
			this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fss-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this.Path);
		}

		public String Path { get; }

		public String Combine(String relativePath)
		{
			return System.IO.Path.Combine(this.Path, relativePath);
		}

		public String Write(String relativePath, String content)
		{
			String full = this.Combine(relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
			File.WriteAllText(full, content);
			return full;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(this.Path, true);
			}
			catch (IOException)
			{
				// Best effort cleanup of a temporary folder.
			}
			catch (UnauthorizedAccessException)
			{
				// Best effort cleanup of a temporary folder.
			}
		}
	}
}
