namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.IO;

	/// <summary>
	/// A uniquely named scratch folder <c>_test_files\work\&lt;name&gt;-&lt;guid&gt;</c> that is deleted when disposed. This file is
	/// linked into both test projects; the helpers of the projects (<c>TempFolder</c>, <c>TempDirectory</c>, ...) build on it.
	/// </summary>
	internal class TestFolder : IDisposable
	{
		/// <summary>Initializes a new instance of the <see cref="TestFolder"/> class and creates the folder.</summary>
		/// <param name="name">A short readable name for the folder (what the test is about).</param>
		public TestFolder(String name)
		{
			this.Path = TestFiles.CreateWorkFolder(name);
		}

		/// <summary>Gets the full path of the folder.</summary>
		public String Path { get; }

		/// <summary>Combines the folder with relative path parts.</summary>
		/// <param name="parts">The parts.</param>
		/// <returns>The combined path.</returns>
		public virtual String Combine(params String[] parts)
		{
			String[] all = new String[parts.Length + 1];
			all[0] = this.Path;
			Array.Copy(parts, 0, all, 1, parts.Length);
			return System.IO.Path.Combine(all);
		}

		/// <summary>Writes a text file below the folder (creating its folders) and returns its full path.</summary>
		/// <param name="relativePath">The path relative to the folder.</param>
		/// <param name="content">The text.</param>
		/// <returns>The full path.</returns>
		public String Write(String relativePath, String content)
		{
			String full = this.Combine(relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
			File.WriteAllText(full, content);
			return full;
		}

		/// <summary>Deletes the folder.</summary>
		public void Dispose()
		{
			TestFiles.DeleteWorkFolder(this.Path);
			GC.SuppressFinalize(this);
		}
	}
}