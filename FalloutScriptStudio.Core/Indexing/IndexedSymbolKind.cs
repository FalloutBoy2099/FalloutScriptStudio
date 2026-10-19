namespace FalloutScriptStudio.Core.Indexing
{
	using System;

	/// <summary>The kinds of symbol of the project index. As a filter the values combine.</summary>
	[Flags]
	public enum IndexedSymbolKind
	{
		/// <summary>No kind.</summary>
		None = 0,

		/// <summary>A procedure.</summary>
		Procedure = 1,

		/// <summary>A <c>#define</c> macro.</summary>
		Macro = 2,

		/// <summary>A script level variable (<c>variable</c>, <c>import variable</c> or <c>export variable</c>).</summary>
		Variable = 4,

		/// <summary>Every kind.</summary>
		All = Procedure | Macro | Variable,
	}
}