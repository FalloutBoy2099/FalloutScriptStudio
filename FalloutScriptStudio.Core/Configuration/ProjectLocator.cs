namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;

	/// <summary>
	/// Finds the project a file belongs to: the nearest folder, starting at the folder of the file and going up, that holds a
	/// <c>fss-project.json</c>.
	/// </summary>
	public static class ProjectLocator
	{
		/// <summary>The number of folders examined: the folder of the file and the four above it.</summary>
		public const Int32 MaxLevels = 5;

		/// <summary>
		/// Finds the project folder of a file.
		/// </summary>
		/// <param name="path">The full path of a file (or of a folder, which is then the first one examined).</param>
		/// <param name="maxLevels">How many folders are examined at most, going up from the file.</param>
		/// <returns>The project folder, or <see langword="null"/> when no folder within reach has a project file.</returns>
		public static String? FindProjectFolder(String? path, Int32 maxLevels = MaxLevels)
		{
			if (String.IsNullOrWhiteSpace(path))
			{
				return null;
			}

			try
			{
				String? directory = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path));
				for (Int32 level = 0; level < maxLevels && !String.IsNullOrEmpty(directory); level++)
				{
					if (File.Exists(Path.Combine(directory, ProjectSettings.FileName)))
					{
						return Normalize(directory);
					}

					directory = Path.GetDirectoryName(directory);
				}
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
			{
				// A path that cannot be examined belongs to no project.
			}

			return null;
		}

		/// <summary>
		/// Brings a folder path to the form used to compare and store project folders: full, without a trailing separator.
		/// </summary>
		/// <param name="folder">The folder.</param>
		/// <returns>The normalized path.</returns>
		public static String Normalize(String folder)
		{
			ArgumentException.ThrowIfNullOrEmpty(folder);
			String full = Path.GetFullPath(folder);
			String root = Path.GetPathRoot(full) ?? String.Empty;
			return String.Equals(full, root, StringComparison.OrdinalIgnoreCase) ? full : Path.TrimEndingDirectorySeparator(full);
		}
	}
}
