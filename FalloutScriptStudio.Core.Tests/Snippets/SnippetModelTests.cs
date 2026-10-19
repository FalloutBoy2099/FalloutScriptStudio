namespace FalloutScriptStudio.Core.Tests.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	/// <summary>The built-in snippets, the merge of the sources, the validation and the import of the templates of the functions tree.</summary>
	public sealed class SnippetModelTests
	{
		private static readonly SnippetContext Plain = new SnippetContext { IndentUnit = "   ", NewLine = "\r\n" };

		[Fact]
		public void TheBuiltInSnippets_HaveValidUniqueTriggers_ThatAreNotWordsOfTheBlockCompletion()
		{
			List<String> triggers = [];
			foreach (Snippet snippet in BuiltInSnippets.All)
			{
				Assert.Equal(SnippetTriggerIssue.None, SnippetValidator.CheckTrigger(snippet.Trigger, triggers));
				triggers.Add(snippet.Trigger);
			}

			Assert.Contains("procedure", triggers);
			Assert.Contains("node", triggers);
			Assert.All(new KeywordExpander().Keywords, keyword => Assert.DoesNotContain(keyword, triggers));
		}

		[Fact]
		public void TheBuiltInSnippets_HaveNoMistakeInTheirBody_ADescriptionAndALocalizedDescription()
		{
			foreach (Snippet snippet in BuiltInSnippets.All)
			{
				Assert.Empty(SnippetTemplate.Parse(snippet.Body).Problems);
				Assert.False(String.IsNullOrWhiteSpace(snippet.Description), snippet.Trigger);
				Assert.StartsWith(BuiltInSnippets.DescriptionKeyPrefix, snippet.DescriptionKey, StringComparison.Ordinal);
				Assert.Equal(SnippetSource.BuiltIn, snippet.Source);
				Assert.True(snippet.IsEnabled);
				Assert.False(SnippetExpander.Expand(snippet.Body, Plain).Text.Contains('$', StringComparison.Ordinal), snippet.Trigger);
			}
		}

		[Fact]
		public void TheBuiltInSnippetsThatSurroundCode_AreTheOnesWithTheSelection()
		{
			String[] surround = BuiltInSnippets.All.Where(snippet => snippet.SurroundsSelection).Select(snippet => snippet.Trigger).ToArray();

			Assert.Equal(new[] { "ifb", "ifelse", "whileb", "forx", "foreachx" }, surround);
		}

		[Fact]
		public void TheNodeSnippet_ExpandsToTheCodeOfTheFirstNodeTemplate()
		{
			Snippet node = BuiltInSnippets.All.Single(snippet => snippet.Trigger == "node");
			DialogueNodeTemplate template = DialogueNodeTemplate.All.Single(t => t.Name == "Template Code 1");

			String text = SnippetExpander.Expand(node.Body, Plain).Text;

			Assert.Equal("procedure Node001 begin" + template.Code + "end", text);
		}

		[Fact]
		public void TheOtherNodeSnippets_FollowTheOtherTemplates()
		{
			String two = SnippetExpander.Expand(BuiltInSnippets.All.Single(snippet => snippet.Trigger == "nodeif").Body, Plain).Text;
			String three = SnippetExpander.Expand(BuiltInSnippets.All.Single(snippet => snippet.Trigger == "nodemsg").Body, Plain).Text;

			Assert.Equal("procedure Node001 begin" + DialogueNodeTemplate.All.Single(t => t.Name == "Template Code 2").Code.Replace("NOption(101, Node999, 4)", "NOption(101, Node999, 4)", StringComparison.Ordinal) + "end", two);
			Assert.Equal("procedure Node001 begin" + DialogueNodeTemplate.All.Single(t => t.Name == "Template Code 3").Code + "end", three);
		}

		[Fact]
		public void ThePlaceholdersOfTheTalkProcSnippet_AreVisitedInOrder()
		{
			SnippetExpansion expansion = SnippetExpander.Expand(BuiltInSnippets.All.Single(snippet => snippet.Trigger == "talkproc").Body, Plain);

			Assert.Equal(new[] { 1, 2, 3, 4 }, expansion.Stops.Select(stop => stop.Index).ToArray());
			Assert.Equal(2, expansion.Stops[2].Occurrences.Count);
		}

		[Fact]
		public void Merge_TheProjectBeatsTheUser_WhoBeatsTheBuiltInSnippets()
		{
			Snippet[] builtIn = { new Snippet("a", "built-in a", "A"), new Snippet("b", "built-in b", "B"), new Snippet("c", "built-in c", "C") };
			Snippet[] user = { new Snippet("b", "user b", "UB") { Source = SnippetSource.User }, new Snippet("d", "user d", "UD") { Source = SnippetSource.User } };
			Snippet[] project = { new Snippet("B", "project b", "PB") { Source = SnippetSource.Project } };

			IReadOnlyList<Snippet> merged = SnippetCatalog.Merge(builtIn, user, project);

			Assert.Equal(new[] { "a", "B", "c", "d" }, merged.Select(snippet => snippet.Trigger).ToArray());
			Assert.Equal("project b", merged.Single(snippet => snippet.Trigger == "B").Description);
			Assert.Equal("user d", merged.Single(snippet => snippet.Trigger == "d").Description);
		}

		[Fact]
		public void Merge_ADisabledSnippet_HidesTheOnesOfTheEarlierSources()
		{
			Snippet[] builtIn = { new Snippet("a", "built-in a", "A"), new Snippet("b", "built-in b", "B") };
			Snippet[] user = { new Snippet("a", String.Empty, String.Empty) { IsEnabled = false, Source = SnippetSource.User } };
			Snippet[] project = { new Snippet("a", "project a", "PA") { Source = SnippetSource.Project } };

			Assert.Equal(new[] { "b" }, SnippetCatalog.Merge(builtIn, user, null).Select(snippet => snippet.Trigger).ToArray());
			Assert.Equal(new[] { "a", "b" }, SnippetCatalog.Merge(builtIn, user, project).Select(snippet => snippet.Trigger).ToArray());
		}

		[Fact]
		public void Find_ComparesTheTriggerIgnoringTheCase_AndSkipsDisabledSnippets()
		{
			Snippet[] snippets = { new Snippet("ifb", "d", "b"), new Snippet("off", "d", "b") { IsEnabled = false } };

			Assert.NotNull(SnippetCatalog.Find(snippets, "IFB"));
			Assert.Null(SnippetCatalog.Find(snippets, "off"));
			Assert.Null(SnippetCatalog.Find(snippets, "none"));
		}

		[Theory]
		[InlineData("", SnippetTriggerIssue.Empty)]
		[InlineData("   ", SnippetTriggerIssue.Empty)]
		[InlineData("ok_name1", SnippetTriggerIssue.None)]
		[InlineData("_x", SnippetTriggerIssue.None)]
		[InlineData("1abc", SnippetTriggerIssue.InvalidCharacters)]
		[InlineData("a b", SnippetTriggerIssue.InvalidCharacters)]
		[InlineData("a-b", SnippetTriggerIssue.InvalidCharacters)]
		[InlineData("é", SnippetTriggerIssue.InvalidCharacters)]
		[InlineData("if", SnippetTriggerIssue.Reserved)]
		[InlineData("proc", SnippetTriggerIssue.Reserved)]
		[InlineData("ELSE", SnippetTriggerIssue.None)]
		[InlineData("mine", SnippetTriggerIssue.Duplicate)]
		[InlineData("MINE", SnippetTriggerIssue.Duplicate)]
		public void TheTriggerIsChecked(String trigger, SnippetTriggerIssue issue)
		{
			Assert.Equal(issue, SnippetValidator.CheckTrigger(trigger, new[] { "mine" }));
		}

		[Fact]
		public void TooLongATriggerIsRefused()
		{
			Assert.Equal(SnippetTriggerIssue.None, SnippetValidator.CheckTrigger(new String('a', SnippetValidator.MaxTriggerLength), Array.Empty<String>()));
			Assert.Equal(SnippetTriggerIssue.TooLong, SnippetValidator.CheckTrigger(new String('a', SnippetValidator.MaxTriggerLength + 1), Array.Empty<String>()));
		}

		[Fact]
		public void Validate_ReportsTheTriggerAndTheMistakesOfTheBodyTogether()
		{
			SnippetValidation bad = SnippetValidator.Validate("if", "${1:abc", Array.Empty<String>());
			SnippetValidation good = SnippetValidator.Validate("mysnip", "foo(${1:x});$0", Array.Empty<String>());

			Assert.False(bad.IsValid);
			Assert.Equal(SnippetTriggerIssue.Reserved, bad.Trigger);
			Assert.Contains(bad.BodyProblems, problem => problem.Kind == SnippetProblemKind.UnterminatedPlaceholder);
			Assert.True(good.IsValid);
		}

		[Fact]
		public void TheImportOfAFunction_TurnsPlaceholdersIntoTabStops_AndLineBreaksIntoLines()
		{
			Snippet snippet = SnippetImporter.FromFunction("DEBUGMSG", "Debug messages", "if ({condition}) then begin<cr>{code}<cr>end", Array.Empty<String>());

			Assert.Equal("debugmsg", snippet.Trigger);
			Assert.Equal("Debug messages", snippet.Description);
			Assert.Equal("if (${1:condition}) then begin\n${2:code}\nend", snippet.Body);
			Assert.Equal(SnippetSource.User, snippet.Source);
		}

		[Fact]
		public void TheImport_UsesOneStopForTheSamePlaceholder_AndEscapesWhatWouldBeSyntax()
		{
			String body = SnippetImporter.ConvertCode("for ({i} := 0; {i} < {n}; {i}++) display_msg(\"$5 \\\\ {}\");");

			Assert.Equal("for (${1:i} := 0; ${1:i} < ${2:n}; ${1:i}++) display_msg(\"\\$5 \\\\\\\\ ${3}\");", body);
			SnippetExpansion expansion = SnippetExpander.Expand(body, Plain);
			Assert.Equal("for (i := 0; i < n; i++) display_msg(\"$5 \\\\ \");", expansion.Text);
			Assert.Equal(2, expansion.Stops.Single(stop => stop.Index == 1).Occurrences.Count - 1);
		}

		[Fact]
		public void TheImport_LeavesAnUnclosedBraceAlone()
		{
			Assert.Equal("a { b", SnippetExpander.Expand(SnippetImporter.ConvertCode("a { b"), Plain).Text);
		}

		[Fact]
		public void TheTriggersMadeFromNames_AreValid_Unique_AndNeverAReservedWord()
		{
			List<String> used = ["mymacro"];

			Assert.Equal("mymacro2", SnippetImporter.MakeTrigger("My Macro!", used));
			Assert.Equal("ifx", SnippetImporter.MakeTrigger("if", used));
			Assert.Equal("f12", SnippetImporter.MakeTrigger("12", used));
			Assert.Equal("f", SnippetImporter.MakeTrigger("???", used));
			Assert.True(SnippetValidator.CheckTrigger(SnippetImporter.MakeTrigger(new String('x', 100), used), used) == SnippetTriggerIssue.None);
		}

		[Fact]
		public void TheImportOfATree_FindsTheFunctionsOfEveryCategory_AndGivesThemDistinctTriggers()
		{
			ParsedFunctionFile parsed = FunctionCatalogParser.Parse(
				new[]
				{
					"<m>Mine",
					"Macros<d>First<s>Macros({example})",
					"<m+>Sub",
					"Macros<d>Second<s>Macros2({a}, {b})<cr>x",
					"<m->",
					"Empty<d>No code<s>",
					"<->",
				},
				FunctionSource.User);
			List<String> used = [];

			IReadOnlyList<Snippet> snippets = SnippetImporter.FromNodes(parsed.Roots, used);

			Assert.Equal(new[] { "macros", "macros2" }, snippets.Select(snippet => snippet.Trigger).ToArray());
			Assert.Equal("Macros(${1:example})", snippets[0].Body);
			Assert.Equal("Macros2(${1:a}, ${2:b})\nx", snippets[1].Body);
			Assert.Equal(new[] { "macros", "macros2" }, used.ToArray());
		}
	}
}