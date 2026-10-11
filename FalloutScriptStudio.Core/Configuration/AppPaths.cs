namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;

	/// <summary>
	/// The folders and well-known files of an installation of the editor. All paths are derived from one base directory.
	/// </summary>
	/// <param name="ProgramDirectory">The folder of the program.</param>
	/// <param name="SettingsDirectory">The folder with the persisted user data.</param>
	/// <param name="ResourcesDirectory">The folder with the external tools, templates and fonts.</param>
	/// <param name="DescriptionsDirectory">The folder with the function descriptions.</param>
	/// <param name="ScratchDirectory">The folder for temporary files of the current session (<c>scrTemp</c>).</param>
	/// <param name="TemplatesDirectory">The folder with script templates.</param>
	/// <param name="FontsDirectory">The folder with additional editor fonts.</param>
	/// <param name="UserDataDirectory">The folder the program writes its log to; the program folder, or a folder of the user profile when the program folder is read-only.</param>
	public sealed record AppPaths(
		String ProgramDirectory,
		String SettingsDirectory,
		String ResourcesDirectory,
		String DescriptionsDirectory,
		String ScratchDirectory,
		String TemplatesDirectory,
		String FontsDirectory,
		String? UserDataDirectory = null)
	{
		/// <summary>Gets the log file of the program.</summary>
		public String LogFile => Path.Combine(this.UserDataDirectory ?? this.ProgramDirectory, "sse.log");

		/// <summary>Gets the file with the application settings.</summary>
		public String SettingsFile => Path.Combine(this.SettingsDirectory, "settings.json");

		/// <summary>Gets the file with the recent file lists.</summary>
		public String RecentFilesFile => Path.Combine(this.SettingsDirectory, "recent.json");

		/// <summary>Gets the file with the last caret line of every script.</summary>
		public String ScriptPositionsFile => Path.Combine(this.SettingsDirectory, "positions.json");

		/// <summary>Gets the file with the folded procedures of every script.</summary>
		public String FoldingFile => Path.Combine(this.SettingsDirectory, "folding.json");

		/// <summary>Gets the file with the search history and search folders.</summary>
		public String SearchHistoryFile => Path.Combine(this.SettingsDirectory, "search-history.json");

		/// <summary>Gets the file with the list of preprocessor definitions.</summary>
		public String PreprocessorDefinesFile => Path.Combine(this.SettingsDirectory, "preprocessor-defines.json");

		/// <summary>Gets the legacy binary settings file.</summary>
		public String LegacySettingsFile => Path.Combine(this.SettingsDirectory, "settings.dat");

		/// <summary>Gets the legacy binary file with recent files and script positions.</summary>
		public String LegacyRecentFile => Path.Combine(this.SettingsDirectory, "recent.dat");

		/// <summary>Gets the legacy binary file with folded procedures.</summary>
		public String LegacyFoldingFile => Path.Combine(this.SettingsDirectory, "folding.dat");

		/// <summary>Gets the legacy text file with the search history.</summary>
		public String LegacySearchHistoryFile => Path.Combine(this.SettingsDirectory, "SearchHistory.ini");

		/// <summary>Gets the legacy text file with the search folders.</summary>
		public String LegacySearchFoldersFile => Path.Combine(this.SettingsDirectory, "SearchPaths.ini");

		/// <summary>Gets the legacy text file with the preprocessor definitions.</summary>
		public String LegacyPreprocessorDefinesFile => Path.Combine(this.SettingsDirectory, "PreprocDefine.ini");

		/// <summary>
		/// Creates the paths for an installation in <paramref name="baseDirectory"/>; everything the program writes lives next to it.
		/// </summary>
		/// <param name="baseDirectory">The program folder.</param>
		/// <returns>The paths.</returns>
		public static AppPaths FromBaseDirectory(String baseDirectory)
		{
			return Create(baseDirectory, baseDirectory);
		}

		/// <summary>
		/// Creates the paths for an installation in <paramref name="baseDirectory"/>. The settings, the scratch folder, the templates
		/// and the log are kept next to the program (portable layout of the legacy editor) when that folder is writable, and in the
		/// user profile otherwise (for example when the program is installed in <c>Program Files</c>).
		/// </summary>
		/// <param name="baseDirectory">The program folder.</param>
		/// <param name="userDataDirectory">The folder used when the program folder is read-only; by default <c>%LOCALAPPDATA%\FalloutScriptStudio</c>.</param>
		/// <param name="canWrite">Tells whether a folder can be written to (replaced in tests); by default a probe file is created.</param>
		/// <returns>The paths.</returns>
		public static AppPaths ForInstallation(String baseDirectory, String? userDataDirectory = null, Func<String, Boolean>? canWrite = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(baseDirectory);

			Func<String, Boolean> probe = canWrite ?? CanWriteTo;
			if (probe(baseDirectory))
			{
				return Create(baseDirectory, baseDirectory);
			}

			String fallback = userDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FalloutScriptStudio");
			return Create(baseDirectory, fallback);
		}

		private static AppPaths Create(String baseDirectory, String userDataDirectory)
		{
			ArgumentException.ThrowIfNullOrEmpty(baseDirectory);

			String resources = Path.Combine(baseDirectory, "resources");
			Boolean portable = String.Equals(baseDirectory, userDataDirectory, StringComparison.OrdinalIgnoreCase);
			return new AppPaths(
				baseDirectory,
				Path.Combine(userDataDirectory, "settings"),
				resources,
				Path.Combine(baseDirectory, "descriptions"),
				Path.Combine(userDataDirectory, "scrTemp"),
				portable ? Path.Combine(resources, "templates") : Path.Combine(userDataDirectory, "templates"),
				Path.Combine(resources, "fonts"),
				userDataDirectory);
		}

		private static Boolean CanWriteTo(String directory)
		{
			try
			{
				Directory.CreateDirectory(directory);
				String probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
				File.WriteAllBytes(probe, Array.Empty<Byte>());
				File.Delete(probe);
				return true;
			}
			catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the path of a file in the resources folder (for example <c>compile.exe</c>).
		/// </summary>
		/// <param name="fileName">The file name.</param>
		/// <returns>The full path.</returns>
		public String GetResourcePath(String fileName)
		{
			return Path.Combine(this.ResourcesDirectory, fileName);
		}

		/// <summary>
		/// Creates the folders the program writes to (settings, templates and the scratch folder).
		/// </summary>
		public void EnsureCreated()
		{
			Directory.CreateDirectory(this.SettingsDirectory);
			Directory.CreateDirectory(this.TemplatesDirectory);
			Directory.CreateDirectory(this.ScratchDirectory);
			this.CopyShippedTemplates();
		}

		/// <summary>
		/// When the templates live in the user profile, makes the templates shipped with the program available there.
		/// </summary>
		private void CopyShippedTemplates()
		{
			String shipped = Path.Combine(this.ResourcesDirectory, "templates");
			if (!Directory.Exists(shipped) || String.Equals(Path.GetFullPath(shipped), Path.GetFullPath(this.TemplatesDirectory), StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			foreach (String file in Directory.GetFiles(shipped))
			{
				String target = Path.Combine(this.TemplatesDirectory, Path.GetFileName(file));
				if (!File.Exists(target))
				{
					File.Copy(file, target);
				}
			}
		}

		/// <summary>
		/// Creates the scratch folder or, if it exists, deletes the files left over from a previous session.
		/// </summary>
		public void PrepareScratchDirectory()
		{
			if (!Directory.Exists(this.ScratchDirectory))
			{
				Directory.CreateDirectory(this.ScratchDirectory);
				return;
			}

			foreach (String file in Directory.GetFiles(this.ScratchDirectory))
			{
				File.Delete(file);
			}
		}

		/// <summary>
		/// Deletes the scratch folder with everything in it (done when the program exits).
		/// </summary>
		public void DeleteScratchDirectory()
		{
			if (Directory.Exists(this.ScratchDirectory))
			{
				Directory.Delete(this.ScratchDirectory, true);
			}
		}
	}
}
