namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Compares the string dictionaries with the glossary: when the English text of a key contains a glossary term, the same key in every
	/// other language must contain one of the accepted forms of the canonical translation. The rules are the ones printed under
	/// <c>matching</c> in <c>Glossary.json</c>.
	/// </summary>
	internal static class GlossaryChecker
	{
		/// <summary>The code of the language the glossary terms are written in.</summary>
		public const String English = "en";

		/// <summary>The term name used for the findings that do not belong to one term.</summary>
		public const String NoTerm = "-";

		private static readonly Regex _placeholder = new Regex(@"\{\d+(?::[^}]*)?\}", RegexOptions.Compiled);
		private static readonly Regex _fileName = new Regex(@"[\w\-]*\.[A-Za-z0-9]{1,4}\b", RegexOptions.Compiled);
		private static readonly Regex _singleQuoted = new Regex(@"'[^'\s]{1,40}'", RegexOptions.Compiled);
		private static readonly Regex _doubleQuoted = new Regex("\"[^\"\\s]{1,40}\"", RegexOptions.Compiled);

		/// <summary>Prepares a text for the comparison: removes placeholders, ignored phrases, file names and access key underscores, and lower-cases it.</summary>
		/// <param name="text">The text.</param>
		/// <param name="ignore">The phrases to remove.</param>
		/// <returns>The normalized text.</returns>
		public static String Normalize(String text, IEnumerable<String> ignore)
		{
			String result = _placeholder.Replace(text, " ");
			foreach (String phrase in ignore)
			{
				result = Regex.Replace(result, Regex.Escape(phrase), " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			}

			result = _fileName.Replace(result, " ");
			return result.Replace("_", String.Empty, StringComparison.Ordinal).ToLowerInvariant();
		}

		/// <summary>Prepares an English text: like <see cref="Normalize"/>, after the quoted literals (such as <c>'Node'</c>) were removed.</summary>
		/// <param name="text">The English text.</param>
		/// <param name="ignore">The phrases to remove.</param>
		/// <returns>The normalized text.</returns>
		public static String NormalizeEnglish(String text, IEnumerable<String> ignore)
		{
			return Normalize(_doubleQuoted.Replace(_singleQuoted.Replace(text, " "), " "), ignore);
		}

		/// <summary>Builds the whole-word pattern of the English wordings of a term.</summary>
		/// <param name="variants">The wordings.</param>
		/// <returns>The pattern, to be applied to a normalized English text.</returns>
		public static Regex EnglishPattern(IEnumerable<String> variants)
		{
			String alternatives = String.Join("|", variants.OrderByDescending(variant => variant.Length).Select(variant => Regex.Escape(variant.ToLowerInvariant())));
			return new Regex(@"(?<![\w#])(?:" + alternatives + @")(?![\w])", RegexOptions.CultureInvariant);
		}

		/// <summary>Tells whether a normalized text contains a glossary fragment (<c>^</c> prefix: word start, <c>$</c> suffix: word end).</summary>
		/// <param name="normalized">The normalized text.</param>
		/// <param name="fragment">The fragment.</param>
		/// <returns><see langword="true"/> when the text contains the fragment.</returns>
		public static Boolean Contains(String normalized, String fragment)
		{
			Boolean atStart = fragment.StartsWith('^');
			Boolean atEnd = fragment.EndsWith('$') && fragment.Length > 1;
			String core = fragment[(atStart ? 1 : 0)..(fragment.Length - (atEnd ? 1 : 0))];
			if (!atStart && !atEnd)
			{
				return normalized.Contains(core, StringComparison.Ordinal);
			}

			String pattern = (atStart ? @"(?<!\p{L})" : String.Empty) + Regex.Escape(core) + (atEnd ? @"(?!\p{L})" : String.Empty);
			return Regex.IsMatch(normalized, pattern, RegexOptions.CultureInvariant);
		}

		/// <summary>Finds every text that does not follow the glossary.</summary>
		/// <param name="glossary">The glossary.</param>
		/// <param name="texts">The texts by language code, then by key; the English texts are under <see cref="English"/>.</param>
		/// <returns>The findings, ordered by key and language.</returns>
		public static IReadOnlyList<Violation> Check(Glossary glossary, IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>> texts)
		{
			List<Violation> violations = new List<Violation>();
			IReadOnlyDictionary<String, String> english = texts[English];
			Dictionary<String, String> englishNormalized = english.ToDictionary(entry => entry.Key, entry => NormalizeEnglish(entry.Value, glossary.Ignore), StringComparer.Ordinal);
			Dictionary<String, Dictionary<String, String>> normalized = texts.Where(entry => entry.Key != English)
				.ToDictionary(entry => entry.Key, entry => entry.Value.ToDictionary(text => text.Key, text => Normalize(text.Value, glossary.Ignore), StringComparer.Ordinal), StringComparer.Ordinal);

			foreach (Glossary.Term term in glossary.Terms)
			{
				Regex pattern = EnglishPattern(term.En);
				foreach (KeyValuePair<String, String> entry in englishNormalized)
				{
					if (!pattern.IsMatch(entry.Value))
					{
						continue;
					}

					foreach (KeyValuePair<String, Glossary.LanguageEntry> language in term.Languages)
					{
						if (!normalized.TryGetValue(language.Key, out Dictionary<String, String>? translated) || !translated.TryGetValue(entry.Key, out String? text))
						{
							continue;
						}

						String original = texts[language.Key][entry.Key];
						if (!language.Value.Accept.Any(fragment => Contains(text, fragment)))
						{
							violations.Add(new Violation(entry.Key, language.Key, term.Id, "missing", $"expected \"{language.Value.Canonical}\" (one of: {String.Join(", ", language.Value.Accept)}) in: {original}"));
						}

						foreach (String forbidden in language.Value.Forbid.Where(fragment => Contains(text, fragment)))
						{
							violations.Add(new Violation(entry.Key, language.Key, term.Id, "forbidden", $"uses \"{forbidden}\" instead of \"{language.Value.Canonical}\" in: {original}"));
						}
					}
				}
			}

			violations.AddRange(CheckForbiddenEverywhere(glossary, normalized, texts));
			violations.AddRange(CheckEnglishWordings(glossary, texts[English]));
			return violations.OrderBy(violation => violation.Key, StringComparer.Ordinal).ThenBy(violation => violation.Language, StringComparer.Ordinal).ThenBy(violation => violation.Term, StringComparer.Ordinal).ToList();
		}

		/// <summary>Applies the allow-list.</summary>
		/// <param name="violations">The findings.</param>
		/// <param name="exceptions">The allow-list.</param>
		/// <param name="stale">Receives the entries that silenced nothing.</param>
		/// <returns>The findings that are not excused.</returns>
		public static IReadOnlyList<Violation> Apply(IEnumerable<Violation> violations, IEnumerable<GlossaryExceptions.Entry> exceptions, out IReadOnlyList<GlossaryExceptions.Entry> stale)
		{
			GlossaryExceptions.Entry[] entries = exceptions.ToArray();
			HashSet<GlossaryExceptions.Entry> used = new HashSet<GlossaryExceptions.Entry>();
			List<Violation> remaining = new List<Violation>();
			foreach (Violation violation in violations)
			{
				GlossaryExceptions.Entry[] matching = entries.Where(entry => Matches(entry, violation)).ToArray();
				if (matching.Length == 0)
				{
					remaining.Add(violation);
				}

				used.UnionWith(matching);
			}

			stale = entries.Where(entry => !used.Contains(entry)).ToList();
			return remaining;
		}

		private static Boolean Matches(GlossaryExceptions.Entry entry, Violation violation)
		{
			return entry.Key == violation.Key
				&& (entry.Language == "*" || entry.Language == violation.Language)
				&& (entry.Term == "*" || entry.Term == violation.Term);
		}

		private static IEnumerable<Violation> CheckForbiddenEverywhere(Glossary glossary, Dictionary<String, Dictionary<String, String>> normalized, IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>> texts)
		{
			foreach (KeyValuePair<String, Glossary.ForbiddenText[]> language in glossary.ForbiddenEverywhere)
			{
				if (!normalized.TryGetValue(language.Key, out Dictionary<String, String>? translated))
				{
					continue;
				}

				foreach (KeyValuePair<String, String> entry in translated)
				{
					foreach (Glossary.ForbiddenText forbidden in language.Value)
					{
						String? found = null;
						if (forbidden.Text is not null && Contains(entry.Value, forbidden.Text))
						{
							found = forbidden.Text;
						}
						else if (forbidden.Chars is not null)
						{
							Int32 index = entry.Value.IndexOfAny(forbidden.Chars.ToCharArray());
							found = index >= 0 ? entry.Value[index].ToString() : null;
						}

						if (found is not null)
						{
							yield return new Violation(entry.Key, language.Key, NoTerm, "forbidden", $"uses \"{found}\" ({forbidden.Reason}) in: {texts[language.Key][entry.Key]}");
						}
					}
				}
			}
		}

		private static IEnumerable<Violation> CheckEnglishWordings(Glossary glossary, IReadOnlyDictionary<String, String> english)
		{
			foreach (Glossary.Term term in glossary.Terms.Where(term => term.EnForbid.Length > 0))
			{
				Regex pattern = EnglishPattern(term.EnForbid);
				foreach (KeyValuePair<String, String> entry in english)
				{
					Match match = pattern.Match(Normalize(entry.Value, glossary.Ignore));
					if (match.Success)
					{
						yield return new Violation(entry.Key, English, term.Id, "english wording", $"uses \"{match.Value}\", which the glossary forbids for \"{term.English}\", in: {entry.Value}");
					}
				}
			}
		}

		/// <summary>One text that does not follow the glossary.</summary>
		/// <param name="Key">The localization key.</param>
		/// <param name="Language">The language code.</param>
		/// <param name="Term">The identifier of the term, or <see cref="NoTerm"/>.</param>
		/// <param name="Kind">The kind of finding: <c>missing</c>, <c>forbidden</c> or <c>english wording</c>.</param>
		/// <param name="Detail">What is expected or wrong.</param>
		internal sealed record Violation(String Key, String Language, String Term, String Kind, String Detail)
		{
			/// <inheritdoc />
			public override String ToString()
			{
				return $"{this.Key} [{this.Language}] {this.Term}: {this.Kind}: {this.Detail.Replace("\r", String.Empty, StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}";
			}
		}
	}
}
