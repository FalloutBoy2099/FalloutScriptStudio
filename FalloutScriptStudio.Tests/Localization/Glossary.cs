namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.Json;
	using FalloutScriptStudio.TestSupport;

	/// <summary>
	/// The common glossary of the user interface texts (<c>FalloutScriptStudio\Localization\Glossary.json</c>): for every domain term the
	/// English wordings that trigger it and, per language, the canonical translation with the accepted and the forbidden fragments.
	/// </summary>
	/// <param name="Version">The version of the file format.</param>
	/// <param name="About">What the file is.</param>
	/// <param name="Matching">How the texts are compared (also printed in the generated document).</param>
	/// <param name="Ignore">Phrases (product names, key names) that are removed from every text before it is compared.</param>
	/// <param name="ForbiddenEverywhere">Fragments that must not occur anywhere in the texts of a language.</param>
	/// <param name="Groups">The groups of the generated document.</param>
	/// <param name="Terms">The terms.</param>
	internal sealed record Glossary(
		Int32 Version,
		String About,
		String[] Matching,
		String[] Ignore,
		Dictionary<String, Glossary.ForbiddenText[]> ForbiddenEverywhere,
		Glossary.TermGroup[] Groups,
		Glossary.Term[] Terms)
	{
		private static readonly JsonSerializerOptions _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		/// <summary>Gets the path of <c>Glossary.json</c>.</summary>
		public static String Path => System.IO.Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Localization", "Glossary.json");

		/// <summary>Loads the glossary of the repository.</summary>
		/// <returns>The glossary.</returns>
		public static Glossary Load()
		{
			return Parse(File.ReadAllText(Path));
		}

		/// <summary>Reads a glossary from its JSON text.</summary>
		/// <param name="json">The JSON text.</param>
		/// <returns>The glossary.</returns>
		public static Glossary Parse(String json)
		{
			return JsonSerializer.Deserialize<Glossary>(json, _options) ?? throw new InvalidDataException("The glossary is empty.");
		}

		/// <summary>The translation of a term into one language.</summary>
		/// <param name="Canonical">The canonical translation, as shown in the document.</param>
		/// <param name="Accept">Lower case fragments, one of which must occur in a translated text (inflected forms, abbreviations).</param>
		/// <param name="Forbid">Lower case fragments of synonyms that must not be used where the term applies.</param>
		internal sealed record LanguageEntry(String Canonical, String[] Accept, String[] Forbid);

		/// <summary>A domain term.</summary>
		/// <param name="Id">The identifier used by the exception list.</param>
		/// <param name="English">The English term as shown in the document.</param>
		/// <param name="Group">The group of the document.</param>
		/// <param name="En">The English wordings (whole words, any case) that make the term apply to a text.</param>
		/// <param name="EnForbid">English wordings that must not be used in the English texts.</param>
		/// <param name="Notes">The context of the term and what not to use.</param>
		/// <param name="Languages">The translations by language code.</param>
		internal sealed record Term(String Id, String English, String Group, String[] En, String[] EnForbid, String Notes, Dictionary<String, LanguageEntry> Languages);

		/// <summary>A group of terms of the generated document.</summary>
		/// <param name="Name">The heading.</param>
		/// <param name="Terms">The identifiers of the terms.</param>
		internal sealed record TermGroup(String Name, String[] Terms);

		/// <summary>A fragment (or a set of characters) that a language must never use.</summary>
		/// <param name="Text">A lower case fragment, or <see langword="null"/>.</param>
		/// <param name="Chars">Characters of which none may occur, or <see langword="null"/>.</param>
		/// <param name="Reason">Why.</param>
		internal sealed record ForbiddenText(String? Text, String? Chars, String Reason);
	}
}