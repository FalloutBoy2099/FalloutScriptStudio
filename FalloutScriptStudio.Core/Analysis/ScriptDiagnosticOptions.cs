namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>What <see cref="ScriptDiagnostics"/> reports.</summary>
	public sealed record ScriptDiagnosticOptions
	{
		/// <summary>Gets a value indicating whether the rules that compare a call with the description of a function (<c>SSL2xxx</c>) run.</summary>
		public Boolean CheckFunctionCalls { get; init; } = true;

		/// <summary>
		/// Gets a value indicating whether a number given where the description states a <c>string</c> is reported (as information). Off by
		/// default: sfall converts numbers to text in many functions and real scripts pass message numbers, for example to <c>load_map</c>.
		/// </summary>
		public Boolean ReportNumbersForStrings { get; init; }

		/// <summary>Gets a value indicating whether a misspelled name gets the nearest known name as a quick fix.</summary>
		public Boolean SuggestNames { get; init; } = true;

		/// <summary>Gets the most diagnostics of one run; the analysis stops when it is reached (a script with thousands of errors needs no more).</summary>
		public Int32 MaximumCount { get; init; } = 500;
	}
}