namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// Guards the common glossary: <c>Localization\Glossary.json</c> is well formed, every translated text uses the canonical term of the
	/// terms its English text contains (all findings are reported at once), the allow-list stays small and justified, the English texts avoid
	/// the forbidden wordings, and <c>docs\GLOSSARY.md</c> is exactly what the generator writes from the JSON.
	/// </summary>
	public sealed class GlossaryTests
	{
		/// <summary>The maximum number of entries of the allow-list: fix the translation instead of excusing it.</summary>
		private const Int32 MaxExceptions = 12;

		private static readonly Lazy<IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>>> _texts = new Lazy<IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>>>(LoadTexts);

		private static String GlossaryDocument => Path.Combine(TestFiles.RepositoryRoot, "docs", "GLOSSARY.md");

		[Fact]
		public void TheGlossaryIsWellFormed()
		{
			Glossary glossary = Glossary.Load();
			List<String> problems = new List<String>();
			String[] languages = Loc.Languages.Select(language => language.Code).Where(code => code != GlossaryChecker.English).OrderBy(code => code, StringComparer.Ordinal).ToArray();

			if (glossary.Version != 1)
			{
				problems.Add("unknown glossary version " + glossary.Version);
			}

			if (glossary.Terms.Length is < 40 or > 70)
			{
				problems.Add($"the glossary should hold the 40 to 70 terms that matter, it has {glossary.Terms.Length}");
			}

			foreach (IGrouping<String, Glossary.Term> duplicate in glossary.Terms.GroupBy(term => term.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
			{
				problems.Add("duplicate term id " + duplicate.Key);
			}

			String[] grouped = glossary.Groups.SelectMany(group => group.Terms).ToArray();
			problems.AddRange(glossary.Terms.Select(term => term.Id).Except(grouped).Select(id => $"term {id} is in no group"));
			problems.AddRange(grouped.Except(glossary.Terms.Select(term => term.Id)).Select(id => $"group lists the unknown term {id}"));
			problems.AddRange(grouped.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => $"term {group.Key} is in several groups"));

			foreach (Glossary.Term term in glossary.Terms)
			{
				if (term.En.Length == 0 || term.En.Any(String.IsNullOrWhiteSpace))
				{
					problems.Add($"{term.Id}: the English wordings are empty");
				}

				if (String.IsNullOrWhiteSpace(term.Notes) || String.IsNullOrWhiteSpace(term.English))
				{
					problems.Add($"{term.Id}: the name or the notes are empty");
				}

				problems.AddRange(languages.Except(term.Languages.Keys).Select(code => $"{term.Id}: no translation for {code}"));
				problems.AddRange(term.Languages.Keys.Except(languages).Select(code => $"{term.Id}: unknown language {code}"));
				foreach (KeyValuePair<String, Glossary.LanguageEntry> language in term.Languages)
				{
					Glossary.LanguageEntry entry = language.Value;
					if (String.IsNullOrWhiteSpace(entry.Canonical) || entry.Accept.Length == 0 || entry.Accept.Any(String.IsNullOrEmpty))
					{
						problems.Add($"{term.Id}/{language.Key}: the canonical translation or the accepted forms are empty");
					}

					foreach (String fragment in entry.Accept.Concat(entry.Forbid))
					{
						if (fragment != fragment.ToLowerInvariant())
						{
							problems.Add($"{term.Id}/{language.Key}: the fragment \"{fragment}\" must be lower case");
						}
					}

					if (entry.Accept.Intersect(entry.Forbid).Any())
					{
						problems.Add($"{term.Id}/{language.Key}: a fragment is both accepted and forbidden");
					}
				}
			}

			Findings.AssertNone(problems);
		}

		[Fact]
		public void EveryTextUsesTheGlossaryTermsOfItsEnglishText()
		{
			Glossary glossary = Glossary.Load();
			GlossaryExceptions exceptions = GlossaryExceptions.Load();

			IReadOnlyList<GlossaryChecker.Violation> remaining = GlossaryChecker.Apply(GlossaryChecker.Check(glossary, _texts.Value), exceptions.Exceptions, out IReadOnlyList<GlossaryExceptions.Entry> _);

			Findings.AssertNone(remaining.Select(violation => violation.ToString()));
		}

		[Fact]
		public void TheGlossaryFindsRealTermsInTheEnglishTexts()
		{
			Glossary glossary = Glossary.Load();
			IReadOnlyDictionary<String, String> english = _texts.Value[GlossaryChecker.English];
			Dictionary<String, String> normalized = english.ToDictionary(entry => entry.Key, entry => GlossaryChecker.NormalizeEnglish(entry.Value, glossary.Ignore), StringComparer.Ordinal);

			// A term that no English text contains is dead data (it would never check anything).
			List<String> unused = glossary.Terms
				.Where(term => !normalized.Values.Any(GlossaryChecker.EnglishPattern(term.En).IsMatch))
				.Select(term => term.Id)
				.ToList();

			Findings.AssertNone(unused.Select(id => $"no English text contains the term {id}"));
		}

		[Fact]
		public void TheAllowListIsSmallAndEveryEntryIsJustifiedAndStillNeeded()
		{
			Glossary glossary = Glossary.Load();
			GlossaryExceptions exceptions = GlossaryExceptions.Load();
			IReadOnlyDictionary<String, String> english = _texts.Value[GlossaryChecker.English];
			String[] languages = Loc.Languages.Select(language => language.Code).Append("*").ToArray();
			List<String> problems = new List<String>();

			if (exceptions.Exceptions.Length > MaxExceptions)
			{
				problems.Add($"the allow-list has {exceptions.Exceptions.Length} entries, the limit is {MaxExceptions}: fix the translations instead");
			}

			foreach (GlossaryExceptions.Entry entry in exceptions.Exceptions)
			{
				if (!english.ContainsKey(entry.Key))
				{
					problems.Add($"exception for the unknown key {entry.Key}");
				}

				if (!languages.Contains(entry.Language))
				{
					problems.Add($"exception {entry.Key}: unknown language {entry.Language}");
				}

				if (entry.Term != "*" && entry.Term != GlossaryChecker.NoTerm && glossary.Terms.All(term => term.Id != entry.Term))
				{
					problems.Add($"exception {entry.Key}: unknown term {entry.Term}");
				}

				if (String.IsNullOrWhiteSpace(entry.Reason) || entry.Reason.Contains('\n', StringComparison.Ordinal) || entry.Reason.Length < 10 || entry.Reason.Length > 200)
				{
					problems.Add($"exception {entry.Key} [{entry.Language}]: the justification must be one line of 10 to 200 characters");
				}
			}

			GlossaryChecker.Apply(GlossaryChecker.Check(glossary, _texts.Value), exceptions.Exceptions, out IReadOnlyList<GlossaryExceptions.Entry> stale);
			problems.AddRange(stale.Select(entry => $"stale exception (it silences nothing): {entry.Key} [{entry.Language}] {entry.Term}"));
			Findings.AssertNone(problems);
		}

		[Fact]
		public void TheGlossaryFilesAreUtf8WithoutBomAndCrlf()
		{
			List<String> problems = new List<String>();
			foreach (String file in new[] { Glossary.Path, GlossaryExceptions.Path, GlossaryDocument })
			{
				Byte[] bytes = File.ReadAllBytes(file);
				if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
				{
					problems.Add(Path.GetFileName(file) + " starts with a BOM");
				}

				String text = Encoding.UTF8.GetString(bytes);
				if (text.Replace("\r\n", String.Empty, StringComparison.Ordinal).Contains('\n', StringComparison.Ordinal))
				{
					problems.Add(Path.GetFileName(file) + " has line breaks that are not CRLF");
				}
			}

			Findings.AssertNone(problems);
		}

		[Fact]
		public void TheGlossaryDocumentIsExactlyWhatTheGeneratorWrites()
		{
			Assert.True(File.Exists(GlossaryDocument), @"docs\GLOSSARY.md is missing: run the tests with FSS_WRITE_GLOSSARY=1 to generate it.");

			Assert.Equal(GlossaryMarkdown.Render(Glossary.Load()), File.ReadAllText(GlossaryDocument));
		}

		[Fact]
		public void TheDocumentationLinksTheGlossary()
		{
			List<String> problems = new List<String>();
			foreach (String file in new[] { "README.md", Path.Combine("docs", "CONVENTIONS.md"), Path.Combine("docs", "HOW_TO.md") })
			{
				if (!File.ReadAllText(Path.Combine(TestFiles.RepositoryRoot, file)).Contains("GLOSSARY.md", StringComparison.Ordinal))
				{
					problems.Add(file + " does not link docs\\GLOSSARY.md");
				}
			}

			Findings.AssertNone(problems);
		}

		[Fact]
		public void WriteTheDocument()
		{
			if (Environment.GetEnvironmentVariable("FSS_WRITE_GLOSSARY") != "1")
			{
				return;
			}

			File.WriteAllText(GlossaryDocument, GlossaryMarkdown.Render(Glossary.Load()), new UTF8Encoding(false));
		}

		private static IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>> LoadTexts()
		{
			Dictionary<String, IReadOnlyDictionary<String, String>> texts = new Dictionary<String, IReadOnlyDictionary<String, String>>(StringComparer.Ordinal);
			foreach (String code in SourceTree.LanguageCodes())
			{
				texts[code] = SourceTree.LoadStrings(code);
			}

			return texts;
		}
	}
}
