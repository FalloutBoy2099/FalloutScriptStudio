namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.IO;
	using System.Text;

	/// <summary>A uniquely named temporary directory that is deleted when disposed.</summary>
	internal sealed class ScriptToolsTempDirectory : IDisposable
	{
		public ScriptToolsTempDirectory()
		{
			this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fss-scripttools-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this.Path);
		}

		public String Path { get; }

		public String Combine(params String[] parts)
		{
			return System.IO.Path.GetFullPath(System.IO.Path.Combine(this.Path, System.IO.Path.Combine(parts)));
		}

		public String Write(String relativePath, String content, Encoding? encoding = null)
		{
			String full = this.Combine(relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
			File.WriteAllBytes(full, (encoding ?? new UTF8Encoding(false)).GetBytes(content));
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
