namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;

	/// <summary>A search for the places where a name is written in the files of the project.</summary>
	/// <param name="Name">The name.</param>
	public sealed record SymbolOccurrenceRequest(String Name)
	{
		/// <summary>Gets a value indicating whether the case matters (macros) or not (procedures and variables).</summary>
		public Boolean CaseSensitive { get; init; }

		/// <summary>Gets a value indicating whether a string literal that is exactly the name counts.</summary>
		public Boolean IncludeStrings { get; init; }

		/// <summary>Gets the files to search, or <see langword="null"/> for every indexed file.</summary>
		public IReadOnlyCollection<String>? Files { get; init; }

		/// <summary>Gets the texts of the open documents by full path; they are used instead of the files on disk.</summary>
		public IReadOnlyDictionary<String, String>? OpenTexts { get; init; }
	}
}