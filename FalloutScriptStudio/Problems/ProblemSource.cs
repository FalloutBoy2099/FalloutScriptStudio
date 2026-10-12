namespace FalloutScriptStudio.Problems
{
	/// <summary>Where a <see cref="Problem"/> comes from; the Errors tab groups and filters by it.</summary>
	public enum ProblemSource
	{
		/// <summary>The script parser (native or built-in).</summary>
		Parse = 0,

		/// <summary>The compiler and the preprocessors: errors, warnings and messages of the build.</summary>
		Build = 1,

		/// <summary>An external tool that is missing or cannot be started (compile.exe, parser.dll, int2ssl.exe, the external editor...).</summary>
		Tool = 2,

		/// <summary>Reading, writing and locating files: open, save, archives, includes.</summary>
		File = 3,

		/// <summary>The settings: damaged or unreadable files, missing folders.</summary>
		Settings = 4,

		/// <summary>The message files (.msg) of the scripts.</summary>
		Message = 5,
	}
}
