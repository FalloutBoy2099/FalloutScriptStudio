namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.IO;

	/// <summary>A uniquely named temporary directory that is deleted when disposed.</summary>
	internal sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fss-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this.Path);
		}

		public String Path { get; }

		public String Write(String relativePath, String content)
		{
			String full = System.IO.Path.Combine(this.Path, relativePath);
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
				// Best effort cleanup of a temporary directory.
			}
			catch (UnauthorizedAccessException)
			{
				// Best effort cleanup of a temporary directory.
			}
		}
	}
}
