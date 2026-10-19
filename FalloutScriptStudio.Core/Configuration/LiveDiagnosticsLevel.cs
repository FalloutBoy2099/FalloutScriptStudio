namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// How much of the semantic analysis of a script (wrong number of arguments, unknown names, a <c>begin</c> without <c>end</c>...) is shown
	/// in the editor while the script is typed. The names are stored in the settings file: never rename or remove one.
	/// </summary>
	public enum LiveDiagnosticsLevel : Byte
	{
		/// <summary>Nothing: only the compiler and the parser report mistakes.</summary>
		Off = 0,

		/// <summary>Only what the compiler (sslc) would reject.</summary>
		ErrorsOnly = 1,

		/// <summary>The errors and the findings that are probably mistakes but that the compiler accepts (a call that does not match the description of a function).</summary>
		ErrorsAndWarnings = 2,
	}
}