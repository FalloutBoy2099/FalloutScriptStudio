namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Globalization;
	using System.IO;

	/// <summary>
	/// Builds the command lines of the external compilation tools. The quoting is identical to the legacy editor.
	/// </summary>
	public sealed class CompilerCommandLineBuilder
	{
		private readonly CompilerOptions _options;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompilerCommandLineBuilder"/> class.
		/// </summary>
		/// <param name="options">The compiler options.</param>
		public CompilerCommandLineBuilder(CompilerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			this._options = options;
		}

		/// <summary>
		/// Builds the sslc (<c>compile.exe</c>) command line.
		/// </summary>
		/// <param name="inputFile">The script to compile; only its file name is passed (the process runs in its folder).</param>
		/// <param name="outputFile">The full path of the output file.</param>
		/// <param name="preprocessOnly">Whether the built-in preprocessor should only preprocess the script.</param>
		/// <param name="shortCircuit">Whether the script itself requires short-circuit evaluation.</param>
		/// <returns>The argument string.</returns>
		public String BuildSslc(String inputFile, String outputFile, Boolean preprocessOnly, Boolean shortCircuit)
		{
			// The built-in preprocessor is disabled when an external mcpp/wcc is used.
			String usePreprocess = String.Empty;
			if (this._options.Preprocessor == PreprocessorKind.Builtin)
			{
				usePreprocess = preprocessOnly ? "-P " : "-p ";
			}

			String programDirectory = "-I" + Quote(this._options.ProgramDirectory) + " ";
			String includeDirs = this._options.SearchesIncludes
				? "-I" + Quote(this._options.HeadersDirectory) + " " + programDirectory
				: programDirectory;

			return includeDirs
				+ usePreprocess
				+ "-q -l -O" + ((Int32)this._options.Optimization).ToString(CultureInfo.InvariantCulture) + " "
				+ (this._options.BackwardCompatibility ? "-b " : String.Empty)
				+ (this._options.ShowWarnings ? String.Empty : "-n ")
				+ (this._options.DebugInfo ? "-d " : String.Empty)
				+ (this.Define is not null ? "-m" + this.Define : String.Empty)
				+ (this._options.ShortCircuit || shortCircuit ? " -s" : String.Empty)
				+ " " + Quote(Path.GetFileName(inputFile)) + " -o " + Quote(outputFile);
		}

		/// <summary>
		/// Builds the argument string of the external preprocessor batch file (<c>mcpp.bat</c> or <c>wcc.bat</c>).
		/// </summary>
		/// <param name="inputFile">The full path of the script.</param>
		/// <param name="outputFileName">The name of the output file inside the scratch folder.</param>
		/// <param name="preprocessOnly">Whether the result is only meant to be displayed.</param>
		/// <returns>The argument string.</returns>
		public String BuildExternalPreprocessor(String inputFile, String outputFileName, Boolean preprocessOnly)
		{
			String searchPaths = " .."; // the parent directory of the resources folder
			if (this._options.SearchesIncludes)
			{
				searchPaths += " " + Quote(this._options.HeadersDirectory);
			}

			// The full path, quoted: the user-profile layout puts the scratch folder in a path with spaces (and far from the resources folder).
			String output = Quote(Path.Combine(Path.GetFullPath(this._options.ScratchDirectory), outputFileName));

			if (this._options.Preprocessor == PreprocessorKind.Watcom)
			{
				// wcc: infile outfile -pc/l i-paths [-d"macro"]
				return Quote(inputFile) + " " + output
					+ (preprocessOnly ? " c" : " l")
					+ searchPaths
					+ (this.Define is not null ? " -d" + this.Define : String.Empty);
			}

			// mcpp: infile outfile -W1/0 i-paths [-D"macro"] [-P]
			return Quote(inputFile) + " " + output
				+ (this._options.ShowWarnings ? " 1" : " 0")
				+ searchPaths
				+ (this.Define is not null ? " -D" + this.Define : String.Empty)
				+ (preprocessOnly ? " -P" : String.Empty);
		}

		/// <summary>
		/// Builds the argument string of the user compilation batch file (<c>usercomp.bat</c>).
		/// </summary>
		/// <param name="inputFile">The full path of the script.</param>
		/// <param name="sourceDirectory">The folder of the script.</param>
		/// <param name="shortCircuit">Whether the script itself requires short-circuit evaluation.</param>
		/// <returns>The argument string.</returns>
		public String BuildUserCompiler(String inputFile, String sourceDirectory, Boolean shortCircuit)
		{
			String quotedSourceDirectory = " " + Quote(sourceDirectory);
			String headers = this._options.HeadersDirectory != null
				? Quote(this._options.HeadersDirectory)
				: "..\\";
			String output = !this._options.CompileToSourceDirectory && this._options.OutputDirectory != null
				? " " + Quote(this._options.OutputDirectory)
				: quotedSourceDirectory;

			return Quote(inputFile) + " "
				+ headers
				+ output
				+ " " + (this.Define ?? "0")
				+ " " + ((Int32)this._options.Optimization).ToString(CultureInfo.InvariantCulture)
				+ quotedSourceDirectory
				+ (this._options.ShortCircuit || shortCircuit ? " -s" : String.Empty);
		}

		/// <summary>
		/// Builds the int2ssl command line.
		/// </summary>
		/// <param name="inputFile">The compiled script.</param>
		/// <param name="outputFile">The decompiled output file.</param>
		/// <returns>The argument string.</returns>
		public String BuildDecompiler(String inputFile, String outputFile)
		{
			return (this._options.DecompileForFallout1 ? "-1" : String.Empty)
				+ (this._options.TabsToSpaces ? " -s" + this._options.TabSize.ToString(CultureInfo.InvariantCulture) : String.Empty)
				+ " " + Quote(inputFile) + " " + Quote(outputFile);
		}

		/// <summary>
		/// Gets the preprocessor definition, or <c>null</c> when it is not set or blank. An empty definition must not reach the tools:
		/// <c>compile.exe -m</c> makes the preprocessor fail with <c>Not an identifier "1"</c>.
		/// </summary>
		private String? Define => String.IsNullOrWhiteSpace(this._options.PreprocessorDefine) ? null : this._options.PreprocessorDefine;

		/// <summary>
		/// Puts a path in double quotes. The C runtime of sslc, mcpp and wcc reads a backslash before a quote as an escape: <c>-I"C:\headers\"</c>
		/// gives the argument <c>C:\headers"</c> and swallows the rest of the line. A trailing backslash is therefore removed, except from the
		/// root of a drive (<c>D:</c> alone is the current folder of that drive), where it is doubled (two backslashes before the quote are
		/// read as one).
		/// </summary>
		/// <param name="path">The path.</param>
		/// <returns>The quoted path.</returns>
		internal static String Quote(String? path)
		{
			path ??= String.Empty;
			Int32 end = path.Length;
			while (end > 0 && path[end - 1] == '\\')
			{
				end--;
			}

			if (end == path.Length)
			{
				return "\"" + path + "\"";
			}

			String trimmed = path.Substring(0, end);
			Boolean root = trimmed.Length == 0 || (trimmed.Length == 2 && Char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':');
			return "\"" + trimmed + (root ? "\\\\" : String.Empty) + "\"";
		}
	}
}