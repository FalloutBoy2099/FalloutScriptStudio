namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Indexing;

	/// <summary>What the symbol list has to show.</summary>
	public sealed record SymbolPickerRequest
	{
		/// <summary>The most symbols the list shows for a filter.</summary>
		public const Int32 DefaultMaxResults = 200;

		/// <summary>Gets how the list is used.</summary>
		public SymbolPickerMode Mode { get; init; }

		/// <summary>Gets the index to search (for <see cref="SymbolPickerMode.Search"/>).</summary>
		public IProjectSymbolIndex? Index { get; init; }

		/// <summary>Gets the places to choose from, nearest first (for <see cref="SymbolPickerMode.Choose"/>).</summary>
		public IReadOnlyList<IndexedSymbol> Candidates { get; init; } = Array.Empty<IndexedSymbol>();

		/// <summary>Gets the name that has several definitions (for <see cref="SymbolPickerMode.Choose"/>).</summary>
		public String Name { get; init; } = String.Empty;

		/// <summary>Gets the folder that file paths are shown relative to, or <see langword="null"/> to show file names.</summary>
		public String? RootFolder { get; init; }

		/// <summary>Gets the most symbols to show for a filter.</summary>
		public Int32 MaxResults { get; init; } = DefaultMaxResults;
	}
}
