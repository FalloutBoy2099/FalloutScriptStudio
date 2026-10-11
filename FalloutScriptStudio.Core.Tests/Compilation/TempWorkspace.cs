namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// A throw-away installation folder layout (<c>resources</c>, <c>scrTemp</c>, <c>scripts</c>) in the temp folder.
	/// </summary>
	internal sealed class TempWorkspace : IDisposable
	{
		public TempWorkspace()
		{
			this.Root = Path.Combine(Path.GetTempPath(), "fss-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(this.Resources);
			Directory.CreateDirectory(this.Scratch);
			Directory.CreateDirectory(this.Scripts);
		}

		public String Root { get; }

		public String Resources => Path.Combine(this.Root, "resources");

		public String Scratch => Path.Combine(this.Root, "scrTemp");

		public String Scripts => Path.Combine(this.Root, "scripts");

		/// <summary>Creates a file below the root (and its folders) and returns its full path.</summary>
		public String CreateFile(String relativePath, String content = "")
		{
			String path = Path.Combine(this.Root, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, content);
			return path;
		}

		/// <summary>Creates empty stand-ins for external tools in the resources folder.</summary>
		public void CreateTools(params String[] names)
		{
			foreach (String name in names)
			{
				File.WriteAllText(Path.Combine(this.Resources, name), String.Empty);
			}
		}

		public CompilerOptions CreateOptions()
		{
			return new CompilerOptions
			{
				ProgramDirectory = this.Root,
				ResourcesDirectory = this.Resources,
				ScratchDirectory = this.Scratch,
			};
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(this.Root, true);
			}
			catch (IOException)
			{
				// Leftover temp files are harmless.
			}
		}
	}
}
