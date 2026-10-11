namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.TestSupport;

	/// <summary>
	/// A throw-away installation folder layout (<c>resources</c>, <c>scrTemp</c>, <c>scripts</c>) in a scratch folder of <c>_test_files\work</c>.
	/// </summary>
	internal sealed class TempWorkspace : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("workspace");

		public TempWorkspace()
		{
			Directory.CreateDirectory(this.Resources);
			Directory.CreateDirectory(this.Scratch);
			Directory.CreateDirectory(this.Scripts);
		}

		public String Root => this._folder.Path;

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
			this._folder.Dispose();
		}
	}
}
