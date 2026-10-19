namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using Parsing;

	/// <summary>
	/// Immutable snapshot of everything the compiler services need to know about the environment and the user settings.
	/// </summary>
	public sealed record CompilerOptions
	{
		/// <summary>Gets the program folder (passed to sslc as an include directory).</summary>
		public required String ProgramDirectory { get; init; }

		/// <summary>Gets the folder with the external tools (<c>compile.exe</c>, <c>mcpp.bat</c>, ... and the default location of <c>int2ssl.exe</c>).</summary>
		public required String ResourcesDirectory { get; init; }

		/// <summary>Gets the scratch folder for temporary files (<c>scrTemp</c>).</summary>
		public required String ScratchDirectory { get; init; }

		/// <summary>Gets the optimization level.</summary>
		public OptimizationLevel Optimization { get; init; } = OptimizationLevel.Basic;

		/// <summary>Gets a value indicating whether compiler warnings are enabled.</summary>
		public Boolean ShowWarnings { get; init; } = true;

		/// <summary>Gets a value indicating whether additional debug information is produced (<c>-d</c>).</summary>
		public Boolean DebugInfo { get; init; } = true;

		/// <summary>Gets a value indicating whether short-circuit evaluation is forced for all scripts.</summary>
		public Boolean ShortCircuit { get; init; }

		/// <summary>Gets a value indicating whether the backward compatibility mode (<c>-b</c>) is used.</summary>
		public Boolean BackwardCompatibility { get; init; }

		/// <summary>Gets the preprocessor in use.</summary>
		public PreprocessorKind Preprocessor { get; init; } = PreprocessorKind.Mcpp;

		/// <summary>Gets the preprocessor definition (conditional compilation macro), if any.</summary>
		public String? PreprocessorDefine { get; init; }

		/// <summary>Gets a value indicating whether the headers folder is searched for includes.</summary>
		public Boolean SearchIncludePath { get; init; } = true;

		/// <summary>Gets the path of the <c>scripts.h</c> file; include searching only works when it is set.</summary>
		public String? ScriptsHeaderFile { get; init; }

		/// <summary>Gets the folder with the header files.</summary>
		public String? HeadersDirectory { get; init; }

		/// <summary>Gets a value indicating whether compiled scripts are written next to their sources (ignoring <see cref="OutputDirectory"/>).</summary>
		public Boolean CompileToSourceDirectory { get; init; } = true;

		/// <summary>Gets the folder for compiled scripts.</summary>
		public String? OutputDirectory { get; init; }

		/// <summary>Gets a value indicating whether the user batch file (<c>usercomp.bat</c>) compiles the script.</summary>
		public Boolean UseUserCommand { get; init; }

		/// <summary>Gets the decompiler that turns <c>.int</c> files back into source.</summary>
		public DecompilerKind Decompiler { get; init; } = DecompilerKind.BuiltIn;

		/// <summary>Gets the path of the external <c>int2ssl.exe</c> (or of its folder) used when <see cref="Decompiler"/> is <see cref="DecompilerKind.ExternalInt2Ssl"/>; the resources folder is used when it is not set.</summary>
		public String? ExternalDecompilerPath { get; init; }

		/// <summary>Gets a value indicating whether the decompiler is asked to emit Fallout 1 code (<c>-1</c>).</summary>
		public Boolean DecompileForFallout1 { get; init; }

		/// <summary>Gets a value indicating whether only the old external decompiler (<c>int2ssl_v35.exe</c>) is used; it has no effect on the built-in decompiler.</summary>
		public Boolean UseLegacyDecompiler { get; init; }

		/// <summary>Gets a value indicating whether the decompiler indents with spaces.</summary>
		public Boolean TabsToSpaces { get; init; } = true;

		/// <summary>Gets the indentation width used by the decompiler.</summary>
		public Int32 TabSize { get; init; } = 3;

		/// <summary>Gets a value indicating whether the include folders are passed to the tools.</summary>
		public Boolean SearchesIncludes => IncludeSearch.IsEnabled(this.SearchIncludePath, this.ScriptsHeaderFile);
	}
}