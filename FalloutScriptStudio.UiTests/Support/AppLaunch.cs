namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;

	/// <summary>How a test starts the program.</summary>
	internal sealed record AppLaunch
	{
		/// <summary>Gets the files or folders of the command line.</summary>
		public IReadOnlyList<String> Arguments { get; init; } = [];

		/// <summary>Gets the language code of the interface (<c>FSS_UI_TEST_LANGUAGE</c>).</summary>
		public String Language { get; init; } = "en";

		/// <summary>Gets the color mode (<c>FSS_UI_TEST_THEME</c>: light, dark).</summary>
		public String Theme { get; init; } = "light";

		/// <summary>Gets a value indicating whether the program is the copy without <c>parser.dll</c>.</summary>
		public Boolean WithoutParser { get; init; }

		/// <summary>Gets the content of <c>settings.json</c> written to the data folder before the start (JSON text), or <see langword="null"/> for none.</summary>
		public String? SettingsJson { get; init; }

		/// <summary>Gets the files written to the data folder before the start (relative path in the data folder, content).</summary>
		public IReadOnlyDictionary<String, String> DataFiles { get; init; } = new Dictionary<String, String>();

		/// <summary>Gets a value indicating whether the start waits for the first document to be parsed (a script on the command line).</summary>
		public Boolean WaitForParse { get; init; } = true;

		/// <summary>Gets the executable to start (relative to the program copy); <c>FalloutScriptStudio.exe</c> by default.</summary>
		public String Executable { get; init; } = "FalloutScriptStudio.exe";

		/// <summary>Gets an explicit program folder (for example the published folder); <see langword="null"/> for the private copy of the build output.</summary>
		public String? ProgramDirectory { get; init; }
	}
}