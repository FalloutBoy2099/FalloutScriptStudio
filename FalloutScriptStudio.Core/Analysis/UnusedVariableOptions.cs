namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>What <see cref="UnusedVariableFinder"/> reports.</summary>
	public sealed record UnusedVariableOptions
	{
		/// <summary>Gets a value indicating whether the arguments of procedures are reported (an argument often cannot be removed: the callers pass it).</summary>
		public Boolean IncludeParameters { get; init; }

		/// <summary>Gets a value indicating whether the script level variables (not the imported and exported ones) are reported.</summary>
		public Boolean IncludeGlobals { get; init; } = true;

		/// <summary>Gets a value indicating whether variables that are assigned but never read are reported too.</summary>
		public Boolean IncludeWriteOnly { get; init; } = true;

		/// <summary>
		/// Gets the test that tells whether a macro of the included headers uses a word. A variable whose name is such a word may be used by
		/// an expansion of that macro and is not reported. It is only asked for the names of variables that look unused.
		/// </summary>
		public Func<String, Boolean>? IsMacroWord { get; init; }
	}
}