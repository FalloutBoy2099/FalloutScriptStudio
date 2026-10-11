namespace FalloutScriptStudio.Core.Compilation
{
	/// <summary>
	/// Selects the preprocessor that runs before the sslc compiler.
	/// </summary>
	public enum PreprocessorKind
	{
		/// <summary>The preprocessor built into the sslc compiler.</summary>
		Builtin = 0,

		/// <summary>The external MCPP preprocessor (<c>mcpp.bat</c>).</summary>
		Mcpp = 1,

		/// <summary>The external Open Watcom C preprocessor (<c>wcc.bat</c>).</summary>
		Watcom = 2,
	}
}
