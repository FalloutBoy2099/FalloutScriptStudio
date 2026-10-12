namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.IO;
	using System.Text.Json;
	using FalloutScriptStudio.TestSupport;

	/// <summary>
	/// The allow-list <c>GlossaryExceptions.json</c>: the few texts in which a translation legitimately does not use the glossary term
	/// (idiomatic phrase, term not translated there, grammatical restructuring...). Every entry names one key, one language and one term
	/// (or <c>*</c>) and carries a one-line justification. An entry that silences nothing is reported as stale.
	/// </summary>
	/// <param name="About">What the file is.</param>
	/// <param name="Exceptions">The exceptions.</param>
	internal sealed record GlossaryExceptions(String About, GlossaryExceptions.Entry[] Exceptions)
	{
		private static readonly JsonSerializerOptions _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		/// <summary>Gets the path of <c>GlossaryExceptions.json</c>.</summary>
		public static String Path => System.IO.Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio.Tests", "Localization", "GlossaryExceptions.json");

		/// <summary>Loads the allow-list of the repository.</summary>
		/// <returns>The allow-list.</returns>
		public static GlossaryExceptions Load()
		{
			return JsonSerializer.Deserialize<GlossaryExceptions>(File.ReadAllText(Path), _options) ?? throw new InvalidDataException("The exception list is empty.");
		}

		/// <summary>One exception.</summary>
		/// <param name="Key">The localization key (<c>Str.Area.Key</c>).</param>
		/// <param name="Language">The language code, or <c>*</c> for every language.</param>
		/// <param name="Term">The identifier of the glossary term, or <c>*</c> for every term.</param>
		/// <param name="Reason">The one-line justification.</param>
		internal sealed record Entry(String Key, String Language, String Term, String Reason);
	}
}
