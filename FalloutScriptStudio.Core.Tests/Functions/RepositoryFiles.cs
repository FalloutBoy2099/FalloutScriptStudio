namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;
	using FalloutScriptStudio.TestSupport;

	/// <summary>Locates the content files of the repository (the real <c>descriptions</c> folder) from the test output folder.</summary>
	internal static class RepositoryFiles
	{
		/// <summary>Gets the repository root: the first parent folder that holds the solution file.</summary>
		public static String Root => TestFiles.RepositoryRoot;

		/// <summary>Gets the path of a file of <c>FalloutScriptStudio\Content\descriptions</c>.</summary>
		/// <param name="fileName">The file name, for example <c>functions.ini</c>.</param>
		/// <returns>The full path.</returns>
		public static String Description(String fileName)
		{
			return Path.Combine(Root, "FalloutScriptStudio", "Content", "descriptions", fileName);
		}
	}
}
