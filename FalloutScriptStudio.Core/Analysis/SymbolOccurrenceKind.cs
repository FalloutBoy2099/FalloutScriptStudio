namespace FalloutScriptStudio.Core.Analysis
{
	/// <summary>What an occurrence of a name in a script is.</summary>
	public enum SymbolOccurrenceKind
	{
		/// <summary>The name after <c>procedure</c> (a forward declaration or the head of the body) or after <c>#define</c>.</summary>
		Declaration = 0,

		/// <summary>The name used in code or in the body of a macro.</summary>
		Reference = 1,

		/// <summary>A string literal that holds exactly the name.</summary>
		StringLiteral = 2,
	}
}
