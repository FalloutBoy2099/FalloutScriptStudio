namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Delete unused procedure, find unused variables and extract procedure, through the workspace and its dialogs.</summary>
	public sealed class ProjectRefactoringTests
	{
		private const String Library =
			"procedure start;\r\n" +
			"procedure used_proc;\r\n" +
			"procedure unused_proc;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   used_proc();\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure used_proc begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"// Not called by anyone.\r\n" +
			"procedure unused_proc begin\r\n" +
			"   display_msg(\"gone\");\r\n" +
			"end\r\n";

		[Fact]
		public void DeleteUnused_RemovesTheProcedureAfterAPreviewAndConfirmation_WithOneUndoStep()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", Library);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, "unused_proc");

				Assert.True(deleted);
				Assert.DoesNotContain("unused_proc", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.DoesNotContain("Not called by anyone", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Contains("used_proc", document.TextDocument.Text, StringComparison.Ordinal);
				String confirmation = Assert.Single(harness.Dialogs.Confirmations);
				Assert.Contains("// Not called by anyone.", confirmation, StringComparison.Ordinal);
				Assert.Contains("procedure unused_proc begin", confirmation, StringComparison.Ordinal);
				Assert.Contains("procedure unused_proc;", confirmation, StringComparison.Ordinal);
				Assert.True(document.ProgramInfo!.ProcedureExists("used_proc"));
				Assert.False(document.ProgramInfo.ProcedureExists("unused_proc"));

				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Library, document.TextDocument.Text);
			});
		}

		[Fact]
		public void DeleteUnused_DeclinedLeavesTheScriptAlone()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", Library);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				harness.Dialogs.ConfirmAnswer = false;

				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, "unused_proc");

				Assert.False(deleted);
				Assert.Equal(Library, document.TextDocument.Text);
			});
		}

		[Fact]
		public void DeleteUnused_ARealCallBlocksItAndTheUsesAreListed()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", Library);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, "used_proc");

				Assert.False(deleted);
				Assert.Equal(Library, document.TextDocument.Text);
				Assert.Empty(harness.Dialogs.Confirmations);
				String message = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains("used_proc", message, StringComparison.Ordinal);
				Assert.Contains("a.ssl:6", message, StringComparison.Ordinal);
				Assert.Contains(harness.Output.SearchResults, result => result.Line == 6);
			});
		}

		[Fact]
		public void DeleteUnused_AUseInAnotherScriptThatIncludesTheHeaderBlocksIt()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String header = harness.Write("project/headers/lib.h", "procedure shared_proc begin\r\nend\r\n");
				harness.Write("project/scripts/a.ssl", "#include \"..\\headers\\lib.h\"\r\nprocedure start begin\r\n   shared_proc();\r\nend\r\n");
				harness.Write("project/scripts/b.ssl", "procedure start begin\r\n   shared_proc();\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(header);

				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, "shared_proc");

				Assert.False(deleted);
				String message = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains("a.ssl:3", message, StringComparison.Ordinal);
				Assert.DoesNotContain("b.ssl", message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void DeleteUnused_AnExportedProcedureUsedByAnyScriptIsKept()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String exporter = harness.Write("project/scripts/b.ssl", "export procedure api begin\r\nend\r\n");
				harness.Write("project/other/a.ssl", "import procedure api;\r\nprocedure start begin\r\n   api();\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(exporter);

				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, "api");

				Assert.False(deleted);
				Assert.Contains("a.ssl:3", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void DeleteUnused_ANameInAStringCountsAsAUse()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n   register_hook_by_name(\"hooked\");\r\nend\r\n\r\nprocedure hooked begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.False(await harness.Workspace.DeleteUnusedProcedureAsync(document, "hooked"));
			});
		}

		[Fact]
		public void DeleteUnused_ARegisteredHookProcedureIsUsed()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n   register_hook_proc(HOOK_X, my_hook);\r\nend\r\n\r\nprocedure my_hook begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.False(await harness.Workspace.DeleteUnusedProcedureAsync(document, "my_hook"));
			});
		}

		[Fact]
		public void DeleteUnused_ARecursiveProcedureNobodyElseCallsCanBeDeleted()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n\r\nprocedure loop begin\r\n   loop();\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.True(await harness.Workspace.DeleteUnusedProcedureAsync(document, "loop"));
				Assert.Equal("procedure start begin\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Theory]
		[InlineData("start")]
		[InlineData("look_at_p_proc")]
		[InlineData("my_custom_p_proc")]
		public void DeleteUnused_AnEngineHandlerIsRefused(String name)
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String text = "procedure " + name + " begin\r\nend\r\n";
				String path = harness.Write("project/a.ssl", text);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.False(await harness.Workspace.DeleteUnusedProcedureAsync(document, name));

				Assert.Equal(text, document.TextDocument.Text);
				Assert.Contains(name + " is a procedure that the game or sfall calls", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void DeleteUnused_ANameOfProcHandlersIniIsRefused()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				Directory.CreateDirectory(harness.Paths.DescriptionsDirectory);
				File.WriteAllText(Path.Combine(harness.Paths.DescriptionsDirectory, "prochandlers.ini"), "<m>Dialogue\r\nNode998<d>Exit\r\n<-m>\r\n");
				String path = harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n\r\nprocedure Node998 begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.False(await harness.Workspace.DeleteUnusedProcedureAsync(document, "node998"));
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void DeleteUnused_AProcedureWithoutABodyIsReported()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure only_declared;\r\nprocedure start begin\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.False(await harness.Workspace.DeleteUnusedProcedureAsync(document, "only_declared"));
				Assert.Contains("has no body in this script", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void FindUnusedVariables_ListsTheVariablesAsInformationProblemsOfTheParser()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write(
					"project/a.ssl",
					"variable global_unused;\r\n" +
					"procedure helper(variable a, variable b) begin\r\n" +
					"   return a;\r\n" +
					"end\r\n" +
					"procedure start begin\r\n" +
					"   variable unused, written, used = 1;\r\n" +
					"   written = 2;\r\n" +
					"   display_msg(used);\r\n" +
					"end\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Int32 count = await harness.Workspace.FindUnusedVariablesAsync(document);

				Assert.Equal(4, count);
				System.Collections.Generic.IReadOnlyList<Problem> problems = harness.Problems.Problems;
				Assert.Equal(4, problems.Count);
				Assert.All(problems, problem =>
				{
					Assert.Equal(ProblemSeverity.Info, problem.Severity);
					Assert.Equal(ProblemSource.Parse, problem.Source);
					Assert.Equal(path, problem.File);
					Assert.Same(document, problem.Owner);
					Assert.Equal(ProblemOrigins.UnusedVariables, problem.Origin);
					Assert.NotNull(problem.Hint);
				});
				Problem global = problems.Single(p => p.Message.Contains("global_unused", StringComparison.Ordinal));
				Assert.Equal(1, global.Line);
				Assert.Equal(10, global.Column);
				Assert.Equal("global_unused".Length, global.Length);
				Assert.Equal(ProblemActionKind.RemoveUnusedVariable, global.Action!.Kind);
				Problem parameter = problems.Single(p => p.Message.Contains("Argument b of procedure helper", StringComparison.Ordinal));
				Assert.Null(parameter.Action);
				Problem written = problems.Single(p => p.Message.Contains("written", StringComparison.Ordinal));
				Assert.Contains("never read", written.Message, StringComparison.Ordinal);
				Assert.Null(written.Action);
				Assert.Contains(OutputTab.Errors, harness.Output.Tabs);
				Assert.Contains(harness.Output.Statuses, status => status.StartsWith("4 unused variable(s)", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void FindUnusedVariables_RunAgainReplacesTheList_AndNoneFoundIsSaid()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n   variable unused;\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				await harness.Workspace.FindUnusedVariablesAsync(document);
				await harness.Workspace.FindUnusedVariablesAsync(document);
				Assert.Single(harness.Problems.Problems);

				document.TextDocument.Text = "procedure start begin\r\nend\r\n";
				Assert.Equal(0, await harness.Workspace.FindUnusedVariablesAsync(document));
				Assert.Empty(harness.Problems.Problems);
				Assert.Contains(harness.Output.Statuses, status => status.StartsWith("No unused variable in a.ssl", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void FindUnusedVariables_ScriptVariablesOfAHeaderAreNotReported()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/h.h", "variable shared_by_scripts;\r\nprocedure helper begin\r\n   variable local_unused;\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				await harness.Workspace.FindUnusedVariablesAsync(document);

				Problem only = Assert.Single(harness.Problems.Problems);
				Assert.Contains("local_unused", only.Message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void FindUnusedVariables_AMacroOfAHeaderThatMentionsTheNameIsAUse()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				harness.Write("project/h.h", "#define BUMP counter += 1\r\n");
				String path = harness.Write("project/a.ssl", "#include \"h.h\"\r\nprocedure start begin\r\n   variable counter;\r\n   BUMP;\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Equal(0, await harness.Workspace.FindUnusedVariablesAsync(document));
			});
		}

		[Fact]
		public void RemoveUnusedVariable_TheFixOfAProblemRemovesTheDeclaration()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n   variable unused;\r\n   display_msg(1);\r\nend\r\n");
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				await harness.Workspace.FindUnusedVariablesAsync(document);
				Problem problem = Assert.Single(harness.Problems.Problems);

				await harness.Workspace.RemoveUnusedVariableAsync(problem.Action!.FilePath!, problem.Action.Line, problem.Action.Column);

				Assert.Equal("procedure start begin\r\n   display_msg(1);\r\nend\r\n", document.TextDocument.Text);
				Assert.Empty(harness.Problems.Problems);
				document.TextDocument.UndoStack.Undo();
				Assert.Contains("variable unused;", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void RemoveUnusedVariable_OpensTheScriptWhenItIsNotOpen_AndRefusesAStaleFix()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n   variable used;\r\n   display_msg(used);\r\nend\r\n");
				await harness.OpenProjectAsync("project");

				await harness.Workspace.RemoveUnusedVariableAsync(path, 2, 13);

				DocumentViewModel opened = Assert.IsType<DocumentViewModel>(harness.Workspace.FindDocument(path));
				Assert.Equal("procedure start begin\r\n   variable used;\r\n   display_msg(used);\r\nend\r\n", opened.TextDocument.Text);
				Assert.Contains("The declaration could not be removed.", harness.Output.Statuses);
			});
		}

		private const String ExtractSource =
			"procedure Foo(variable a) begin\r\n" +
			"   variable b;\r\n" +
			"   debug_msg(\"start\");\r\n" +
			"   b = a + 1;\r\n" +
			"   debug_msg(b);\r\n" +
			"   debug_msg(\"done\");\r\n" +
			"end\r\n";

		private static void Select(FakeEditorSurface editor, String text, String from, String to)
		{
			Int32 start = text.IndexOf(from, StringComparison.Ordinal);
			Int32 end = text.IndexOf(to, start, StringComparison.Ordinal) + to.Length;
			editor.Select(start, end - start);
		}

		[Fact]
		public void Extract_CreatesTheProcedureBeforeTheCurrentOne_AsOneUndoStep()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", ExtractSource);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				FakeEditorSurface editor = harness.AttachEditor(document);
				Select(editor, document.TextDocument.Text, "b = a + 1;", "debug_msg(b);");
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("Helper", "Helper(variable a)", ProcedurePlacement.AtCaret, false);

				await harness.Workspace.ExtractProcedureAsync(document);

				Assert.Equal(
					"procedure Helper(variable a) begin\r\n" +
					"   variable b;\r\n" +
					"\r\n" +
					"   b = a + 1;\r\n" +
					"   debug_msg(b);\r\n" +
					"end\r\n" +
					"\r\n" +
					"procedure Foo(variable a) begin\r\n" +
					"   debug_msg(\"start\");\r\n" +
					"   call Helper(a);\r\n" +
					"   debug_msg(\"done\");\r\n" +
					"end\r\n",
					document.TextDocument.Text);
				ProcedureNameRequest request = Assert.Single(harness.ScriptTools.Requests);
				Assert.Equal(ProcedureNameDialogMode.Extract, request.Mode);
				Assert.Equal("a", request.Subject);
				Assert.Equal("NewProcedure(a)", request.InitialName);
				Assert.Contains("Foo", request.ExistingNames);
				Assert.True(document.ProgramInfo!.ProcedureExists("Helper"));
				Assert.Contains("Procedure Helper created from the selected statements.", harness.Output.Statuses);

				document.TextDocument.UndoStack.Undo();
				Assert.Equal(ExtractSource, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Extract_AWrongParameterListIsRefusedAndAskedAgain()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", ExtractSource);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				FakeEditorSurface editor = harness.AttachEditor(document);
				Select(editor, document.TextDocument.Text, "b = a + 1;", "debug_msg(b);");
				harness.ScriptTools.ProcedureAnswers.Enqueue(new ProcedureNameResult("Helper", "Helper(variable a, variable extra)", ProcedurePlacement.AtCaret, false));
				harness.ScriptTools.ProcedureAnswers.Enqueue(new ProcedureNameResult("Helper", "Helper(variable a)", ProcedurePlacement.AtCaret, false));

				await harness.Workspace.ExtractProcedureAsync(document);

				Assert.Equal(2, harness.ScriptTools.Requests.Count);
				Assert.Equal("Helper(a)", harness.ScriptTools.Requests[1].InitialName);
				Assert.Contains("The parameters of the new procedure must be: a", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
				Assert.Contains("procedure Helper(variable a) begin", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Extract_CancellingTheNamePromptChangesNothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", ExtractSource);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				FakeEditorSurface editor = harness.AttachEditor(document);
				Select(editor, document.TextDocument.Text, "b = a + 1;", "debug_msg(b);");

				await harness.Workspace.ExtractProcedureAsync(document);

				Assert.Equal(ExtractSource, document.TextDocument.Text);
				Assert.False(document.TextDocument.UndoStack.CanUndo);
			});
		}

		[Theory]
		[InlineData("", "", "Select the statements to extract first.")]
		[InlineData("debug_msg(\"start\");", "debug_msg(\"st", "must contain complete statements")]
		[InlineData("variable b;", "variable b;", "is declared in the selection and used after it")]
		public void Extract_ARefusedSelectionIsExplained(String from, String to, String expected)
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String path = harness.Write("project/a.ssl", ExtractSource);
				await harness.OpenProjectAsync("project");
				DocumentViewModel document = await harness.OpenAsync(path);
				FakeEditorSurface editor = harness.AttachEditor(document);
				if (from.Length > 0)
				{
					Select(editor, document.TextDocument.Text, from, to);
				}

				await harness.Workspace.ExtractProcedureAsync(document);

				Assert.Contains(expected, Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);

				Assert.Equal(ExtractSource, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Extract_EveryRefusalHasALocalizedMessage()
		{
			foreach (FalloutScriptStudio.Core.Refactoring.ExtractFailure failure in Enum.GetValues<FalloutScriptStudio.Core.Refactoring.ExtractFailure>().Where(f => f != FalloutScriptStudio.Core.Refactoring.ExtractFailure.None))
			{
				String message = CodeIntelligenceOperations.GetFailureMessage(failure, "v");
				Assert.False(String.IsNullOrEmpty(message), failure.ToString());
				Assert.DoesNotContain("Str.", message, StringComparison.Ordinal);
			}
		}
	}
}