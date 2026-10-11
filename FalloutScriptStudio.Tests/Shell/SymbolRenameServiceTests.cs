namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class SymbolRenameServiceTests
	{
		private const String Script =
			"#define LIMIT 10\r\n"
			+ "variable begin\r\n"
			+ "   counter;\r\n"
			+ "end\r\n"
			+ "procedure start begin\r\n"
			+ "   counter := LIMIT;\r\n"
			+ "   counter := counter + LIMIT;\r\n"
			+ "end\r\n";

		// The internal parser does not track variables, so the symbol is described the way the native parser would.
		private static ScriptVariable CreateVariable(DocumentViewModel document)
		{
			return new ScriptVariable("counter", VariableScope.Global)
			{
				DeclaredFile = document.FilePath,
				DeclaredLine = 3,
				References = new[] { SourceReference.Create(document.FilePath, 6), SourceReference.Create(document.FilePath, 7), SourceReference.Create(document.FilePath, 7) },
			};
		}

		private static Int32 Count(String text, String word)
		{
			return text.Split(word).Length - 1;
		}

		[Fact]
		public void Renaming_a_variable_changes_the_declaration_and_every_use()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol variable = CreateVariable(document);
				harness.Dialogs.PromptAnswer = "total";

				await harness.Workspace.RenameSymbolAsync(document, variable);

				String text = document.TextDocument.Text;
				Assert.DoesNotContain("counter", text, StringComparison.Ordinal);
				Assert.Equal(4, Count(text, "total"));
				Assert.True(document.TextDocument.UndoStack.CanUndo);
			});
		}

		[Fact]
		public void Keeping_the_name_changes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol variable = CreateVariable(document);
				harness.Dialogs.PromptAnswer = "counter";

				await harness.Workspace.RenameSymbolAsync(document, variable);

				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Cancelling_the_name_prompt_changes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol variable = CreateVariable(document);
				harness.Dialogs.PromptAnswer = null;

				await harness.Workspace.RenameSymbolAsync(document, variable);

				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void A_macro_of_the_script_is_renamed_without_a_preview()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.ScriptMacros["LIMIT"];
				harness.Dialogs.PromptAnswer = "MAXV";

				await harness.Workspace.RenameSymbolAsync(document, macro);

				String text = document.TextDocument.Text;
				Assert.Null(harness.DialogueDialogs.LastPreview);
				Assert.DoesNotContain("LIMIT", text, StringComparison.Ordinal);
				Assert.Equal(3, Count(text, "MAXV"));
			});
		}

		[Fact]
		public void A_global_macro_is_renamed_in_the_project_after_the_preview()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String header = harness.Folder.Write("headers\\globals.h", "#define GLOBAL_X 5\r\n");
				String other = harness.Folder.Write("project\\other.ssl", "procedure start begin\r\n   display_msg(GLOBAL_X);\r\nend\r\n");
				harness.Settings.Settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				harness.Parser.GlobalMacros.Replace(new[] { new ScriptMacro("GLOBAL_X", "GLOBAL_X", "5", header, 1, null) });
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n   display_msg(GLOBAL_X);\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.GlobalMacros.Macros["GLOBAL_X"];
				harness.Dialogs.PromptAnswer = "GLOBAL_Y";

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.NotNull(harness.DialogueDialogs.LastPreview);
				Assert.Contains("GLOBAL_Y", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Contains("GLOBAL_Y", File.ReadAllText(other), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Cancelling_the_preview_of_a_global_macro_changes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String header = harness.Folder.Write("headers\\globals.h", "#define GLOBAL_X 5\r\n");
				harness.Parser.GlobalMacros.Replace(new[] { new ScriptMacro("GLOBAL_X", "GLOBAL_X", "5", header, 1, null) });
				String source = "procedure start begin\r\n   display_msg(GLOBAL_X);\r\nend\r\n";
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", source);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.GlobalMacros.Macros["GLOBAL_X"];
				harness.Dialogs.PromptAnswer = "GLOBAL_Y";
				harness.DialogueDialogs.Cancel = true;

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.Equal(source, document.TextDocument.Text);
			});
		}

		private static async Task<(WorkspaceHarness Harness, DocumentViewModel Document, ScriptProcedure Procedure, String Header)> CreateSharedProcedureAsync()
		{
			WorkspaceHarness harness = new WorkspaceHarness();
			String header = harness.Folder.Write("defs.h", "procedure shared_proc begin\r\nend\r\n");
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n   call shared_proc;\r\nend\r\n");
			await harness.Workspace.RequestParseAsync(document, true);
			ScriptProcedure procedure = new ScriptProcedure("shared_proc")
			{
				DeclaredFile = header,
				StartFile = header,
				DeclaredLine = 1,
				DefinedLine = 1,
				StartLine = 1,
				EndLine = 2,
				References = new[] { SourceReference.Create(document.FilePath, 2) },
			};
			harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("renamed_proc", "renamed_proc", ProcedurePlacement.AtEnd, false);
			return (harness, document, procedure, header);
		}

		[Fact]
		public void A_procedure_defined_in_another_file_is_renamed_there_too_when_the_user_agrees()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, ScriptProcedure procedure, String header) = await CreateSharedProcedureAsync();
				using WorkspaceHarness _h = harness;
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);

				await harness.Workspace.RenameSymbolAsync(document, procedure);

				Assert.Contains("call renamed_proc;", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal("procedure renamed_proc begin\r\nend\r\n", File.ReadAllText(header));
				Assert.Contains("defs.h", Assert.Single(harness.Dialogs.Confirmations), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_procedure_defined_in_another_file_stays_unchanged_there_when_the_user_declines()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, ScriptProcedure procedure, String header) = await CreateSharedProcedureAsync();
				using WorkspaceHarness _h = harness;
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);

				await harness.Workspace.RenameSymbolAsync(document, procedure);

				Assert.Contains("call renamed_proc;", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal("procedure shared_proc begin\r\nend\r\n", File.ReadAllText(header));
			});
		}

		[Fact]
		public void Cancelling_the_question_about_the_defining_file_cancels_the_rename()
		{
			ShellStaRunner.Run(async () =>
			{
				(WorkspaceHarness harness, DocumentViewModel document, ScriptProcedure procedure, String header) = await CreateSharedProcedureAsync();
				using WorkspaceHarness _h = harness;
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				await harness.Workspace.RenameSymbolAsync(document, procedure);

				Assert.Contains("call shared_proc;", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal("procedure shared_proc begin\r\nend\r\n", File.ReadAllText(header));
			});
		}

		[Fact]
		public void A_procedure_defined_in_the_script_itself_does_not_ask_about_another_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("begin_here", "begin_here", ProcedurePlacement.AtEnd, false);

				await harness.Workspace.RenameSymbolAsync(document, document.ProgramInfo!.GetProcedure("start")!);

				Assert.Empty(harness.Dialogs.Confirmations);
				Assert.Contains("procedure begin_here begin", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Renaming_a_global_macro_reloads_the_global_macro_registry()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String header = harness.Folder.Write("headers\\globals.h", "#define GLOBAL_X 5\r\n");
				harness.Settings.Settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				harness.Parser.GlobalMacros.Replace(new[] { new ScriptMacro("GLOBAL_X", "GLOBAL_X", "5", header, 1, null) });
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n   display_msg(GLOBAL_X);\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.GlobalMacros.Macros["GLOBAL_X"];
				harness.Dialogs.PromptAnswer = "GLOBAL_Y";
				Int32 before = harness.Parser.MacroReloads;

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.Equal(before + 1, harness.Parser.MacroReloads);
			});
		}

		[Fact]
		public void Renaming_a_local_macro_does_not_reload_the_global_macro_registry()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.ScriptMacros["LIMIT"];
				harness.Dialogs.PromptAnswer = "MAXV";
				Int32 before = harness.Parser.MacroReloads;

				await harness.Workspace.RenameSymbolAsync(document, macro);

				Assert.Equal(before, harness.Parser.MacroReloads);
			});
		}
	}
}
