namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Kind of an <see cref="AutosuggestItem"/>.
	/// </summary>
	public enum AutosuggestKind
	{
		/// <summary>A built-in opcode (engine or sfall function).</summary>
		Opcode,

		/// <summary>A procedure of the script.</summary>
		Procedure,

		/// <summary>A variable.</summary>
		Variable,

		/// <summary>A <c>#define</c> macro.</summary>
		Macro,
	}
}
