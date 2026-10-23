namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Generic;

	/// <summary>What the analysis of the global variables leaves out and how much it reports.</summary>
	public sealed record GlobalAnalysisOptions
	{
		/// <summary>The default most findings that are reported.</summary>
		public const Int32 DefaultMaxFindings = 2000;

		/// <summary>Gets the variables the user does not want findings for: a name, a macro, a number, or a prefix ending with <c>*</c> (<c>GVAR_QUEST_*</c>); the case is ignored.</summary>
		public IReadOnlyList<String> Ignored { get; init; } = Array.Empty<String>();

		/// <summary>Gets a value indicating whether the variables the engine sets itself (see <see cref="GlobalEngineVariables"/>) are not reported as read but never written.</summary>
		public Boolean SkipEngineVariables { get; init; } = true;

		/// <summary>Gets the most findings that are reported.</summary>
		public Int32 MaxFindings { get; init; } = DefaultMaxFindings;
	}
}