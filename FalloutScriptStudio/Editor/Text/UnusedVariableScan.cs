namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Collections.Generic;
	using Core.Analysis;
	using Core.Parsing;

	/// <summary>Finds the unused variables of a script with what the parser knows about the macros of its headers.</summary>
	public static class UnusedVariableScan
	{
		/// <summary>Finds the unused variables of a script text.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="info">The parse result of the script, whose macros may use variables (a variable named in such a macro is not reported); can be <see langword="null"/>.</param>
		/// <param name="includeParameters">Whether the arguments of procedures are reported.</param>
		/// <param name="includeGlobals">Whether the script level variables are reported.</param>
		/// <param name="includeWriteOnly">Whether variables that are only assigned are reported.</param>
		/// <returns>The variables in text order.</returns>
		public static IReadOnlyList<UnusedVariable> Find(String text, ProgramInfo? info, Boolean includeParameters, Boolean includeGlobals, Boolean includeWriteOnly)
		{
			ArgumentNullException.ThrowIfNull(text);

			Lazy<HashSet<String>> words = new(() =>
			{
				HashSet<String> set = new(StringComparer.OrdinalIgnoreCase);
				if (info is not null)
				{
					MacroWords.Collect(info.ScriptMacros.Values, set);
					MacroWords.Collect(info.GlobalMacros.Macros.Values, set);
				}

				return set;
			});
			return UnusedVariableFinder.Find(
				text,
				new UnusedVariableOptions
				{
					IncludeParameters = includeParameters,
					IncludeGlobals = includeGlobals,
					IncludeWriteOnly = includeWriteOnly,
					IsMacroWord = name => words.Value.Contains(name),
				});
		}
	}
}