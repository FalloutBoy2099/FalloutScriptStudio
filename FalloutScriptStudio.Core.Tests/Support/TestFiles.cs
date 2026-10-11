namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.IO;
	using System.Threading;

	/// <summary>
	/// The one place where the automated tests find their files and create their scratch folders. Everything lives in the folder
	/// <c>_test_files</c> at the repository root: <c>tools</c> (the real sfall tools), <c>samples</c> (real scripts) and <c>work</c>
	/// (throw-away folders of the running tests, git-ignored). The tests never read or write anywhere else (no <c>%TEMP%</c>).
	/// This file is linked into both test projects.
	/// </summary>
	internal static class TestFiles
	{
		/// <summary>The environment variable that overrides the folder of the real tools (<c>compile.exe</c>, <c>parser.dll</c>, <c>int2ssl.exe</c>).</summary>
		public const String ToolsVariable = "FSS_SSLC_DIR";

		/// <summary>The environment variable that overrides the folder with the sample sets (<c>FO2tweaksMk2\source</c>, ...).</summary>
		public const String SamplesVariable = "FSS_SSL_SAMPLES";

		/// <summary>The name of the folder at the repository root.</summary>
		public const String FolderName = "_test_files";

		private const String SolutionFile = "FalloutScriptStudio.sln";

		private static readonly TimeSpan _staleAfter = TimeSpan.FromHours(1);

		private static readonly String _repositoryRoot = FindRepositoryRoot();

		private static readonly String _root = Path.Combine(_repositoryRoot, FolderName);

		private static readonly String _work = Path.Combine(_root, "work");

		static TestFiles()
		{
			SweepStaleFolders();
		}

		/// <summary>Gets the repository root: the first parent folder of the test output that holds the solution file.</summary>
		public static String RepositoryRoot => _repositoryRoot;

		/// <summary>Gets the folder <c>_test_files</c> at the repository root.</summary>
		public static String Root => _root;

		/// <summary>Gets the folder of the real tools: <c>%FSS_SSLC_DIR%</c> or <c>_test_files\tools</c>.</summary>
		public static String Tools => Environment.GetEnvironmentVariable(ToolsVariable) is { Length: > 0 } custom ? custom : Path.Combine(_root, "tools");

		/// <summary>Gets the folder with the sample sets: <c>%FSS_SSL_SAMPLES%</c> or <c>_test_files\samples</c>.</summary>
		public static String Samples => Environment.GetEnvironmentVariable(SamplesVariable) is { Length: > 0 } custom ? custom : Path.Combine(_root, "samples");

		/// <summary>Gets the scratch folder of the tests (<c>_test_files\work</c>); it is created on demand.</summary>
		public static String Work => _work;

		/// <summary>Creates a new empty folder <c>_test_files\work\&lt;name&gt;-&lt;guid&gt;</c>.</summary>
		/// <param name="name">A short readable name (the kind of test that uses the folder).</param>
		/// <returns>The full path of the folder.</returns>
		public static String CreateWorkFolder(String name)
		{
			String path = Path.Combine(_work, name + "-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}

		/// <summary>
		/// Deletes a folder created by <see cref="CreateWorkFolder"/> (best effort: a file that is still locked is retried a few times,
		/// read-only files are made writable). A folder outside <see cref="Work"/> is never touched.
		/// </summary>
		/// <param name="path">The folder.</param>
		public static void DeleteWorkFolder(String path)
		{
			if (!IsInsideWork(path))
			{
				return;
			}

			for (Int32 attempt = 0; attempt < 6 && Directory.Exists(path); attempt++)
			{
				try
				{
					Directory.Delete(path, true);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					MakeWritable(path);
					Thread.Sleep(40 * (attempt + 1));
				}
			}
		}

		private static Boolean IsInsideWork(String path)
		{
			String full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			String work = Path.GetFullPath(_work).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return full.Length > work.Length && full.StartsWith(work + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}

		private static void MakeWritable(String path)
		{
			try
			{
				foreach (String entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
				{
					File.SetAttributes(entry, FileAttributes.Normal);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The next attempt reports the problem.
			}
		}

		/// <summary>Removes what a crashed or killed run left in the scratch folder (anything older than one hour).</summary>
		private static void SweepStaleFolders()
		{
			try
			{
				if (!Directory.Exists(_work))
				{
					return;
				}

				DateTime limit = DateTime.UtcNow - _staleAfter;
				foreach (String entry in Directory.GetFileSystemEntries(_work))
				{
					DateTime last = Directory.Exists(entry)
						? Directory.GetLastWriteTimeUtc(entry)
						: File.GetLastWriteTimeUtc(entry);
					if (last >= limit)
					{
						continue;
					}

					if (Directory.Exists(entry))
					{
						DeleteWorkFolder(entry);
					}
					else
					{
						TryDeleteFile(entry);
					}
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A folder that cannot be swept now is swept by a later run.
			}
		}

		private static void TryDeleteFile(String path)
		{
			try
			{
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A later run sweeps it.
			}
		}

		private static String FindRepositoryRoot()
		{
			DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory is not null)
			{
				if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
				{
					return directory.FullName;
				}

				directory = directory.Parent;
			}

			throw new InvalidOperationException("The repository root (" + SolutionFile + ") was not found above " + AppContext.BaseDirectory + ".");
		}
	}
}
