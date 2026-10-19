namespace FalloutScriptStudio.Core.Analysis
{
	/// <summary>How serious a <see cref="ScriptDiagnostic"/> is. The values are ordered: a higher value is more serious.</summary>
	public enum ScriptDiagnosticSeverity
	{
		/// <summary>A remark that does not stop the script from compiling (for example a deprecated function).</summary>
		Info = 0,

		/// <summary>Probably a mistake, but the compiler may accept it (for example a call that does not match the description of a function).</summary>
		Warning = 1,

		/// <summary>The compiler (sslc) rejects the script because of it.</summary>
		Error = 2,
	}
}