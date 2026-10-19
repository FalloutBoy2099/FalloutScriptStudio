namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Localization;

	/// <summary>Writes <c>docs\GLOSSARY.md</c> from <c>Glossary.json</c>, so that the document never drifts from the data the tests check.</summary>
	internal static class GlossaryMarkdown
	{
		/// <summary>Renders the document (with CRLF line breaks).</summary>
		/// <param name="glossary">The glossary.</param>
		/// <returns>The Markdown text.</returns>
		public static String Render(Glossary glossary)
		{
			List<String> languages = Loc.Languages.Select(language => language.Code).Where(code => code != GlossaryChecker.English).OrderBy(code => code, StringComparer.Ordinal).ToList();
			StringBuilder text = new StringBuilder();
			text.AppendLine("# Glossary of the user interface texts");
			text.AppendLine();
			text.AppendLine("The application is translated into 11 languages. The same domain term must be translated **the same way in every window** of a language: this document lists the");
			text.AppendLine("canonical translation of the " + glossary.Terms.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " terms that matter. **Every new or changed string must use these terms** (see [CONVENTIONS.md](CONVENTIONS.md) and the guide");
			text.AppendLine("\"A localized string\" in [HOW_TO.md](HOW_TO.md)).");
			text.AppendLine();
			text.AppendLine("This file is **generated** from [`FalloutScriptStudio/Localization/Glossary.json`](../FalloutScriptStudio/Localization/Glossary.json) by `GlossaryTests`; do not edit it by hand.");
			text.AppendLine("To regenerate it after changing the JSON run the tests with the environment variable `FSS_WRITE_GLOSSARY=1`.");
			text.AppendLine();
			text.AppendLine("## How the glossary is enforced");
			text.AppendLine();
			text.AppendLine("`GlossaryTests` (project `FalloutScriptStudio.Tests`, no UI) read every string dictionary and, for every key whose **English** text contains a term below as a whole word,");
			text.AppendLine("check that the same key of each other language contains one of the *accepted forms* of the canonical translation (inflected forms, plurals and abbreviations are listed in the JSON).");
			text.AppendLine("The check also fails when a text uses a *forbidden synonym* of a term, when the English texts use a forbidden wording, and when a language uses a text that is excluded for it");
			text.AppendLine("(for example European Portuguese words: the Portuguese texts are Brazilian). The findings are reported all at once with the key, the language and the expected term.");
			text.AppendLine();
			text.AppendLine("* **Fix the translation first.** The allow-list `FalloutScriptStudio.Tests/Localization/GlossaryExceptions.json` is only for texts in which the term is legitimately not used");
			text.AppendLine("  (idiomatic phrase, term not translated there, grammatical restructuring). Each entry names a key, a language and a term and has a one-line justification; an entry that");
			text.AppendLine("  silences nothing is reported as stale and the list has a hard size limit.");
			text.AppendLine("* **A new term** goes into `Glossary.json` (English wordings, canonical translation and accepted forms per language, notes). Check the existing texts with the tests before you commit.");
			text.AppendLine("* **Matching rules** (also stored under `matching` in the JSON):");
			foreach (String rule in glossary.Matching)
			{
				text.AppendLine("  * " + rule);
			}

			text.AppendLine();
			text.AppendLine("## Terms");
			text.AppendLine();
			text.AppendLine("The tables show the canonical translation. Where the canonical form is the one of an inflected language (cs, pl, ru: nominative singular; de: infinitive or nominative; sv: indefinite form),");
			text.AppendLine("the other cases and the plural are accepted by the test and must be used with the right grammar. Two forms separated by a slash are two contexts of the term (for example noun and verb).");
			foreach (Glossary.TermGroup group in glossary.Groups)
			{
				text.AppendLine();
				text.AppendLine("### " + group.Name);
				text.AppendLine();
				text.AppendLine("| English | " + String.Join(" | ", languages) + " |");
				text.AppendLine("| --- |" + String.Concat(languages.Select(_ => " --- |")));
				List<Glossary.Term> terms = group.Terms.Select(id => glossary.Terms.Single(term => term.Id == id)).ToList();
				foreach (Glossary.Term term in terms)
				{
					text.AppendLine("| **" + term.English + "** | " + String.Join(" | ", languages.Select(code => Escape(term.Languages[code].Canonical))) + " |");
				}

				text.AppendLine();
				text.AppendLine("Usage notes:");
				text.AppendLine();
				foreach (Glossary.Term term in terms)
				{
					text.AppendLine("* **" + term.English + "**: " + term.Notes + " English wordings that trigger the check: " + String.Join(", ", term.En.Select(Code)) + "." + Forbidden(term, languages) + EnglishForbidden(term));
				}
			}

			text.AppendLine();
			text.AppendLine("## Excluded in a whole language");
			text.AppendLine();
			foreach (KeyValuePair<String, Glossary.ForbiddenText[]> language in glossary.ForbiddenEverywhere.OrderBy(entry => entry.Key, StringComparer.Ordinal))
			{
				text.AppendLine("* **" + language.Key + "**: " + String.Join("; ", language.Value.Select(forbidden => (forbidden.Text is not null ? Code(forbidden.Text) : "characters " + Code(forbidden.Chars ?? String.Empty)) + " (" + forbidden.Reason + ")")) + ".");
			}

			return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal).TrimEnd('\r', '\n');
		}

		private static String Forbidden(Glossary.Term term, IEnumerable<String> languages)
		{
			List<String> entries = languages.Where(code => term.Languages[code].Forbid.Length > 0)
				.Select(code => code + " " + String.Join(", ", term.Languages[code].Forbid.Select(Code)))
				.ToList();
			return entries.Count == 0 ? String.Empty : " Do not use: " + String.Join("; ", entries) + ".";
		}

		private static String EnglishForbidden(Glossary.Term term)
		{
			return term.EnForbid.Length == 0 ? String.Empty : " Not allowed in English: " + String.Join(", ", term.EnForbid.Select(Code)) + ".";
		}

		private static String Code(String text)
		{
			return "`" + text + "`";
		}

		private static String Escape(String text)
		{
			return text.Replace("|", "\\|", StringComparison.Ordinal);
		}
	}
}