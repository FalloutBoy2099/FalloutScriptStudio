namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// How a parse operation obtained its data.
	/// </summary>
	public enum ParseOutcome
	{
		/// <summary>The native parser succeeded; the data comes from it.</summary>
		NativeSucceeded,

		/// <summary>The native parser reported errors in the script; the previous data was kept and updated by the internal parser.</summary>
		NativeReportedErrors,

		/// <summary>The native parser is not available (missing or not loadable <c>parser.dll</c>); the internal parser was used.</summary>
		NativeUnavailable,

		/// <summary>The native parser failed unexpectedly; the internal parser was used.</summary>
		NativeFailed,

		/// <summary>The native parser is disabled by the options, or the script has no file path; only the internal parser and the macros were used.</summary>
		NativeDisabled,
	}
}
