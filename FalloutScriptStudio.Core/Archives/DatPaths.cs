namespace FalloutScriptStudio.Core.Archives
{
	using System;

	/// <summary>
	/// Converts paths to the form used inside archives: backslashes, no leading separator.
	/// </summary>
	internal static class DatPaths
	{
		private const Char Backslash = '\\';

		/// <summary>Converts a file path.</summary>
		/// <param name="path">A path with slashes or backslashes.</param>
		/// <returns>The path with backslashes and without a leading one.</returns>
		public static String NormalizePath(String path)
		{
			return path.Replace('/', Backslash).TrimStart(Backslash);
		}

		/// <summary>Converts a folder path: backslashes, no leading separator and exactly one trailing one (nothing for the root).</summary>
		/// <param name="directory">A folder path.</param>
		/// <returns>The normalized folder path.</returns>
		public static String NormalizeDirectory(String directory)
		{
			String normalized = directory.Replace('/', Backslash).Trim(Backslash);
			return normalized.Length == 0 ? String.Empty : normalized + Backslash;
		}

		/// <summary>Splits a normalized path into its folder and its file name.</summary>
		/// <param name="path">The normalized path.</param>
		/// <param name="directory">The folder, ending with a backslash, or an empty string.</param>
		/// <param name="name">The file name.</param>
		public static void Split(String path, out String directory, out String name)
		{
			Int32 separator = path.LastIndexOf(Backslash) + 1;
			directory = path.Substring(0, separator);
			name = path.Substring(separator);
		}
	}
}