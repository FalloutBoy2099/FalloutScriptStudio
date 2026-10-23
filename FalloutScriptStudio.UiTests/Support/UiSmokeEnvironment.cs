namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.TestSupport;

	/// <summary>Where the smoke tests find the built program and where they write their pictures and report, and whether they run at all.</summary>
	internal static class UiSmokeEnvironment
	{
		/// <summary>The environment variable that switches the real-UI tests on (<c>1</c> or <c>true</c>).</summary>
		public const String RunVariable = "FSS_RUN_UI_TESTS";

		/// <summary>The environment variable that names the folder of the built program (default: the build output of the application project in the same configuration as the tests).</summary>
		public const String AppDirectoryVariable = "FSS_UI_APP_DIR";

		/// <summary>The environment variable that names the folder of the pictures and the report (default <c>_test_files\work\ui-smoke</c>).</summary>
		public const String OutputVariable = "FSS_UI_SMOKE_OUT";

		/// <summary>The environment variable that keeps the data folders of the tests (for a post mortem) instead of deleting them.</summary>
		public const String KeepVariable = "FSS_UI_SMOKE_KEEP";

		/// <summary>Gets the reason the tests are skipped, or <see langword="null"/> when they run.</summary>
		public static String? SkipReason
		{
			get
			{
				if (!IsSwitchedOn(Environment.GetEnvironmentVariable(RunVariable)))
				{
					return "The real-UI smoke tests start the program off-screen and are skipped unless " + RunVariable + "=1 (run scripts\\Run-UiSmoke.ps1).";
				}

				if (!File.Exists(Path.Combine(AppDirectory, "FalloutScriptStudio.exe")))
				{
					return "The program is not built in " + AppDirectory + " (build FalloutScriptStudio, or set " + AppDirectoryVariable + ").";
				}

				return null;
			}
		}

		/// <summary>Gets the folder of the built program.</summary>
		public static String AppDirectory
		{
			get
			{
				if (Environment.GetEnvironmentVariable(AppDirectoryVariable) is { Length: > 0 } custom)
				{
					return Path.GetFullPath(custom);
				}

				return Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "bin", Configuration, "net10.0-windows");
			}
		}

		/// <summary>Gets the folder that <c>Publish.ps1</c> fills with the two executables.</summary>
		public static String PublishDirectory => Path.Combine(TestFiles.RepositoryRoot, "publish");

		/// <summary>
		/// Gets the reason the tests of the published executables are skipped, or <see langword="null"/> when they run. An executable that was published
		/// before the last change of the sources does not know the UI test mode and would open its windows on the screen of the developer: it is
		/// never started (publish again with <c>Publish.ps1</c>).
		/// </summary>
		public static String? PublishedSkipReason
		{
			get
			{
				if (SkipReason is { } reason)
				{
					return reason;
				}

				String[] executables = [Path.Combine(PublishDirectory, "FalloutScriptStudio.exe"), Path.Combine(PublishDirectory, "FalloutScriptStudio-NET.exe")];
				if (executables.Any(executable => !File.Exists(executable)))
				{
					return "The executables are not published in " + PublishDirectory + " (run Publish.ps1).";
				}

				DateTime published = executables.Min(executable => File.GetLastWriteTimeUtc(executable));
				DateTime newest = NewestSource();
				return newest > published ? "The executables in " + PublishDirectory + " are older than the sources (run Publish.ps1): an old build does not know the UI test mode and would show its windows." : null;
			}
		}

		/// <summary>Gets the folder of the pictures and the report.</summary>
		public static String OutputDirectory => Environment.GetEnvironmentVariable(OutputVariable) is { Length: > 0 } custom
			? Path.GetFullPath(custom)
			: Path.Combine(TestFiles.Work, "ui-smoke");

		/// <summary>Gets a value indicating whether the data folders of the tests are kept.</summary>
		public static Boolean KeepData => IsSwitchedOn(Environment.GetEnvironmentVariable(KeepVariable));

		/// <summary>Gets the build configuration of the tests (the name of the folder above the target framework folder of the output).</summary>
		public static String Configuration
		{
			get
			{
				DirectoryInfo? framework = new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				String name = framework.Parent?.Name ?? "Debug";
				return name.Equals("Release", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
			}
		}

		private static DateTime NewestSource()
		{
			DateTime newest = DateTime.MinValue;
			foreach (String project in new[] { "FalloutScriptStudio", "FalloutScriptStudio.Core", "FalloutScriptStudio.ParserHost" })
			{
				String folder = Path.Combine(TestFiles.RepositoryRoot, project);
				if (!Directory.Exists(folder))
				{
					continue;
				}

				foreach (String file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
				{
					String relative = Path.GetRelativePath(folder, file);
					String first = relative.Split(Path.DirectorySeparatorChar)[0];
					if (first.Equals("bin", StringComparison.OrdinalIgnoreCase) || first.Equals("obj", StringComparison.OrdinalIgnoreCase) || !new[] { ".cs", ".xaml", ".csproj", ".props", ".targets", ".resx", ".ini", ".txt" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
					{
						continue;
					}

					DateTime written = File.GetLastWriteTimeUtc(file);
					if (written > newest)
					{
						newest = written;
					}
				}
			}

			return newest;
		}

		private static Boolean IsSwitchedOn(String? value)
		{
			return value is not null && (value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
		}
	}
}