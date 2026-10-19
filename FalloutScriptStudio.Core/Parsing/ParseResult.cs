namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Result of <see cref="IScriptParser.ParseAsync"/>.
	/// </summary>
	/// <param name="ProgramInfo">The new snapshot; never <c>null</c>.</param>
	/// <param name="Outcome">How the data was obtained.</param>
	public sealed record ParseResult(ProgramInfo ProgramInfo, ParseOutcome Outcome)
	{
		/// <summary>Gets the status code returned by the native parser (0 = success), or -1 when it did not run.</summary>
		public Int32 NativeStatusCode { get; init; } = -1;

		/// <summary>Gets the raw log written by the native parser (the legacy <c>errors.txt</c>); empty when there is none.</summary>
		public String NativeLog { get; init; } = String.Empty;

		/// <summary>Gets the messages of the internal parser and of the macro collector.</summary>
		public IReadOnlyList<ParseDiagnostic> Diagnostics { get; init; } = Array.Empty<ParseDiagnostic>();

		/// <summary>Gets an explanation when the native parser was unavailable or failed; otherwise <c>null</c>.</summary>
		public String? Message { get; init; }

		/// <summary>
		/// Gets the text for the parser log pane: the native log when the native parser is enabled (followed by
		/// <see cref="Message"/> when it could not run), otherwise the messages of the internal parser.
		/// </summary>
		public String LogText { get; init; } = String.Empty;

		/// <summary>Gets a value indicating whether the data of <see cref="ProgramInfo"/> comes from the native parser.</summary>
		public Boolean UsedNativeParser => this.Outcome == ParseOutcome.NativeSucceeded;

		/// <summary>Gets a value indicating whether the script contains errors reported by the native parser.</summary>
		public Boolean HasParseError => this.ProgramInfo.HasParseError;
	}
}