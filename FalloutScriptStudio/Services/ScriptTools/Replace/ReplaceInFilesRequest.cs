namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Core.IO;
	using Search;

	/// <summary>Describes what a "Replace in Files" run looks for and replaces with.</summary>
	public sealed class ReplaceInFilesRequest
	{
		/// <summary>The most replacements a preview lists; a run that finds more is reported as truncated and replaces only the listed ones.</summary>
		public const Int32 DefaultMaxMatches = 50000;

		/// <summary>Gets what to look for (case, whole word, regular expression, time limit of the expression).</summary>
		public required SearchMatcher Matcher { get; init; }

		/// <summary>Gets the replacement; for a regular expression it may refer to groups (<c>$1</c>).</summary>
		public required String Replacement { get; init; }

		/// <summary>Gets the texts that are replaced from memory, before the folder is looked at (the current script, the open scripts).</summary>
		public IReadOnlyList<ReplaceTextSource> Sources { get; init; } = Array.Empty<ReplaceTextSource>();

		/// <summary>Gets the folder to run on, or <see langword="null"/> to run only on <see cref="Sources"/>.</summary>
		public SearchFolder? Folder { get; init; }

		/// <summary>Gets the open documents by full path: a file of the folder that is open is replaced in its document, not on the disk.</summary>
		public IReadOnlyDictionary<String, ReplaceTextSource> OpenDocuments { get; init; } = new Dictionary<String, ReplaceTextSource>();

		/// <summary>Gets the encoding of files that are not Unicode.</summary>
		public Encoding FallbackEncoding { get; init; } = TextEncodings.Ansi;

		/// <summary>Gets the most replacements the preview lists.</summary>
		public Int32 MaxMatches { get; init; } = DefaultMaxMatches;
	}
}