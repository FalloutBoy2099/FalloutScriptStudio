namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>The conventions <see cref="DialogueQualityChecker"/> relies on; every default is the convention of the Fallout 2 scripts.</summary>
	public sealed record DialogueQualityOptions
	{
		/// <summary>The nodes that end the dialogue by convention: <c>Node998</c> (combat), <c>Node999</c> (exit) and <c>Node1000</c> (barter).</summary>
		public static readonly IReadOnlyList<String> DefaultExitNodeNames = new[] { "Node998", "Node999", "Node1000" };

		/// <summary>The functions that end the dialogue or hand it over to the game when a node calls them.</summary>
		public static readonly IReadOnlyList<String> DefaultExitFunctionNames = new[]
		{
			"end_dialogue",
			"gsay_end",
			"SayEnd",
			"SayQuit",
			"gdialog_barter",
			"gdialog_mod_barter",
			"attack_complex",
			"attack_setup",
		};

		/// <summary>Gets the number of options above which a node gets <see cref="DialogueQualityCode.TooManyOptions"/>.</summary>
		public Int32 MaxOptionsPerNode { get; init; } = 8;

		/// <summary>Gets the names of the nodes that end the dialogue by convention (case is ignored); a node that has no dialogue statement at all ends it too.</summary>
		public IReadOnlyList<String> ExitNodeNames { get; init; } = DefaultExitNodeNames;

		/// <summary>Gets the names of the functions that end the dialogue (case is ignored).</summary>
		public IReadOnlyList<String> ExitFunctionNames { get; init; } = DefaultExitFunctionNames;

		/// <summary>Gets a value indicating whether an option that jumps to an unknown name is an error. Turn it off when the headers of the script cannot be read.</summary>
		public Boolean ReportUndefinedTargets { get; init; } = true;
	}
}