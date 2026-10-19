namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Xunit;

	/// <summary>The rules of the glossary check, on a tiny glossary and tiny dictionaries (no file of the application is read).</summary>
	public sealed class GlossaryCheckerTests
	{
		private const String SampleGlossary = """
			{
			  "version": 1, "about": "", "matching": [], "ignore": ["Fallout Script Studio"],
			  "forbiddenEverywhere": { "pt": [ { "text": "ficheiro", "chars": null, "reason": "European Portuguese" } ], "zh": [ { "text": null, "chars": "設檔", "reason": "Traditional" } ] },
			  "groups": [ { "name": "G", "terms": ["node", "header", "procedure", "log"] } ],
			  "terms": [
			    { "id": "node", "english": "Node", "group": "G", "en": ["node", "nodes"], "enForbid": ["block"], "notes": "n",
			      "languages": { "de": { "canonical": "Knoten", "accept": ["knoten"], "forbid": [] }, "sv": { "canonical": "nod", "accept": ["nod"], "forbid": [] } } },
			    { "id": "header", "english": "Header", "group": "G", "en": ["header", "headers"], "enForbid": [], "notes": "h",
			      "languages": { "de": { "canonical": "Header", "accept": ["header"], "forbid": ["kopfzeile"] }, "sv": { "canonical": "huvudfil", "accept": ["huvudfil"], "forbid": ["headerfil"] } } },
			    { "id": "procedure", "english": "Procedure", "group": "G", "en": ["procedure"], "enForbid": [], "notes": "p",
			      "languages": { "de": { "canonical": "Prozedur", "accept": ["prozedur"], "forbid": [] }, "sv": { "canonical": "procedur", "accept": ["procedur"], "forbid": [] } } },
			    { "id": "log", "english": "Log", "group": "G", "en": ["log"], "enForbid": [], "notes": "l",
			      "languages": { "de": { "canonical": "Protokoll", "accept": ["protokoll"], "forbid": [] }, "sv": { "canonical": "logg", "accept": ["^logg", "protokoll$"], "forbid": [] } } }
			  ]
			}
			""";

		private static readonly Glossary _glossary = Glossary.Parse(SampleGlossary);

		private static IReadOnlyList<GlossaryChecker.Violation> Check(Dictionary<String, String> english, Dictionary<String, String> de, Dictionary<String, String> sv, Dictionary<String, String>? pt = null, Dictionary<String, String>? zh = null)
		{
			Dictionary<String, IReadOnlyDictionary<String, String>> texts = new Dictionary<String, IReadOnlyDictionary<String, String>>
			{
				["en"] = english,
				["de"] = de,
				["sv"] = sv,
				["pt"] = pt ?? [],
				["zh"] = zh ?? [],
			};
			return GlossaryChecker.Check(_glossary, texts);
		}

		[Fact]
		public void ATextWithoutTheCanonicalTermIsReportedWithKeyLanguageAndExpectedTerm()
		{
			IReadOnlyList<GlossaryChecker.Violation> violations = Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Rename node {0}" },
				new Dictionary<String, String> { ["Str.A.One"] = "Block {0} umbenennen" },
				new Dictionary<String, String> { ["Str.A.One"] = "Byt namn på nod {0}" });

			GlossaryChecker.Violation violation = Assert.Single(violations);
			Assert.Equal("Str.A.One", violation.Key);
			Assert.Equal("de", violation.Language);
			Assert.Equal("node", violation.Term);
			Assert.Equal("missing", violation.Kind);
			Assert.Contains("Knoten", violation.Detail, StringComparison.Ordinal);
		}

		[Fact]
		public void InflectedAndCompoundFormsAreAccepted()
		{
			Assert.Empty(Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Dialogue nodes" },
				new Dictionary<String, String> { ["Str.A.One"] = "Dialogknoten" },
				new Dictionary<String, String> { ["Str.A.One"] = "Dialognoder" }));
		}

		[Fact]
		public void AForbiddenSynonymIsReportedEvenWhenTheCanonicalTermIsAlsoPresent()
		{
			IReadOnlyList<GlossaryChecker.Violation> violations = Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Select the header file" },
				new Dictionary<String, String> { ["Str.A.One"] = "Header-Kopfzeile wählen" },
				new Dictionary<String, String> { ["Str.A.One"] = "Välj huvudfil eller headerfil" });

			Assert.Equal(new[] { "de:forbidden", "sv:forbidden" }, violations.Select(violation => violation.Language + ":" + violation.Kind).ToArray());
		}

		[Fact]
		public void QuotedLiteralsFileNamesPlaceholdersAndIgnoredPhrasesDoNotTriggerATerm()
		{
			Assert.Empty(Check(
				new Dictionary<String, String>
				{
					["Str.A.One"] = "The name must contain the word 'Node'.",
					["Str.A.Two"] = "Open scripts.log and {0} Procedure.log",
					["Str.A.Three"] = "About Fallout Script Studio (node.h)",
				},
				new Dictionary<String, String> { ["Str.A.One"] = "x", ["Str.A.Two"] = "x", ["Str.A.Three"] = "x" },
				new Dictionary<String, String> { ["Str.A.One"] = "x", ["Str.A.Two"] = "x", ["Str.A.Three"] = "x" }));
		}

		[Fact]
		public void OnlyWholeEnglishWordsTriggerATerm()
		{
			Assert.Empty(Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Logging in, catalog, nodeless" },
				new Dictionary<String, String> { ["Str.A.One"] = "x" },
				new Dictionary<String, String> { ["Str.A.One"] = "x" }));
		}

		[Fact]
		public void WordStartAndWordEndFragmentsAreAnchored()
		{
			Dictionary<String, String> english = new Dictionary<String, String> { ["Str.A.One"] = "Log" };

			Assert.Empty(Check(english, new Dictionary<String, String> { ["Str.A.One"] = "Protokoll" }, new Dictionary<String, String> { ["Str.A.One"] = "Loggfil" }));
			Assert.Single(Check(english, new Dictionary<String, String> { ["Str.A.One"] = "Protokoll" }, new Dictionary<String, String> { ["Str.A.One"] = "Dialog" }));
			Assert.Empty(Check(english, new Dictionary<String, String> { ["Str.A.One"] = "Protokoll" }, new Dictionary<String, String> { ["Str.A.One"] = "Visa protokoll" }));
			Assert.Single(Check(english, new Dictionary<String, String> { ["Str.A.One"] = "Protokoll" }, new Dictionary<String, String> { ["Str.A.One"] = "Visa protokollet" }));
		}

		[Fact]
		public void TextsOfALanguageThatUseAnExcludedVariantAreReported()
		{
			IReadOnlyList<GlossaryChecker.Violation> violations = Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Open file", ["Str.A.Two"] = "Open file" },
				[],
				[],
				new Dictionary<String, String> { ["Str.A.One"] = "Abrir ficheiro", ["Str.A.Two"] = "Abrir arquivo" },
				new Dictionary<String, String> { ["Str.A.One"] = "打开檔案", ["Str.A.Two"] = "打开文件" });

			Assert.Equal(new[] { "Str.A.One:pt", "Str.A.One:zh" }, violations.Select(violation => violation.Key + ":" + violation.Language).ToArray());
		}

		[Fact]
		public void AnEnglishTextWithAForbiddenWordingIsReported()
		{
			IReadOnlyList<GlossaryChecker.Violation> violations = Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Dialogue block", ["Str.A.Two"] = "Dialogue node" },
				new Dictionary<String, String> { ["Str.A.Two"] = "Knoten" },
				new Dictionary<String, String> { ["Str.A.Two"] = "Nod" });

			GlossaryChecker.Violation violation = Assert.Single(violations);
			Assert.Equal(("Str.A.One", "en", "node", "english wording"), (violation.Key, violation.Language, violation.Term, violation.Kind));
		}

		[Fact]
		public void AnExceptionExcusesExactlyItsKeyLanguageAndTermAndAStaleOneIsReported()
		{
			IReadOnlyList<GlossaryChecker.Violation> violations = Check(
				new Dictionary<String, String> { ["Str.A.One"] = "Node and procedure", ["Str.A.Two"] = "Node" },
				new Dictionary<String, String> { ["Str.A.One"] = "Block und Routine", ["Str.A.Two"] = "Block" },
				new Dictionary<String, String> { ["Str.A.One"] = "Nod och procedur", ["Str.A.Two"] = "Block" });
			Assert.Equal(4, violations.Count);

			GlossaryExceptions.Entry excuseOne = new GlossaryExceptions.Entry("Str.A.One", "de", "node", "Idiomatic phrase in this language");
			GlossaryExceptions.Entry excuseAllOfTwo = new GlossaryExceptions.Entry("Str.A.Two", "*", "*", "Term not translated in this text");
			GlossaryExceptions.Entry stale = new GlossaryExceptions.Entry("Str.A.One", "sv", "node", "Nothing to excuse here any more");

			IReadOnlyList<GlossaryChecker.Violation> remaining = GlossaryChecker.Apply(violations, new[] { excuseOne, excuseAllOfTwo, stale }, out IReadOnlyList<GlossaryExceptions.Entry> unused);

			GlossaryChecker.Violation left = Assert.Single(remaining);
			Assert.Equal(("Str.A.One", "de", "procedure"), (left.Key, left.Language, left.Term));
			Assert.Equal(new[] { stale }, unused);
		}

		[Fact]
		public void TheViolationTextNamesTheKeyTheLanguageAndTheTerm()
		{
			GlossaryChecker.Violation violation = new GlossaryChecker.Violation("Str.A.One", "de", "node", "missing", "expected \"Knoten\" in: a\nb");

			Assert.Equal("Str.A.One [de] node: missing: expected \"Knoten\" in: a\\nb", violation.ToString());
		}
	}
}