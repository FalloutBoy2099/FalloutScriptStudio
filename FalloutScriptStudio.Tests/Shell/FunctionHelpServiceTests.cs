namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class FunctionHelpServiceTests
	{
		private static ProgramInfo Info(params ScriptMacro[] macros)
		{
			return new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>(), macros.ToDictionary(static m => m.Token, StringComparer.Ordinal));
		}

		[Fact]
		public void A_built_in_function_has_its_category_signature_and_description()
		{
			using ShellHarness harness = new ShellHarness();

			HelpTopic? topic = harness.HelpService.Find("WAIT", null, null, 1);

			Assert.NotNull(topic);
			Assert.Equal("void wait(int time)", topic.SignatureText);
			Assert.Equal(new[] { "Group" }, topic.Categories);
			Assert.Equal("wait({int time});", topic.InsertCode);
			Assert.Equal(new[] { "Pauses the game for the specified time." }, topic.Description);
		}

		[Fact]
		public void An_unknown_word_has_no_help()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.Null(harness.HelpService.Find("no_such_word", null, null, 1));
			Assert.Null(harness.HelpService.Find(String.Empty, null, null, 1));
		}

		[Fact]
		public void A_user_function_of_the_tree_is_found_and_marked()
		{
			using ShellHarness harness = new ShellHarness();

			HelpTopic? topic = harness.HelpService.Find("mine", null, null, 1);

			Assert.Equal(HelpTopicKind.UserFunction, topic?.Kind);
			Assert.Equal("Mine", topic?.Summary);
		}

		[Fact]
		public void Editing_the_user_functions_makes_the_topics_again_and_announces_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Int32 changes = 0;
				harness.HelpService.Changed += (_, _) => changes++;
				FunctionNode category = harness.Catalog.Catalog.Roots.Single(static r => r.Name == "User");

				FunctionOperationResult result = await harness.Catalog.AddFunctionAsync(category, new FunctionDefinition("brand_new", "A new helper", "brand_new({int x});"));

				Assert.True(result.Succeeded);
				Assert.True(changes > 0);
				HelpTopic? topic = harness.HelpService.Find("brand_new", null, null, 1);
				Assert.Equal(HelpTopicKind.UserFunction, topic?.Kind);
				Assert.Equal("A new helper", topic?.Summary);
			});
		}

		[Fact]
		public void A_changed_opcodes_file_is_read_again()
		{
			using ShellHarness harness = new ShellHarness();
			Assert.Equal(new[] { "Pauses the game for the specified time." }, harness.HelpService.Find("wait", null, null, 1)!.Description);
			String path = Path.Combine(harness.Paths.DescriptionsDirectory, "opcodes.txt");

			File.WriteAllText(path, "void wait (int time) | - Waits a bit longer than before.\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

			Assert.Equal(new[] { "Waits a bit longer than before." }, harness.HelpService.Find("wait", null, null, 1)!.Description);
		}

		[Fact]
		public void A_missing_opcodes_file_still_gives_the_topics_of_the_tree()
		{
			using ShellHarness harness = new ShellHarness();
			File.Delete(Path.Combine(harness.Paths.DescriptionsDirectory, "opcodes.txt"));
			FalloutScriptStudio.Workspace.Help.FunctionHelpService service = new(harness.Settings, harness.Catalog, harness.Shell, harness.Workspace, TimeSpan.Zero);

			HelpTopic? topic = service.Find("wait", null, null, 1);

			Assert.Equal("wait(int time)", topic?.SignatureText);
			Assert.Equal("Waits", topic?.Summary);
		}

		[Fact]
		public void A_macro_of_the_project_shows_the_comment_above_its_define_read_from_the_file()
		{
			using ShellHarness harness = new ShellHarness();
			String path = harness.Folder.Write("h.h", "// The larger value.\r\n#define MY_MAX(a, b) (((a) > (b)) ? (a) : (b))\r\n");
			ScriptMacro macro = new("MY_MAX", "MY_MAX(a, b)", "(((a) > (b)) ? (a) : (b))", path, 2, null);

			HelpTopic? topic = harness.HelpService.Find("MY_MAX", Info(macro), path, 5);

			Assert.NotNull(topic);
			Assert.Equal(HelpTopicKind.Macro, topic.Kind);
			Assert.Equal(new[] { "The larger value." }, topic.Description);
			Assert.Equal("#define MY_MAX(a, b) (((a) > (b)) ? (a) : (b))", topic.Definition);
		}

		[Fact]
		public void The_comment_is_read_again_when_the_file_changes()
		{
			using ShellHarness harness = new ShellHarness();
			String path = harness.Folder.Write("h.h", "// First.\r\n#define LIMIT 5\r\n");
			ScriptMacro macro = new("LIMIT", "LIMIT", "5", path, 2, null);
			Assert.Equal(new[] { "First." }, harness.HelpService.Find("LIMIT", Info(macro), path, 1)!.Description);

			File.WriteAllText(path, "// Second one.\r\n#define LIMIT 5\r\n");
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

			Assert.Equal(new[] { "Second one." }, harness.HelpService.Find("LIMIT", Info(macro), path, 1)!.Description);
		}

		[Fact]
		public void The_unsaved_text_of_an_open_document_wins_over_the_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FalloutScriptStudio.Documents.DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "// Saved comment.\r\n#define LIMIT 5\r\n");
				document.TextDocument.Replace(0, 17, "// Edited comment");
				ScriptMacro macro = new("LIMIT", "LIMIT", "5", document.FilePath!, 2, null);

				HelpTopic? topic = harness.HelpService.Find("LIMIT", Info(macro), document.FilePath, 1);

				Assert.Equal(new[] { "Edited comment" }, topic?.Description);
			});
		}

		[Fact]
		public void A_macro_without_a_comment_shows_only_its_definition_and_a_missing_file_does_not_fail()
		{
			using ShellHarness harness = new ShellHarness();
			ScriptMacro macro = new("LIMIT", "LIMIT", "5", harness.Folder.Combine("gone.h"), 3, null);

			HelpTopic? topic = harness.HelpService.Find("LIMIT", Info(macro), null, 1);

			Assert.NotNull(topic);
			Assert.Empty(topic.Description);
			Assert.Equal("#define LIMIT 5", topic.Definition);
			Assert.True(topic.IsDocumented);
		}

		[Fact]
		public void A_procedure_of_the_project_shows_its_comment()
		{
			using ShellHarness harness = new ShellHarness();
			String path = harness.Folder.Write("a.ssl", "// Heals everybody.\r\nprocedure heal begin\r\nend\r\n");
			ScriptProcedure procedure = new("heal") { StartFile = path, DeclaredFile = path, StartLine = 2, DeclaredLine = 2, EndLine = 3 };
			ProgramInfo info = new(new[] { procedure }, Array.Empty<ScriptVariable>(), new Dictionary<String, ScriptMacro>());

			HelpTopic? topic = harness.HelpService.Find("heal", info, path, 2);

			Assert.Equal(HelpTopicKind.Procedure, topic?.Kind);
			Assert.Equal(new[] { "Heals everybody." }, topic?.Description);
		}

		[Fact]
		public void The_comment_of_a_forward_declaration_is_used_when_the_body_has_none()
		{
			using ShellHarness harness = new ShellHarness();
			String path = harness.Folder.Write("a.ssl", "// Heals everybody.\r\nprocedure heal;\r\n\r\nprocedure heal begin\r\nend\r\n");
			ScriptProcedure procedure = new("heal") { StartFile = path, DeclaredFile = path, StartLine = 4, DeclaredLine = 2, EndLine = 5 };
			ProgramInfo info = new(new[] { procedure }, Array.Empty<ScriptVariable>(), new Dictionary<String, ScriptMacro>());

			HelpTopic? topic = harness.HelpService.Find("heal", info, path, 4);

			Assert.Equal(new[] { "Heals everybody." }, topic?.Description);
		}

		[Fact]
		public void Opening_the_documentation_uses_the_shell_with_the_address_of_the_page()
		{
			using ShellHarness harness = new ShellHarness();
			HelpTopic topic = harness.HelpService.Find("set_sfall_global", null, null, 1)!;

			Uri? opened = harness.HelpService.OpenDocumentation(topic);

			Assert.Equal("https://sfall-team.github.io/sfall/global-variables/#set_sfall_global", opened?.AbsoluteUri);
			Assert.Equal(new[] { "https://sfall-team.github.io/sfall/global-variables/#set_sfall_global" }, harness.Shell.Opened);
		}

		[Fact]
		public void A_topic_of_the_project_has_no_documentation_page_and_nothing_is_opened()
		{
			using ShellHarness harness = new ShellHarness();
			HelpTopic topic = ProjectHelp.FromMacro(new ScriptMacro("LIMIT", "LIMIT", "5", "a.h", 1, null));

			Assert.Null(harness.HelpService.OpenDocumentation(topic));
			Assert.Empty(harness.Shell.Opened);
		}

		[Fact]
		public void A_shell_that_cannot_open_the_address_gives_null()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Shell.OpenWithShellResult = false;

			Assert.Null(harness.HelpService.OpenDocumentation(harness.HelpService.Find("wait", null, null, 1)!));
		}

		[Fact]
		public void Searching_follows_the_catalog()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.Contains(harness.HelpService.Search("wa", 10), static t => t.Name == "wait");
			Assert.True(harness.HelpService.Catalog.Count >= 3);
		}

		[Fact]
		public async Task A_local_variable_of_a_procedure_shows_its_declaration()
		{
			using ShellHarness harness = new ShellHarness();
			ScriptVariable local = new("counter", VariableScope.Local) { InitialValue = "0", DeclaredFile = "a.ssl", DeclaredLine = 3 };
			ScriptProcedure procedure = new("start") { StartFile = "a.ssl", DeclaredFile = "a.ssl", StartLine = 2, EndLine = 6, DeclaredLine = 2, Variables = [local], ArgumentCount = 0 };
			ProgramInfo info = new(new[] { procedure }, Array.Empty<ScriptVariable>(), new Dictionary<String, ScriptMacro>());

			HelpTopic? topic = harness.HelpService.Find("counter", info, "a.ssl", 4);

			Assert.Equal(HelpTopicKind.Variable, topic?.Kind);
			Assert.Equal("counter := 0", topic?.Definition);
			await Task.CompletedTask;
		}
	}
}