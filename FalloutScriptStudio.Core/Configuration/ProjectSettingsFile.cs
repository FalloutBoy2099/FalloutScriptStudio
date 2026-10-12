namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;
	using System.Text.Json;
	using System.Threading;

	/// <summary>
	/// Reads and writes <c>fss-project.json</c>. The same safety rules as the settings of the program apply: the file is written atomically,
	/// unknown properties are kept, a damaged file is copied to <c>.corrupt</c> and a file that cannot be read is never overwritten.
	/// </summary>
	public static class ProjectSettingsFile
	{
		private const Int32 ReadAttempts = 3;
		private const Int32 RetryDelayMilliseconds = 40;

		/// <summary>Gets the path of the project file of a folder.</summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>The path.</returns>
		public static String GetPath(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return Path.Combine(projectFolder, ProjectSettings.FileName);
		}

		/// <summary>
		/// Resolves a path stored in the project file: a relative path is relative to the project folder.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="path">The stored path.</param>
		/// <returns>The absolute path.</returns>
		public static String ResolvePath(String projectFolder, String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			ArgumentNullException.ThrowIfNull(path);

			String trimmed = path.Trim();
			if (trimmed.Length == 0 || Path.IsPathRooted(trimmed))
			{
				return trimmed;
			}

			return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed, projectFolder));
		}

		/// <summary>
		/// Turns an absolute path into the form stored in the project file: relative to the project folder with forward slashes, or unchanged
		/// when it is on another drive.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="path">The absolute path.</param>
		/// <returns>The path to store.</returns>
		public static String ToStoredPath(String projectFolder, String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			ArgumentNullException.ThrowIfNull(path);

			String trimmed = path.Trim();
			if (trimmed.Length == 0 || !Path.IsPathRooted(trimmed))
			{
				return trimmed.Replace('\\', '/');
			}

			String relative = Path.GetRelativePath(projectFolder, trimmed);
			return Path.IsPathRooted(relative) ? trimmed : relative.Replace('\\', '/');
		}

		/// <summary>
		/// Reads the project file of a folder.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>What was found; the paths of the settings are absolute.</returns>
		public static ProjectFileReadResult Read(String projectFolder)
		{
			String path = GetPath(projectFolder);
			Byte[] content;
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					if (!File.Exists(path))
					{
						return new ProjectFileReadResult(ProjectFileStatus.Missing, null);
					}

					content = File.ReadAllBytes(path);
					break;
				}
				catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
				{
					return new ProjectFileReadResult(ProjectFileStatus.Missing, null);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					if (attempt >= ReadAttempts)
					{
						return new ProjectFileReadResult(ProjectFileStatus.Unreadable, null, ex.Message);
					}

					// A short bounded retry for a file another program (a virus scanner, a backup tool) holds for a moment.
					Thread.Sleep(RetryDelayMilliseconds);
				}
			}

			try
			{
				ProjectSettings? settings = JsonSerializer.Deserialize<ProjectSettings>(content, SettingsJson.Options);
				if (settings is not null)
				{
					settings.Normalize();
					return new ProjectFileReadResult(ProjectFileStatus.Loaded, settings.ConvertPaths(value => ResolvePath(projectFolder, value)));
				}
			}
			catch (Exception ex) when (ex is JsonException or NotSupportedException)
			{
				// Handled below like an empty file.
			}

			KeepCorruptCopy(path);
			return new ProjectFileReadResult(ProjectFileStatus.Damaged, null);
		}

		/// <summary>
		/// Writes the project file of a folder (atomically). The paths are stored relative to the folder.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="settings">The settings, with absolute paths.</param>
		/// <exception cref="IOException">The file could not be written.</exception>
		/// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
		public static void Write(String projectFolder, ProjectSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			ProjectSettings stored = settings.ConvertPaths(value => ToStoredPath(projectFolder, value));
			stored.Normalize();
			AtomicFile.WriteAllBytes(GetPath(projectFolder), JsonSerializer.SerializeToUtf8Bytes(stored, SettingsJson.Options));
		}

		private static void KeepCorruptCopy(String path)
		{
			try
			{
				File.Copy(path, path + ".corrupt", true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The copy is a courtesy; the damaged file itself is untouched until the user saves new settings.
			}
		}
	}
}
