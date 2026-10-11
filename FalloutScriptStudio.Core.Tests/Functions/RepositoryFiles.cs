namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;

	/// <summary>Locates the content files of the repository (the real <c>descriptions</c> folder) from the test output folder.</summary>
	internal static class RepositoryFiles
	{
		private const String MarkerFile = "FalloutScriptStudio.sln";

		/// <summary>Gets the repository root: the first parent folder that holds the solution file.</summary>
		public static String Root
		{
			get
			{
				DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
				while (directory is not null)
				{
					if (File.Exists(Path.Combine(directory.FullName, MarkerFile)))
					{
						return directory.FullName;
					}

					directory = directory.Parent;
				}

				throw new InvalidOperationException("The repository root (" + MarkerFile + ") was not found above " + AppContext.BaseDirectory + ".");
			}
		}

		/// <summary>Gets the path of a file of <c>FalloutScriptStudio\Content\descriptions</c>.</summary>
		/// <param name="fileName">The file name, for example <c>functions.ini</c>.</param>
		/// <returns>The full path.</returns>
		public static String Description(String fileName)
		{
			return Path.Combine(Root, "FalloutScriptStudio", "Content", "descriptions", fileName);
		}
	}
}
