namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Common contract of every named entity of a script: procedure, variable or macro
	/// (replaces the legacy <c>IParserInfo</c>).
	/// </summary>
	public interface IScriptSymbol
	{
		/// <summary>Gets the kind of the symbol.</summary>
		SymbolKind Kind { get; }

		/// <summary>Gets the name as written in the declaration (case preserved).</summary>
		String Name { get; }

		/// <summary>Gets a value indicating whether the symbol is imported from another script.</summary>
		Boolean IsImported { get; }

		/// <summary>Gets a value indicating whether the symbol is exported to other scripts.</summary>
		Boolean IsExported { get; }

		/// <summary>Gets a value indicating whether the symbol is a standard engine handler (for example <c>look_at_p_proc</c>).</summary>
		Boolean IsStandard { get; }

		/// <summary>Gets the places where the symbol is used. Empty when references are not tracked.</summary>
		IReadOnlyList<SourceReference> References { get; }

		/// <summary>Gets the declaration location, or <c>null</c> when unknown. Lines are 1-based.</summary>
		SourceReference? Declaration { get; }

		/// <summary>Gets the full textual description (tooltips).</summary>
		String FullDescription { get; }

		/// <summary>Gets the short textual description (autocomplete lists).</summary>
		String ShortDescription { get; }
	}
}
