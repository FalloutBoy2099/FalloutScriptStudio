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

			String programDirectory = "-I\"" + this._options.ProgramDirectory + "\" ";
			String includeDirs = this._options.SearchesIncludes
				? "-I\"" + this._options.HeadersDirectory + "\" " + programDirectory
				: programDirectory;

			return includeDirs
				+ usePreprocess
				+ "-q -l -O" + ((Int32)this._options.Optimization).ToString(CultureInfo.InvariantCulture) + " "
				+ (this._options.BackwardCompatibility ? "-b " : String.Empty)
				+ (this._options.ShowWarnings ? String.Empty : "-n ")
				+ (this._options.DebugInfo ? "-d " : String.Empty)
				+ (this.Define is not null ? "-m" + this.Define : String.Empty)
				+ (this._options.ShortCircuit || shortCircuit ? " -s" : String.Empty)
				+ " \"" + Path.GetFileName(inputFile) + "\" -o \"" + outputFile + "\"";
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
				searchPaths += " \"" + this._options.HeadersDirectory + "\"";
			}

			String output = this.GetScratchRelativeToResources() + outputFileName;

			if (this._options.Preprocessor == PreprocessorKind.Watcom)
			{
				// wcc: infile outfile -pc/l i-paths [-d"macro"]
				return "\"" + inputFile + "\" " + output
					+ (preprocessOnly ? " c" : " l")
					+ searchPaths
					+ (this.Define is not null ? " -d" + this.Define : String.Empty);
			}

			// mcpp: infile outfile -W1/0 i-paths [-D"macro"] [-P]
			return "\"" + inputFile + "\" " + output
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
			String quotedSourceDirectory = " \"" + sourceDirectory + "\"";
			String headers = this._options.HeadersDirectory != null
				? "\"" + this._options.HeadersDirectory + "\""
				: "..\\";
			String output = !this._options.CompileToSourceDirectory && this._options.OutputDirectory != null
				? " \"" + this._options.OutputDirectory + "\""
				: quotedSourceDirectory;

			return "\"" + inputFile + "\" "
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
				+ " \"" + inputFile + "\" \"" + outputFile + "\"";
		}

		/// <summary>
		/// Gets the preprocessor definition, or <c>null</c> when it is not set or blank. An empty definition must not reach the tools:
		/// <c>compile.exe -m</c> makes the preprocessor fail with <c>Not an identifier "1"</c>.
		/// </summary>
		private String? Define => String.IsNullOrWhiteSpace(this._options.PreprocessorDefine) ? null : this._options.PreprocessorDefine;

		private String GetScratchRelativeToResources()
		{
			String relative = Path.GetRelativePath(
				Path.GetFullPath(this._options.ResourcesDirectory),
				Path.GetFullPath(this._options.ScratchDirectory));
			return relative + "\\";
		}
	}
}
