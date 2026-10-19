namespace FalloutScriptStudio.Documents
{
	/// <summary>The state of the parser of one document, as shown in the status bar.</summary>
	public enum ParserStatusKind
	{
		/// <summary>No status yet (the parser has not been asked to run for the document).</summary>
		None,

		/// <summary>The parse is scheduled and waits for the typing to pause.</summary>
		Waiting,

		/// <summary>The native parser is off: only the macros are collected.</summary>
		OffMacros,

		/// <summary>A parse is running.</summary>
		Running,

		/// <summary>The parse failed unexpectedly (the details are in the log file).</summary>
		FailedLog,

		/// <summary>The native parser was not available: the built-in parser was used.</summary>
		Builtin,

		/// <summary>The native parser failed (the details are in the parser log).</summary>
		FailedParserLog,

		/// <summary>The script has a syntax error.</summary>
		SyntaxError,

		/// <summary>The parse finished.</summary>
		Done,
	}
}