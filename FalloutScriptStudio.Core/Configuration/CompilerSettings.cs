namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// Compilation, preprocessing and decompilation options.
	/// </summary>
	public sealed class CompilerSettings
	{
		/// <summary>Gets or sets the optimization level.</summary>
		public OptimizationLevel Optimization { get; set; } = OptimizationLevel.Basic;

		/// <summary>Gets or sets a value indicating whether compiler warnings are shown.</summary>
		public Boolean ShowWarnings { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether additional debug information is produced and shown.</summary>
		public Boolean DebugInfo { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether short-circuit evaluation is forced for every script.</summary>
		public Boolean ShortCircuit { get; set; }

		/// <summary>Gets or sets a value indicating whether the backward compatibility mode of the compiler is used.</summary>
		public Boolean BackwardCompatibility { get; set; }

		/// <summary>Gets or sets the preprocessor that runs before the compiler.</summary>
		public PreprocessorKind Preprocessor { get; set; } = PreprocessorKind.Mcpp;

		/// <summary>Gets or sets the preprocessor definition (conditional compilation macro), or <see langword="null"/> for none.</summary>
		public String? PreprocessorDefine { get; set; }

		/// <summary>Gets or sets a value indicating whether the headers folder is added to the include search paths.</summary>
		public Boolean SearchIncludePath { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether compiled scripts are written next to their sources instead of the output folder.</summary>
		public Boolean CompileToSourceDirectory { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the user batch file compiles the scripts.</summary>
		public Boolean UseUserCommand { get; set; }

		/// <summary>Gets or sets a value indicating whether a failed compilation brings the build log to the front.</summary>
		public Boolean WarnOnFailedCompile { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether a mass compilation compiles several scripts at the same time.</summary>
		public Boolean MultiThreadedBatch { get; set; } = true;

		/// <summary>Gets or sets the decompiler that turns compiled scripts back into source.</summary>
		public DecompilerKind Decompiler { get; set; } = DecompilerKind.BuiltIn;

		/// <summary>Gets or sets the path of the external <c>int2ssl.exe</c>, or <see langword="null"/> to look for it in the resources folder. Used only with <see cref="DecompilerKind.ExternalInt2Ssl"/>.</summary>
		public String? ExternalDecompilerPath { get; set; }

		/// <summary>Gets or sets a value indicating whether the decompiler is asked to produce Fallout 1 code.</summary>
		public Boolean DecompileForFallout1 { get; set; }

		/// <summary>Gets or sets a value indicating whether only the old external decompiler (<c>int2ssl_v35.exe</c>) is used. It has no effect on the built-in decompiler.</summary>
		public Boolean UseLegacyDecompiler { get; set; }

		/// <summary>Creates a copy that can be changed without touching this instance.</summary>
		/// <returns>The copy.</returns>
		internal CompilerSettings Copy()
		{
			return (CompilerSettings)this.MemberwiseClone();
		}
	}
}
