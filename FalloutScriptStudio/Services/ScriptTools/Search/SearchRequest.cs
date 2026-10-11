namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// Describes one search run.
	/// </summary>
	public sealed class SearchRequest
	{
		/// <summary>The default limit of the number of reported lines.</summary>
		public const Int32 DefaultMaxHits = 20000;

		/// <summary>Gets what to look for.</summary>
		public required SearchMatcher Matcher { get; init; }

		/// <summary>Gets the texts searched from memory.</summary>
		public IReadOnlyList<SearchTextSource> Sources { get; init; } = Array.Empty<SearchTextSource>();

		/// <summary>Gets the folder to search, or <see langword="null"/> to search only <see cref="Sources"/>.</summary>
		public SearchFolder? Folder { get; init; }

		/// <summary>Gets the encoding of files that are not Unicode.</summary>
		public Encoding FallbackEncoding { get; init; } = TextEncodings.Ansi;

		/// <summary>Gets the largest number of matching lines that is reported.</summary>
		public Int32 MaxHits { get; init; } = DefaultMaxHits;
	}
}
