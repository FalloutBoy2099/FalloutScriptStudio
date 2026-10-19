namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Kind of a named entity found in an SSL script (legacy <c>NameType</c>).
	/// </summary>
	public enum SymbolKind
	{
		/// <summary>No symbol (lookup failed).</summary>
		None = 0,

		/// <summary>A <c>#define</c> macro.</summary>
		Macro,

		/// <summary>A variable local to a procedure (including procedure arguments).</summary>
		LocalVariable,

		/// <summary>A script-level (global, imported or exported) variable.</summary>
		GlobalVariable,

		/// <summary>A procedure.</summary>
		Procedure,
	}
}