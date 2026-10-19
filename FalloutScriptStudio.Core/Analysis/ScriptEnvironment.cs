namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using Parsing;

	/// <summary>
	/// Everything outside the text that the analysis of a script needs: the macros, the procedures and variables of the files it includes
	/// and the descriptions of the functions of the engine. It is built by <see cref="ScriptEnvironmentBuilder"/> in the application; the
	/// tests of the analysis create it directly.
	/// </summary>
	public sealed class ScriptEnvironment
	{
		/// <summary>Gets an environment with no macro, no header and no function description: only the text and what the compiler knows by itself.</summary>
		public static ScriptEnvironment Empty { get; } = new ScriptEnvironment();

		/// <summary>
		/// Gets the lookup of the macros of the headers (the macros defined in the text itself are found by the analysis); the name is
		/// case sensitive, as macros are. <see langword="null"/> when there is none.
		/// </summary>
		public Func<String, ScriptMacro?>? FindMacro { get; init; }

		/// <summary>Gets the procedures and variables the included files declare.</summary>
		public ExternalSymbols Symbols { get; init; } = ExternalSymbols.Empty;

		/// <summary>
		/// Gets a value indicating whether every file the script includes was read and none of them hides declarations behind macros. When
		/// it is <see langword="false"/> a name that nothing declares may still come from a file the analysis could not see, so the rules
		/// that report a missing name stay quiet.
		/// </summary>
		public Boolean IsComplete { get; init; } = true;

		/// <summary>Gets the descriptions of the functions of the engine and of sfall.</summary>
		public FunctionSignatureCatalog Functions { get; init; } = FunctionSignatureCatalog.Empty;
	}
}