namespace FalloutScriptStudio.Core.Help
{
	/// <summary>What a help topic describes.</summary>
	public enum HelpTopicKind
	{
		/// <summary>A built-in function or opcode (vanilla or sfall).</summary>
		Function = 0,

		/// <summary>A standard procedure handler such as <c>talk_p_proc</c>.</summary>
		Handler = 1,

		/// <summary>A constant the description files define (<c>HOOK_COMBATDAMAGE</c>).</summary>
		Define = 2,

		/// <summary>A function or macro of the user's <c>userfunctions.ini</c>.</summary>
		UserFunction = 3,

		/// <summary>A <c>#define</c> macro of the project or of its headers.</summary>
		Macro = 4,

		/// <summary>A procedure of the project.</summary>
		Procedure = 5,

		/// <summary>A variable of the project.</summary>
		Variable = 6,
	}
}