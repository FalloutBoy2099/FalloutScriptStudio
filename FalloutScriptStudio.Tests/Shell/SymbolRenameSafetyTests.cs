namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>A rename across files must not corrupt a file that changed meanwhile, must report a partial result and must use the current symbol.</summary>
	public sealed class SymbolRenameSafetyTests
	{
		private const String UseOfMacro = "procedure start begin\r\n   display_msg(GLOBAL_X);\r\nend\r\n";

		private static async Task<(WorkspaceHarness Harness, DocumentViewModel Document, IScriptSymbol Macro)> CreateGlobalMacroAsync(params String[] projectFiles)
		{
			WorkspaceHarness harness = new WorkspaceHarness();
			String header = harness.Folder.Write("headers\\globals.h", "#define GLOBAL_X 5\r\n");
			foreach (String file in projectFiles)
			{
				harness.Folder.Write("project\\" + file, UseOfMacro);
			}

			harness.Settings.Settings.Paths.SolutionFolder = harness.Folder.Combine("project");
			harness.Parser.GlobalMacros.Replace(new[] { new ScriptMacro("GLOBAL_X", "GLOBAL_X", "5", header, 1, null) });
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", UseOfMacro);
			await harness.Workspace.RequestParseAsync(document, true);
			harness.Dialogs.PromptAnswer = "GLOBAL_Y";
			return (harness, document, document.ProgramInfo!.GlobalMacros.Macros["GLOBAL_X"]);
		}

		[Fact]
		public void A_file_that_changed_after_the_edits_were_computed_stops_the_whole_rename()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, IScriptSymbol macro) = await CreateGlobalMacroAsync("other.ssl");
				using WorkspaceHarness _h = harness;
				String other = harness.Folder.Combine("project", "other.ssl");
				String changed = "// added by another program\r\n// and another line\r\n" + UseOfMacro;
				harness.Dialogs.OnPrompt = () => File.WriteAllText(other, changed);

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.Equal(changed, File.ReadAllText(other));
				Assert.Equal(UseOfMacro, document.TextDocument.Text);
				Assert.Contains("other.ssl", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void An_open_tab_that_changed_after_the_edits_were_computed_stops_the_whole_rename()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, IScriptSymbol macro) = await CreateGlobalMacroAsync("other.ssl");
				using WorkspaceHarness _h = harness;
				String other = harness.Folder.Combine("project", "other.ssl");
				DocumentViewModel otherTab = (await harness.Workspace.OpenAsync(other))!;
				harness.Dialogs.OnPrompt = () => otherTab.TextDocument.Insert(0, "// typed\r\n");

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.Equal("// typed\r\n" + UseOfMacro, otherTab.TextDocument.Text);
				Assert.Equal(UseOfMacro, document.TextDocument.Text);
				Assert.Equal(UseOfMacro, File.ReadAllText(other));
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void A_rename_that_succeeds_in_some_files_lists_the_files_that_were_modified()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, IScriptSymbol macro) = await CreateGlobalMacroAsync("one.ssl", "two.ssl");
				using WorkspaceHarness _h = harness;
				String one = harness.Folder.Combine("project", "one.ssl");
				String two = harness.Folder.Combine("project", "two.ssl");
				File.SetAttributes(two, FileAttributes.ReadOnly);
				try
				{
					await harness.Workspace.RenameSymbolAsync(document, macro);

					String report = Assert.Single(harness.Dialogs.Messages);
					Assert.Contains(one, report, StringComparison.Ordinal);
					Assert.Contains(two, report, StringComparison.Ordinal);
					Assert.Contains("GLOBAL_Y", File.ReadAllText(one), StringComparison.Ordinal);
					Assert.Contains("GLOBAL_X", File.ReadAllText(two), StringComparison.Ordinal);
				}
				finally
				{
					File.SetAttributes(two, FileAttributes.Normal);
				}
			});
		}

		[Fact]
		public void A_symbol_taken_from_an_older_parse_is_looked_up_again_after_the_text_changed()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "#define LIMIT 10\r\nprocedure start begin\r\n   display_msg(LIMIT);\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol stale = document.ProgramInfo!.ScriptMacros["LIMIT"];
				document.TextDocument.Insert(0, "// one\r\n// two\r\n// three\r\n");
				harness.Dialogs.PromptAnswer = "MAXV";

				await harness.Workspace.RenameSymbolAsync(document, stale);

				// The value of the definition stays aligned only when the rename knows the real line of the #define.
				Assert.Equal("// one\r\n// two\r\n// three\r\n#define MAXV  10\r\nprocedure start begin\r\n   display_msg(MAXV);\r\nend\r\n", document.TextDocument.Text);
			});
		}
	}
}
