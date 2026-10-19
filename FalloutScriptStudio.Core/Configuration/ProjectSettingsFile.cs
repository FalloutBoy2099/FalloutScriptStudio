namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;
	using System.Text;
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

			if (TryParse(content, projectFolder, out ProjectSettings? settings, out _))
			{
				return new ProjectFileReadResult(ProjectFileStatus.Loaded, settings, null, content);
			}

			KeepCorruptCopy(path);
			return new ProjectFileReadResult(ProjectFileStatus.Damaged, null, null, content);
		}

		/// <summary>
		/// Interprets the content of a project file (nothing is read from or written to the disk).
		/// </summary>
		/// <param name="content">The bytes of the file.</param>
		/// <param name="projectFolder">The project folder, which relative paths are resolved against.</param>
		/// <param name="settings">The settings with absolute paths when the content is valid, otherwise <see langword="null"/>.</param>
		/// <param name="error">Why the content is not valid, otherwise <see langword="null"/>.</param>
		/// <returns><see langword="true"/> when the content is valid.</returns>
		public static Boolean TryParse(Byte[] content, String projectFolder, out ProjectSettings? settings, out ProjectFileError? error)
		{
			ArgumentNullException.ThrowIfNull(content);
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);

			settings = null;
			error = null;
			try
			{
				ProjectSettings? parsed = JsonSerializer.Deserialize<ProjectSettings>(ToUtf8(content), SettingsJson.Options);
				if (parsed is not null)
				{
					parsed.Normalize();
					settings = parsed.ConvertPaths(value => ResolvePath(projectFolder, value));
					return true;
				}

				error = new ProjectFileError("The file does not contain a settings object");
			}
			catch (JsonException ex)
			{
				error = new ProjectFileError(CleanMessage(ex.Message), ex.LineNumber is { } line ? (Int32)line + 1 : -1, ex.BytePositionInLine is { } column ? (Int32)column + 1 : -1);
			}
			catch (NotSupportedException ex)
			{
				error = new ProjectFileError(ex.Message);
			}

			return false;
		}

		/// <summary>
		/// Writes the project file of a folder (atomically). The paths are stored relative to the folder.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="settings">The settings, with absolute paths.</param>
		/// <returns>The bytes that were written.</returns>
		/// <exception cref="IOException">The file could not be written.</exception>
		/// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
		public static Byte[] Write(String projectFolder, ProjectSettings settings)
		{
			Byte[] content = Serialize(projectFolder, settings);
			AtomicFile.WriteAllBytes(GetPath(projectFolder), content);
			return content;
		}

		/// <summary>
		/// Creates the content of the project file of a folder: the paths are stored relative to the folder.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="settings">The settings, with absolute paths.</param>
		/// <returns>The bytes to write.</returns>
		public static Byte[] Serialize(String projectFolder, ProjectSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			ProjectSettings stored = settings.ConvertPaths(value => ToStoredPath(projectFolder, value));
			stored.Normalize();
			return JsonSerializer.SerializeToUtf8Bytes(stored, SettingsJson.Options);
		}

		/// <summary>
		/// Brings the bytes of a file saved by a Windows editor to plain UTF-8: Notepad and PowerShell write a byte order mark, and
		/// "Unicode" files are UTF-16; the JSON parser accepts neither.
		/// </summary>
		private static Byte[] ToUtf8(Byte[] content)
		{
			if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
			{
				return content[3..];
			}

			if (content.Length >= 2 && ((content[0] == 0xFF && content[1] == 0xFE) || (content[0] == 0xFE && content[1] == 0xFF)))
			{
				Encoding encoding = content[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
				return Encoding.UTF8.GetBytes(encoding.GetString(content, 2, content.Length - 2));
			}

			return content;
		}

		/// <summary>The parser appends " Path: $.x | LineNumber: 2 | BytePositionInLine: 5." to its explanation; the position is reported separately and the final period dropped (the texts that show the message add their own).</summary>
		private static String CleanMessage(String message)
		{
			Int32 position = message.IndexOf(" Path:", StringComparison.Ordinal);
			return (position > 0 ? message[..position] : message).Trim().TrimEnd('.');
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