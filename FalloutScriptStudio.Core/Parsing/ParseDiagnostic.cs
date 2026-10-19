namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// A message produced while parsing (replaces the legacy <c>intParserPrint</c> calls).
	/// </summary>
	/// <param name="Severity">Severity of the message.</param>
	/// <param name="Source">Component that produced the message, for example <c>Internal Parser</c>.</param>
	/// <param name="Line">1-based line the message refers to, or -1 when it is not tied to a line.</param>
	/// <param name="Message">Message text.</param>
	public sealed record ParseDiagnostic(ParseDiagnosticSeverity Severity, String Source, Int32 Line, String Message)
	{
		/// <summary>
		/// Formats the message like the legacy parser log, for example
		/// <c>[Error] &lt;Internal Parser&gt; Line: 12 : message</c>.
		/// </summary>
		/// <returns>The formatted log line (without a line terminator).</returns>
		public override String ToString()
		{
			String line = this.Line > 0 ? " Line: " + this.Line + " :" : String.Empty;
			return "[" + this.Severity + "] <" + this.Source + ">" + line + " " + this.Message;
		}
	}
}