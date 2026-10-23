namespace FalloutScriptStudio.Core.Tests.Help
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class FunctionHelpTests
	{
		private static readonly String[] _opcodes =
		[
			"// VANILLA OPCODES AND MACROS:",
			"int random (int min, int max) | - Returns a random number in the range (min...max) inclusive.",
			"ObjectPtr dude_obj | - Returns a pointer to the player.",
			"void add_timer_event (ObjectPtr obj, int time, int info) | - Adds a timed event. | Arguments: | • obj - pointer to the object || Note: the delay is in game ticks.",
			"Handler talk_p_proc | - Dialogue handler, called when an attempt is made to talk to the object.",
			"// SFALL OPCODES:",
			"void set_sfall_global (string/int varname, int/float value) | - Sets the value of an sfall global variable.",
			"mixed get_sfall_arg | - Returns the next hook argument.",
			"void reg_anim_begin ([int flags], ...) | - Begins an animation.",
			"Define HOOK_SETGLOBALVAR : value 29 | - Runs when a value is set for a global variable (GVAR_*). || int     arg1 - The index of the global variable. | int     arg2 - The value being set. || int     ret1 - Overrides the value.",
		];

		private static readonly String[] _ini =
		[
			"<m>Script functions",
			"<m+>General script functions",
			"Random<d>[int] Returns a random number<s>random({int min}, {int max})",
			"<m->",
			"<m>SFALL functions",
			"<m+>Global script functions",
			"Set_Sfall_Global<d>[void] Sets the value of an sfall global variable.<s>set_sfall_global({string/int varname}, {int/float value});<cr>",
			"<m->",
			"<m+>Animation functions",
			"Reg_Anim_Begin<d>[void] Begins<s>reg_anim_begin;<cr>",
			"Obscure_Function<d>[int] Only in the ini<s>obscure_function({int x})",
			"<m->",
		];

		private static FunctionHelpCatalog Create(IEnumerable<String>? user = null)
		{
			List<FunctionNode> roots = [.. FunctionCatalogParser.Parse(_ini, FunctionSource.BuiltIn).Roots];
			if (user is not null)
			{
				roots.AddRange(FunctionCatalogParser.Parse(user.ToList(), FunctionSource.User).Roots);
			}

			return FunctionHelpCatalog.Create(_opcodes, new FunctionCatalog(roots, Array.Empty<FunctionCatalogIssue>()));
		}

		private static HelpTopic Get(FunctionHelpCatalog catalog, String name)
		{
			Assert.True(catalog.TryGet(name, out HelpTopic? topic), name);
			return topic!;
		}

		[Fact]
		public void The_signature_is_split_into_typed_parts()
		{
			FunctionSignatureCatalog signatures = FunctionSignatureCatalog.Create(["int random (int min, int max) | - x", "void reg_anim_begin ([int flags], ...) | - y", "ObjectPtr dude_obj | - z"], []);
			signatures.TryGetSignatures("random", out FunctionSignature[] random);
			signatures.TryGetSignatures("reg_anim_begin", out FunctionSignature[] anim);
			signatures.TryGetSignatures("dude_obj", out FunctionSignature[] dude);

			IReadOnlyList<SignatureSegment> segments = FunctionSignatureFormatter.Format(random[0]);

			Assert.Equal("int random(int min, int max)", FunctionSignatureFormatter.ToText(segments));
			Assert.Equal(
				new[]
				{
					SignatureSegmentKind.ReturnType, SignatureSegmentKind.Punctuation, SignatureSegmentKind.Name, SignatureSegmentKind.Punctuation,
					SignatureSegmentKind.ParameterType, SignatureSegmentKind.Punctuation, SignatureSegmentKind.ParameterName, SignatureSegmentKind.Punctuation,
					SignatureSegmentKind.ParameterType, SignatureSegmentKind.Punctuation, SignatureSegmentKind.ParameterName, SignatureSegmentKind.Punctuation,
				},
				segments.Select(static s => s.Kind).ToArray());
			Assert.Equal("void reg_anim_begin([int flags], ...)", FunctionSignatureFormatter.ToText(FunctionSignatureFormatter.Format(anim[0])));
			Assert.Equal("ObjectPtr dude_obj", FunctionSignatureFormatter.ToText(FunctionSignatureFormatter.Format(dude[0])));
		}

		[Fact]
		public void A_parameter_with_only_a_type_or_only_a_name_is_formatted()
		{
			FunctionSignature signature = new("f", "int", [new FunctionParameter("float", String.Empty, false, false), new FunctionParameter(String.Empty, "x", true, false)], false);

			Assert.Equal("int f(float, [x])", FunctionSignatureFormatter.ToText(FunctionSignatureFormatter.Format(signature)));
		}

		[Fact]
		public void An_opcode_has_its_signature_description_and_the_category_of_the_tree()
		{
			FunctionHelpCatalog catalog = Create();

			HelpTopic random = Get(catalog, "RANDOM");

			Assert.Equal("random", random.Name, ignoreCase: true);
			Assert.Equal(HelpTopicKind.Function, random.Kind);
			Assert.Equal("int random(int min, int max)", random.SignatureText);
			Assert.Equal(new[] { "Returns a random number in the range (min...max) inclusive." }, random.Description);
			Assert.Equal("Returns a random number", random.Summary);
			Assert.Equal(new[] { "Script functions > General script functions" }, random.Categories);
			Assert.Equal("random({int min}, {int max})", random.InsertCode);
			Assert.False(random.IsSfall);
			Assert.True(random.CanInsert);
		}

		[Fact]
		public void A_single_bar_is_a_line_break_and_a_double_bar_a_new_paragraph()
		{
			HelpTopic topic = Get(Create(), "add_timer_event");

			Assert.Equal(new[] { "Adds a timed event.\nArguments:\n• obj - pointer to the object", "Note: the delay is in game ticks." }, topic.Description);
			Assert.Equal("Adds a timed event.", topic.Summary);
			Assert.False(topic.CanInsert);
			Assert.Empty(topic.Categories);
		}

		[Fact]
		public void A_handler_is_a_topic_with_its_name_as_signature()
		{
			HelpTopic topic = Get(Create(), "talk_p_proc");

			Assert.Equal(HelpTopicKind.Handler, topic.Kind);
			Assert.Equal("talk_p_proc", topic.SignatureText);
			Assert.True(topic.HasDocumentation);
		}

		[Fact]
		public void A_hook_define_keeps_its_value_and_the_lines_of_its_arguments_apart()
		{
			HelpTopic topic = Get(Create(), "HOOK_SETGLOBALVAR");

			Assert.Equal(HelpTopicKind.Define, topic.Kind);
			Assert.Equal("HOOK_SETGLOBALVAR = 29", topic.SignatureText);
			Assert.Equal(new[] { "int     arg1 - The index of the global variable.", "int     arg2 - The value being set.", "int     ret1 - Overrides the value." }, topic.Arguments);
			Assert.Equal(new[] { "Runs when a value is set for a global variable (GVAR_*)." }, topic.Description);
			Assert.True(topic.IsSfall);
			Assert.Equal("hook-types", topic.DocumentationPage);
		}

		[Fact]
		public void The_sfall_section_of_opcodes_and_the_sfall_categories_mark_sfall_functions()
		{
			FunctionHelpCatalog catalog = Create();

			Assert.False(Get(catalog, "random").IsSfall);
			Assert.True(Get(catalog, "set_sfall_global").IsSfall);
			Assert.True(Get(catalog, "get_sfall_arg").IsSfall);
			Assert.True(Get(catalog, "obscure_function").IsSfall);
			Assert.Equal("global-variables", Get(catalog, "set_sfall_global").DocumentationPage);
			Assert.Equal("hook-functions", Get(catalog, "get_sfall_arg").DocumentationPage);
			Assert.Equal("animations", Get(catalog, "reg_anim_begin").DocumentationPage);
		}

		[Fact]
		public void A_function_that_only_the_ini_file_knows_has_its_signature_from_the_code()
		{
			HelpTopic topic = Get(Create(), "obscure_function");

			Assert.Equal("int obscure_function(int x)", topic.SignatureText);
			Assert.Equal(new[] { "Only in the ini" }, topic.Description);
			Assert.Equal("Only in the ini", topic.Summary);
		}

		[Fact]
		public void A_user_function_is_marked_and_not_documented_online()
		{
			FunctionHelpCatalog catalog = Create(["<m>User", "my_helper<d>[void] Does my thing<s>my_helper({int x});<cr>"]);

			HelpTopic topic = Get(catalog, "my_helper");

			Assert.Equal(HelpTopicKind.UserFunction, topic.Kind);
			Assert.False(topic.HasDocumentation);
			Assert.Null(SfallDocumentation.GetUri(topic));
			Assert.Equal("void my_helper(int x)", topic.SignatureText);
		}

		[Fact]
		public void A_user_entry_for_a_built_in_function_keeps_the_built_in_help()
		{
			FunctionHelpCatalog catalog = Create(["<m>User", "random<d>[int] My version<s>random({int a}, {int b})"]);

			Assert.Equal(HelpTopicKind.Function, Get(catalog, "random").Kind);
			Assert.Equal("Returns a random number", Get(catalog, "random").Summary);
		}

		[Fact]
		public void The_construct_labels_of_the_tree_are_found_by_their_keyword()
		{
			FunctionHelpCatalog catalog = FunctionHelpCatalog.Create([], new FunctionCatalog(FunctionCatalogParser.Parse(["<m>Constructs", "if ... then ...<d>Conditional statement<s>if {condition} then"], FunctionSource.BuiltIn).Roots, []));

			Assert.True(catalog.TryGet("if", out HelpTopic? topic));
			Assert.Equal("Conditional statement", topic!.Summary);
		}

		[Fact]
		public void Searching_ranks_by_name_and_a_blank_filter_lists_by_name()
		{
			FunctionHelpCatalog catalog = Create();

			Assert.Equal("random", catalog.Search("rand", 10)[0].Name, ignoreCase: true);
			Assert.Equal("set_sfall_global", catalog.Search("ssg", 10).Select(static t => t.Name).FirstOrDefault(), ignoreCase: true);
			Assert.Empty(catalog.Search("qqqqzzzz", 10));
			Assert.Equal(3, catalog.Search(null, 3).Count);
			Assert.Equal(catalog.Topics.OrderBy(static t => t.Name, StringComparer.OrdinalIgnoreCase).Select(static t => t.Name), catalog.Topics.Select(static t => t.Name));
			Assert.Empty(catalog.Search("a", 0));
		}

		[Fact]
		public void The_empty_catalog_knows_nothing()
		{
			Assert.False(FunctionHelpCatalog.Empty.TryGet("random", out _));
			Assert.Equal(0, FunctionHelpCatalog.Empty.Count);
			Assert.Empty(FunctionHelpCatalog.Empty.Search("x", 5));
		}

		[Fact]
		public void The_shipped_description_files_give_complete_topics()
		{
			String folder = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions");
			String[] opcodes = File.ReadAllLines(Path.Combine(folder, "opcodes.txt"));
			ParsedFunctionFile ini = FunctionCatalogParser.Parse(File.ReadAllLines(Path.Combine(folder, "functions.ini")), FunctionSource.BuiltIn);
			ParsedFunctionFile user = FunctionCatalogParser.Parse(File.ReadAllLines(Path.Combine(folder, "userfunctions.ini")), FunctionSource.User);

			FunctionHelpCatalog catalog = FunctionHelpCatalog.Create(opcodes, new FunctionCatalog(ini.Roots.Concat(user.Roots).ToList(), []));

			Assert.True(catalog.Count > 900, "topics: " + catalog.Count);
			HelpTopic global = Get(catalog, "set_sfall_global");
			Assert.Equal("void set_sfall_global(string/int varname, int/float value)", global.SignatureText);
			Assert.True(global.IsSfall);
			Assert.Equal("global-variables", global.DocumentationPage);
			Assert.Contains("8-character", string.Join(" ", global.Description), StringComparison.Ordinal);
			Assert.Contains(global.Categories, static c => c.Contains("SFALL functions", StringComparison.Ordinal));
			Assert.True(global.CanInsert);
			HelpTopic hook = Get(catalog, "HOOK_SETGLOBALVAR");
			Assert.Equal(3, hook.Arguments.Count);
			Assert.Equal(HelpTopicKind.Define, hook.Kind);
			Assert.False(Get(catalog, "dude_obj").IsSfall);
			Assert.Equal(HelpTopicKind.Handler, Get(catalog, "talk_p_proc").Kind);
			Assert.All(catalog.Topics, static t => Assert.False(String.IsNullOrWhiteSpace(t.SignatureText)));
		}

		[Fact]
		public void Every_page_the_catalog_chooses_is_a_page_of_the_site()
		{
			String folder = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions");
			String[] opcodes = File.ReadAllLines(Path.Combine(folder, "opcodes.txt"));
			ParsedFunctionFile ini = FunctionCatalogParser.Parse(File.ReadAllLines(Path.Combine(folder, "functions.ini")), FunctionSource.BuiltIn);
			HashSet<String> sitePages =
			[
				"global-script-functions", "hook-types", "hook-functions", "array-functions", "strings", "math", "maps-and-encounters", "combat", "animations",
				"perks-and-traits", "graphics", "ini-settings", "direct-memory-access", "virtual-file-system", "keyboard-and-mouse", "interface", "objects-and-scripts",
				"sfall-funcx-macros", "other", "global-variables",
			];

			FunctionHelpCatalog catalog = FunctionHelpCatalog.Create(opcodes, new FunctionCatalog(ini.Roots, []));

			Assert.All(catalog.Topics.Where(static t => t.DocumentationPage is not null), t => Assert.Contains(t.DocumentationPage!, sitePages));
			Assert.True(catalog.Topics.Count(static t => t.DocumentationPage is not null) > 300);
		}

		[Fact]
		public void Documentation_addresses_name_the_page_and_the_function()
		{
			HelpTopic global = Get(Create(), "set_sfall_global");

			Uri? uri = SfallDocumentation.GetUri(global);

			Assert.Equal("https://sfall-team.github.io/sfall/global-variables/#set_sfall_global", uri?.AbsoluteUri);
			Assert.True(SfallDocumentation.IsAllowed(uri));
		}

		[Fact]
		public void A_function_without_a_known_page_opens_the_index_of_the_site()
		{
			Assert.Equal(SfallDocumentation.BaseUri, SfallDocumentation.GetUri(Get(Create(), "random")));
			Assert.Equal(SfallDocumentation.BaseUri, SfallDocumentation.GetUri(Get(Create(), "talk_p_proc")));
			Assert.Equal("https://sfall-team.github.io/sfall/", SfallDocumentation.BaseUri.AbsoluteUri);
		}

		[Theory]
		[InlineData("https://sfall-team.github.io/sfall/", true)]
		[InlineData("https://sfall-team.github.io/sfall/hooks/#a", true)]
		[InlineData("https://SFALL-TEAM.github.io/sfall/hooks/", true)]
		[InlineData("http://sfall-team.github.io/sfall/", false)]
		[InlineData("https://sfall-team.github.io/other/", false)]
		[InlineData("https://sfall-team.github.io/", false)]
		[InlineData("https://sfall-team.github.io.evil.example/sfall/", false)]
		[InlineData("https://evil.example/sfall/", false)]
		[InlineData("https://user@sfall-team.github.io/sfall/", false)]
		[InlineData("https://sfall-team.github.io:8443/sfall/", false)]
		[InlineData("https://sfall-team.github.io/sfall/../secret", false)]
		[InlineData("file:///c:/windows/system32/calc.exe", false)]
		[InlineData("javascript:alert(1)", false)]
		[InlineData("ftp://sfall-team.github.io/sfall/", false)]
		[InlineData("//sfall-team.github.io/sfall/", false)]
		[InlineData("", false)]
		public void Only_the_documentation_site_is_allowed(String address, Boolean allowed)
		{
			Assert.Equal(allowed, SfallDocumentation.TryCreate(address, out Uri? uri));
			Assert.Equal(allowed, uri is not null);
			Assert.False(SfallDocumentation.IsAllowed(null));
		}

		[Fact]
		public void The_page_of_a_function_comes_from_its_name_or_its_category()
		{
			Assert.Equal("global-variables", SfallDocumentation.FindPage("get_sfall_global_int", []));
			Assert.Equal("hook-functions", SfallDocumentation.FindPage("set_sfall_return", []));
			Assert.Equal("hook-functions", SfallDocumentation.FindPage("register_hook_proc", []));
			Assert.Equal("hook-types", SfallDocumentation.FindPage("HOOK_COMBATDAMAGE", []));
			Assert.Equal("strings", SfallDocumentation.FindPage("substr", ["SFALL functions > String functions"]));
			Assert.Equal("objects-and-scripts", SfallDocumentation.FindPage("x", ["SFALL functions > Critter/Player functions"]));
			Assert.Null(SfallDocumentation.FindPage("x", ["Script functions > General"]));
		}

		[Fact]
		public void A_macro_shows_its_parameters_its_comment_and_its_definition()
		{
			ScriptMacro macro = new("MAX", "MAX(a, b)", "(((a) > (b)) ? (a) : (b))", @"c:\p\h.h", 7, null);

			HelpTopic topic = ProjectHelp.FromMacro(macro, "The larger of two values.\nBoth are evaluated twice.");

			Assert.Equal(HelpTopicKind.Macro, topic.Kind);
			Assert.Equal("MAX(a, b)", topic.SignatureText);
			Assert.Equal(new[] { "The larger of two values.\nBoth are evaluated twice." }, topic.Description);
			Assert.Equal("The larger of two values.", topic.Summary);
			Assert.Equal("#define MAX(a, b) (((a) > (b)) ? (a) : (b))", topic.Definition);
			Assert.Equal((@"c:\p\h.h", 7), (topic.File, topic.Line));
			Assert.False(topic.HasDocumentation);
		}

		[Fact]
		public void The_documentation_comment_of_a_macro_wins_over_the_comment_above_it()
		{
			ScriptMacro macro = new("LIMIT", "LIMIT", "10", String.Empty, 1, "The limit.\n");

			HelpTopic topic = ProjectHelp.FromMacro(macro, "ignored");

			Assert.Equal("LIMIT", topic.SignatureText);
			Assert.Equal(new[] { "The limit." }, topic.Description);
			Assert.Null(topic.File);
		}

		[Fact]
		public void A_procedure_shows_its_arguments_and_its_comment()
		{
			ScriptProcedure procedure = new("heal_party")
			{
				ArgumentCount = 2,
				Variables = [new ScriptVariable("amount", VariableScope.Local), new ScriptVariable("who", VariableScope.Local)],
				StartFile = @"c:\p\a.ssl",
				DeclaredFile = @"c:\p\a.ssl",
				StartLine = 12,
				DeclaredLine = 3,
			};

			HelpTopic topic = ProjectHelp.FromProcedure(procedure, "Heals everybody.");

			Assert.Equal("procedure heal_party(amount, who)", topic.SignatureText);
			Assert.Equal(new[] { "Heals everybody." }, topic.Description);
			Assert.Equal(12, topic.Line);
			Assert.Equal(HelpTopicKind.Procedure, topic.Kind);
		}

		[Fact]
		public void A_variable_shows_its_initial_value()
		{
			HelpTopic topic = ProjectHelp.FromVariable(new ScriptVariable("counter", VariableScope.Global) { InitialValue = "5", DeclaredFile = "a.ssl", DeclaredLine = 4 });

			Assert.Equal("variable counter", topic.SignatureText);
			Assert.Equal("counter := 5", topic.Definition);
			Assert.Equal(4, topic.Line);
		}

		[Theory]
		[InlineData("// Heals everybody.\r\nprocedure heal begin\r\nend\r\n", 2, "Heals everybody.")]
		[InlineData("// Heals.\r\n// Twice.\r\nprocedure heal begin\r\nend\r\n", 3, "Heals.\nTwice.")]
		[InlineData("/* Heals\r\n * everybody. */\r\nprocedure heal begin\r\nend\r\n", 3, "Heals\neverybody.")]
		[InlineData("/** Heals. **/\r\nprocedure heal begin\r\nend\r\n", 2, "Heals.")]
		[InlineData("// Too far.\r\n\r\nprocedure heal begin\r\nend\r\n", 3, null)]
		[InlineData("// ==========\r\nprocedure heal begin\r\nend\r\n", 2, null)]
		[InlineData("procedure heal begin\r\nend\r\n", 1, null)]
		[InlineData("procedure heal begin\r\nend\r\n", 99, null)]
		public void The_comment_above_a_line_is_read_without_its_markers(String text, Int32 line, String? expected)
		{
			Assert.Equal(expected, ProjectHelp.ReadLeadingComment(text, line));
		}
	}
}