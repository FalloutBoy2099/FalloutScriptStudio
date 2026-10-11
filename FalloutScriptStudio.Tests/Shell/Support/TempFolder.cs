namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.IO;

	/// <summary>A unique temporary folder that is deleted when disposed.</summary>
	internal sealed class TempFolder : IDisposable
	{
		public TempFolder()
		{
			this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fss-shell-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this.Path);
		}

		public String Path { get; }

		public String Combine(params String[] parts)
		{
			String[] all = new String[parts.Length + 1];
			all[0] = this.Path;
			Array.Copy(parts, 0, all, 1, parts.Length);
			return System.IO.Path.Combine(all);
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
				// A leftover temp folder is harmless.
			}
			catch (UnauthorizedAccessException)
			{
				// A leftover temp folder is harmless.
			}
		}
	}
}
