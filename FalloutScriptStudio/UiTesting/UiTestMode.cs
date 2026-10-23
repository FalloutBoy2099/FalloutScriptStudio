namespace FalloutScriptStudio.UiTesting
{
	using System;
	using System.IO;
	using Core.Configuration;
	using Services;

	/// <summary>
	/// The opt-in mode of the application that lets the UI smoke tests (<c>FalloutScriptStudio.UiTests</c>) drive the real program without
	/// disturbing the developer: every window is placed far outside of the monitors and is never activated, the first-run Settings window
	/// is skipped, the settings, the scratch folder and the log live in a folder of their own, the single-instance lock is private, and the
	/// language and the color mode are fixed. It is switched on by the environment variable <c>FSS_UI_TEST=1</c>, which <see cref="Read"/> reads
	/// (the only place that does); without it nothing changes. The decisions are pure, the hooks that apply them are in
	/// <see cref="UiTestWindowGuard"/>.
	/// </summary>
	public sealed record UiTestMode
	{
		/// <summary>The environment variable that switches the mode on (<c>1</c>, <c>true</c> or <c>yes</c>).</summary>
		public const String EnabledVariable = "FSS_UI_TEST";

		/// <summary>The environment variable with the folder of the settings, the scratch folder and the log.</summary>
		public const String DataVariable = "FSS_UI_TEST_DATA";

		/// <summary>The environment variable with the language code of the interface (default <see cref="DefaultLanguage"/>, so that the texts do not depend on the machine).</summary>
		public const String LanguageVariable = "FSS_UI_TEST_LANGUAGE";

		/// <summary>The environment variable with the color mode (<c>light</c>, <c>dark</c> or <c>system</c>; default light).</summary>
		public const String ThemeVariable = "FSS_UI_TEST_THEME";

		/// <summary>The language of the interface when <see cref="LanguageVariable"/> is not set.</summary>
		public const String DefaultLanguage = "en";

		private const String TestFilesFolder = "_test_files";

		/// <summary>Gets the mode of a normal run: everything is off.</summary>
		public static UiTestMode Off { get; } = new();

		/// <summary>Gets a value indicating whether the program runs under the UI smoke tests.</summary>
		public Boolean IsEnabled { get; init; }

		/// <summary>Gets the folder of the settings, the scratch folder and the log (<see langword="null"/> when the mode is off).</summary>
		public String? DataDirectory { get; init; }

		/// <summary>Gets the language code of the interface.</summary>
		public String Language { get; init; } = DefaultLanguage;

		/// <summary>Gets the color mode the program starts with.</summary>
		public AppTheme Theme { get; init; } = AppTheme.Light;

		/// <summary>Gets a value indicating whether the first-run Settings window opens (never under test: it would block the start).</summary>
		public Boolean OpensFirstRunSettings => !this.IsEnabled;

		/// <summary>Gets a value indicating whether the program may activate a window or bring it to the front (never under test: it would steal the focus).</summary>
		public Boolean ActivatesWindows => !this.IsEnabled;

		/// <summary>Gets a value indicating whether the language and the color mode come from the environment instead of the stored layout.</summary>
		public Boolean OverridesLayout => this.IsEnabled;

		/// <summary>Reads the mode from the environment of the process (the only place that reads <c>FSS_UI_TEST</c>).</summary>
		/// <param name="programDirectory">The folder of the program.</param>
		/// <returns>The mode.</returns>
		public static UiTestMode Read(String programDirectory)
		{
			return FromEnvironment(Environment.GetEnvironmentVariable, programDirectory, Directory.Exists, Guid.NewGuid);
		}

		/// <summary>Decides the mode from the values of the environment variables.</summary>
		/// <param name="getVariable">Reads an environment variable (<see langword="null"/> when it is not set).</param>
		/// <param name="programDirectory">The folder of the program.</param>
		/// <param name="directoryExists">Tells whether a folder exists.</param>
		/// <param name="newId">Creates the identifier of a default data folder.</param>
		/// <returns>The mode.</returns>
		public static UiTestMode FromEnvironment(Func<String, String?> getVariable, String programDirectory, Func<String, Boolean> directoryExists, Func<Guid> newId)
		{
			ArgumentNullException.ThrowIfNull(getVariable);
			ArgumentNullException.ThrowIfNull(programDirectory);
			ArgumentNullException.ThrowIfNull(directoryExists);
			ArgumentNullException.ThrowIfNull(newId);

			if (!IsSwitchedOn(getVariable(EnabledVariable)))
			{
				return Off;
			}

			String? data = getVariable(DataVariable);
			String language = getVariable(LanguageVariable) is { Length: > 0 } code ? code.Trim() : DefaultLanguage;
			return new UiTestMode
			{
				IsEnabled = true,
				DataDirectory = Path.GetFullPath(String.IsNullOrWhiteSpace(data) ? GetDefaultDataDirectory(programDirectory, directoryExists, newId()) : data.Trim()),
				Language = language,
				Theme = ParseTheme(getVariable(ThemeVariable)),
			};
		}

		/// <summary>
		/// Gets the default data folder <c>_test_files\work\ui-smoke-&lt;guid&gt;</c>: in the <c>_test_files</c> folder of the repository when the
		/// program runs from its build output (found by walking up from the program folder), otherwise next to the program.
		/// </summary>
		/// <param name="programDirectory">The folder of the program.</param>
		/// <param name="directoryExists">Tells whether a folder exists.</param>
		/// <param name="id">The identifier that makes the folder unique.</param>
		/// <returns>The folder.</returns>
		public static String GetDefaultDataDirectory(String programDirectory, Func<String, Boolean> directoryExists, Guid id)
		{
			ArgumentNullException.ThrowIfNull(programDirectory);
			ArgumentNullException.ThrowIfNull(directoryExists);

			String name = "ui-smoke-" + id.ToString("N");
			DirectoryInfo? directory = new(Path.GetFullPath(programDirectory));
			while (directory is not null)
			{
				String candidate = Path.Combine(directory.FullName, TestFilesFolder);
				if (directoryExists(candidate))
				{
					return Path.Combine(candidate, "work", name);
				}

				directory = directory.Parent;
			}

			return Path.Combine(Path.GetFullPath(programDirectory), TestFilesFolder, "work", name);
		}

		/// <summary>Creates the paths of the program: everything it writes (settings, templates, scratch folder, log) goes to the data folder.</summary>
		/// <param name="programDirectory">The folder of the program.</param>
		/// <returns>The paths.</returns>
		public AppPaths CreatePaths(String programDirectory)
		{
			ArgumentNullException.ThrowIfNull(programDirectory);
			return this.IsEnabled && this.DataDirectory is { } data
				? AppPaths.ForInstallation(programDirectory, data, _ => false)
				: AppPaths.ForInstallation(programDirectory);
		}

		/// <summary>Gets the text the single-instance lock is derived from: private to the data folder under test, so that the developer's own editor is never reached.</summary>
		/// <param name="programDirectory">The folder of the program.</param>
		/// <returns>The text.</returns>
		public String GetInstanceKey(String programDirectory)
		{
			ArgumentNullException.ThrowIfNull(programDirectory);
			return this.IsEnabled && this.DataDirectory is { } data ? data : programDirectory;
		}

		private static Boolean IsSwitchedOn(String? value)
		{
			return value is not null
				&& (value.Trim().Equals("1", StringComparison.Ordinal)
					|| value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
					|| value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
		}

		private static AppTheme ParseTheme(String? value)
		{
			return value?.Trim().ToLowerInvariant() switch
			{
				"dark" => AppTheme.Dark,
				"system" => AppTheme.System,
				_ => AppTheme.Light,
			};
		}
	}
}