namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Severity of a <see cref="ParseDiagnostic"/>.
	/// </summary>
	public enum ParseDiagnosticSeverity
	{
		/// <summary>Informational message (for example a missing include file).</summary>
		Info,

		/// <summary>The parser could not fully analyse a construct.</summary>
		Warning,

		/// <summary>The script is malformed.</summary>
		Error,
	}
}