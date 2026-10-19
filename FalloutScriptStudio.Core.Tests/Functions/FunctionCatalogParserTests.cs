namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Functions;
	using Xunit;

	public class FunctionCatalogParserTests
	{
		private static readonly String[] _sample =
		{
			"// comment",                                                  // 0
			"<m>Top",                                                      // 1
			"f1<d>First<s>f1({a});<cr>",                                   // 2
			"<m+>Sub",                                                     // 3
			"f2<d>Second<s>f2",                                            // 4
			"<m+>SubSub",                                                  // 5
			"f3<d>Third<s>f3",                                             // 6
			"<m->",                                                        // 7
			"f4<d>Fourth, back in Sub<s>f4",                               // 8
			"<m->",                                                        // 9
			"f5<d>Back in Top<s>f5",                                       // 10
			"<->",                                                         // 11
			"<m>Second",                                                   // 12
			"f6<d><s>f6;",                                                 // 13
		};

		[Fact]
		public void Parse_BuildsTheTree()
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(_sample, FunctionSource.User);

			Assert.Empty(file.Issues);
			Assert.Equal(new[] { "Top", new String('-', 50), "Second" }, file.Roots.Select(r => r.Name).ToArray());
			FunctionNode top = file.Roots[0];
			Assert.Equal(FunctionNodeKind.Category, top.Kind);
			Assert.Equal(new[] { "f1", "Sub", "f5" }, top.Children.Select(c => c.Name).ToArray());
			FunctionNode sub = top.Children[1];
			Assert.Equal(new[] { "f2", "SubSub", "f4" }, sub.Children.Select(c => c.Name).ToArray());
			Assert.Equal("f3", sub.Children[1].Children.Single().Name);
			Assert.Equal(FunctionNodeKind.Separator, file.Roots[1].Kind);
		}

		[Fact]
		public void Parse_FillsFunctionFields()
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(_sample, FunctionSource.BuiltIn);

			FunctionNode f1 = file.Roots[0].Children[0];
			Assert.Equal("First", f1.Description);
			Assert.Equal("f1({a});<cr>", f1.Code);
			Assert.False(f1.IsUser);
			Assert.Equal(2, f1.SourceLine);
			Assert.Equal("f1<d>First<s>f1({a});<cr>", f1.RawLine);
			Assert.Equal(String.Empty, file.Roots[2].Children[0].Description);
		}

		[Fact]
		public void Parse_TracksDepthAndLines()
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(_sample, FunctionSource.User);

			FunctionNode top = file.Roots[0];
			FunctionNode sub = top.Children[1];
			FunctionNode subSub = sub.Children[1];
			Assert.Equal(new[] { 0, 1, 2, 3 }, new[] { top.Depth, sub.Depth, subSub.Depth, subSub.Children[0].Depth });
			Assert.Equal((1, 10), (top.SourceLine, top.LastLine));
			Assert.Equal((3, 9), (sub.SourceLine, sub.LastLine));
			Assert.Equal((5, 7), (subSub.SourceLine, subSub.LastLine));
			Assert.Equal((12, 13), (file.Roots[2].SourceLine, file.Roots[2].LastLine));
		}

		[Fact]
		public void Parse_MalformedLines_AreReportedAndSkipped()
		{
			String[] lines =
			{
				"orphan<d>x<s>y",         // 1: function outside any category
				"<m+>orphan sub",         // 2: nested category outside any category
				"<m>Top",                 // 3
				"no markers here",        // 4: unknown line
				"nocode<d>only a desc",   // 5: no <s>
				"<m->",                   // 6: nothing to close
				"ok<d>fine<s>ok",         // 7
			};

			ParsedFunctionFile file = FunctionCatalogParser.Parse(lines, FunctionSource.User);

			Assert.Equal(new[] { 1, 2, 4, 5, 6 }, file.Issues.Select(i => i.Line).ToArray());
			Assert.All(file.Issues, issue => Assert.Equal(FunctionSource.User, issue.Source));
			FunctionNode top = Assert.Single(file.Roots);
			Assert.Equal(new[] { "nocode", "ok" }, top.Children.Select(c => c.Name).ToArray());
			Assert.Equal("only a desc", top.Children[0].Description);
			Assert.Equal(String.Empty, top.Children[0].Code);
		}

		[Fact]
		public void Parse_UnclosedSubCategories_AreKept()
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(new[] { "<m>Top", "<m+>Open", "f<d>d<s>c" }, FunctionSource.User);

			FunctionNode open = file.Roots[0].Children.Single();
			Assert.Equal("f", open.Children.Single().Name);
			Assert.Equal(2, file.Roots[0].LastLine);
		}

		[Fact]
		public void Parse_NewTopCategory_ClosesOpenSubCategories()
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(new[] { "<m>A", "<m+>Open", "<m>B", "f<d>d<s>c" }, FunctionSource.User);

			Assert.Equal(new[] { "A", "B" }, file.Roots.Select(r => r.Name).ToArray());
			Assert.Equal("f", file.Roots[1].Children.Single().Name);
		}

		[Fact]
		public void Parse_RealFunctionsIni_ReadsEverythingWithoutIssues()
		{
			String[] lines = File.ReadAllLines(RepositoryFiles.Description("functions.ini"));

			ParsedFunctionFile file = FunctionCatalogParser.Parse(lines, FunctionSource.BuiltIn);

			Assert.Empty(file.Issues);
			var catalog = new FunctionCatalog(file.Roots, file.Issues);
			Assert.True(catalog.GetFunctions().Count() > 1000);
			Assert.Equal(5, file.Roots.Count(r => r.Kind == FunctionNodeKind.Separator));

			FunctionNode call = catalog.FindFunctions("call").First();
			Assert.Equal("call {procedure};", call.Code);

			FunctionNode armor = catalog.FindFunctions("item_type_armor").Single();
			Assert.Equal("item_type_armor", armor.Code);
			Assert.Equal(3, armor.Depth);
			Assert.Equal("Value: 0", armor.Description);
			Assert.Equal(2, catalog.GetParent(armor)!.Depth);
			Assert.Equal(FunctionNodeKind.Category, catalog.GetParent(catalog.GetParent(armor)!)!.Kind);

			FunctionNode addTimer = catalog.FindFunctions("add_timer_event").Single();
			Assert.EndsWith("<cr>", addTimer.Code);
			Assert.Contains("{ObjectPtr obj}", addTimer.Code);
		}

		[Fact]
		public void Parse_RealUserFunctionsIni_ReadsTheExample()
		{
			String[] lines = File.ReadAllLines(RepositoryFiles.Description("userfunctions.ini"));

			ParsedFunctionFile file = FunctionCatalogParser.Parse(lines, FunctionSource.User);

			Assert.Empty(file.Issues);
			FunctionNode category = Assert.Single(file.Roots);
			Assert.Equal("User defined macros", category.Name);
			Assert.Equal("DEBUGMSG", category.Children[0].Name);
			Assert.Equal("Example node", category.Children[1].Name);
		}

		[Fact]
		public void Parse_TemplateOfTheService_MatchesTheShippedFile()
		{
			ParsedFunctionFile template = FunctionCatalogParser.Parse(FunctionCatalogService.UserFileTemplate.Split("\r\n"), FunctionSource.User);
			ParsedFunctionFile shipped = FunctionCatalogParser.Parse(File.ReadAllLines(RepositoryFiles.Description("userfunctions.ini")), FunctionSource.User);

			Assert.Equal(
				new FunctionCatalog(shipped.Roots, shipped.Issues).Flatten().Select(n => n.RawLine).ToArray(),
				new FunctionCatalog(template.Roots, template.Issues).Flatten().Select(n => n.RawLine).ToArray());
		}

		[Fact]
		public void Catalog_FindEquivalent_MatchesByFileLineAndText()
		{
			FunctionCatalog first = Build(_sample);
			FunctionCatalog second = Build(_sample);

			FunctionNode f4 = first.FindFunctions("f4").Single();

			Assert.Same(second.FindFunctions("f4").Single(), second.FindEquivalent(f4));
			Assert.Null(second.FindEquivalent(new FunctionNode(FunctionNodeKind.Function, "x", FunctionSource.User) { SourceLine = 99, RawLine = "x" }));
		}

		private static FunctionCatalog Build(String[] lines)
		{
			ParsedFunctionFile file = FunctionCatalogParser.Parse(lines, FunctionSource.User);
			return new FunctionCatalog(file.Roots, file.Issues);
		}
	}
}