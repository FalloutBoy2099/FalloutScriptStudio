namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Immutable configuration of the parsers. The application builds it from its settings; the parsers never read
	/// global settings.
	/// </summary>
	public sealed record ParserOptions
	{
		/// <summary>The default name of the scratch file handed to the native parser.</summary>
		public const String DefaultScratchFileName = "parser.ssl";

		/// <summary>The default name of the log file written by the native parser.</summary>
		public const String DefaultNativeLogFileName = "errors.txt";

		/// <summary>Gets a value indicating whether the native parser should be used (legacy <c>Settings.enableParser</c>).</summary>
		public Boolean EnableNativeParser { get; init; } = true;

		/// <summary>Gets the preprocessor definitions passed to the native parser (legacy <c>Settings.preprocDef</c>).</summary>
		public String? PreprocessorDefinitions { get; init; }

		/// <summary>
		/// Gets the directory with the game headers used to resolve <c>#include</c>, or <c>null</c> when include search is
		/// disabled (legacy: <c>Settings.IsSearchIncludes ? Settings.pathHeadersFiles : null</c>).
		/// </summary>
		public String? HeadersDirectory { get; init; }

		/// <summary>
		/// Gets the directory scanned for global macros (legacy <c>Settings.pathHeadersFiles</c>, regardless of the include search switch);
		/// <see cref="HeadersDirectory"/> is used when this is <c>null</c>.
		/// </summary>
		public String? GlobalMacroDirectory { get; init; }

		/// <summary>Gets the backward compatibility mode passed to the native parser (legacy <c>Settings.compileBackwardMode</c>).</summary>
		public Int32 BackwardCompatibilityMode { get; init; }

		/// <summary>Gets the directory of the application, searched last when resolving <c>#include</c>.</summary>
		public String? ProgramDirectory { get; init; }

		/// <summary>Gets the directory for the scratch file of the native parser; the system temp directory is used when <c>null</c>.</summary>
		public String? ScratchDirectory { get; init; }

		/// <summary>Gets the name of the scratch file.</summary>
		public String ScratchFileName { get; init; } = DefaultScratchFileName;

		/// <summary>Gets the path of the native <c>parser.dll</c>; <c>null</c> or a missing file means the native parser is unavailable.</summary>
		public String? NativeParserPath { get; init; }

		/// <summary>
		/// Gets the path of the log file the native parser writes (<c>errors.txt</c> in its current directory);
		/// <c>errors.txt</c> in the current directory of the process is used when <c>null</c>.
		/// </summary>
		public String? NativeLogPath { get; init; }

		/// <summary>Gets the directory scanned for global macros.</summary>
		public String? EffectiveGlobalMacroDirectory => this.GlobalMacroDirectory ?? this.HeadersDirectory;

		/// <summary>Creates the include resolver matching these options.</summary>
		/// <returns>A new resolver.</returns>
		public IncludeResolver CreateIncludeResolver()
		{
			return new IncludeResolver(this.HeadersDirectory, this.ProgramDirectory);
		}
	}
}
