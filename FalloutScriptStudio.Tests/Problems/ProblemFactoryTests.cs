namespace FalloutScriptStudio.Tests.Problems
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class ProblemFactoryTests
	{
		private static IEnumerable<Problem> AllProblems()
		{
			Object owner = new Object();
			yield return ProblemFactory.FromToolFailure(new ToolFailure(ToolFailureKind.NotFound, @"C:\app\resources\compile.exe"));
			yield return ProblemFactory.FromToolFailure(new ToolFailure(ToolFailureKind.StartFailed, @"C:\app\resources\compile.exe", "not a valid application"));
			yield return ProblemFactory.BuildFailedWithoutDetails(@"C:\s\a.ssl", false, owner);
			yield return ProblemFactory.BuildFailedWithoutDetails(@"C:\s\a.ssl", true, owner);
			yield return ProblemFactory.OutputFolderNotSet(owner);
			yield return ProblemFactory.NotAScript("a.h", owner);
			yield return ProblemFactory.SaveFailed(@"C:\s\missing\a.ssl", new IOException("disk full"), owner);
			yield return ProblemFactory.OpenFailed(@"C:\s\a.ssl", new IOException("locked"));
			yield return ProblemFactory.FileNotFound(@"C:\s\a.ssl");
			yield return ProblemFactory.EncodingFallback(@"C:\s\a.ssl", "Western European (Windows)", owner);
			yield return ProblemFactory.DecompileFailed(@"C:\s\a.int");
			yield return ProblemFactory.ExtractFailed("a.int", "not found in the archive");
			yield return ProblemFactory.IncludeNotFound(@"C:\h\x.h", @"C:\s\a.ssl", owner);
			yield return ProblemFactory.MessageFileMissing(@"C:\s\a.ssl", owner);
			yield return ProblemFactory.NativeParser(false, null);
			yield return ProblemFactory.NativeParser(true, "crashed three times");
			yield return ProblemFactory.ParseFailed(@"C:\s\a.ssl", new InvalidOperationException("boom"), owner);
			yield return ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.Damaged, @"C:\app\settings\settings.json"));
			yield return ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.Unreadable, @"C:\app\settings\settings.json", "locked"));
			yield return ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.SaveFailed, @"C:\app\settings\settings.json", "disk full"));
			yield return ProblemFactory.ExternalEditorFailed(@"C:\tools\editor.exe");
			yield return ProblemFactory.OperationFailed("it broke");
			yield return ProblemFactory.FormatFileChanged(@"C:\s\a.ssl");
			yield return ProblemFactory.FormatFileChanged("Untitled.ssl");
			yield return ProblemFactory.FormatReadOnly(@"C:\s\a.ssl");
			yield return ProblemFactory.FormatFailed(@"C:\s\a.ssl", "access denied");
			yield return ProblemFactory.ProjectFileInvalid(@"C:\mods\a\fss-project.json", new ProjectFileError("'}' is invalid after a property name", 3, 7));
			yield return ProblemFactory.ProjectFileInvalid(@"C:\mods\a\fss-project.json", new ProjectFileError("The file does not contain a settings object"));
			yield return ProblemFactory.ProjectDocumentsAvailable(@"C:\mods\a", "Great Mod", 4);
			yield return ProblemFactory.UnusedVariable(new FalloutScriptStudio.Core.Analysis.UnusedVariable("x", FalloutScriptStudio.Core.Analysis.VariableDeclarationKind.Local, FalloutScriptStudio.Core.Analysis.UnusedVariableStatus.Unused, "start", 10, 3, 5, 1, true), @"C:\s\a.ssl", owner);
			yield return ProblemFactory.UnusedVariable(new FalloutScriptStudio.Core.Analysis.UnusedVariable("x", FalloutScriptStudio.Core.Analysis.VariableDeclarationKind.Local, FalloutScriptStudio.Core.Analysis.UnusedVariableStatus.WriteOnly, "start", 10, 3, 5, 1, false), @"C:\s\a.ssl", owner);
			yield return ProblemFactory.UnusedVariable(new FalloutScriptStudio.Core.Analysis.UnusedVariable("x", FalloutScriptStudio.Core.Analysis.VariableDeclarationKind.Parameter, FalloutScriptStudio.Core.Analysis.UnusedVariableStatus.Unused, "start", 10, 3, 5, 1, false), @"C:\s\a.ssl", owner);
			foreach (FalloutScriptStudio.Core.Analysis.ScriptDiagnosticRule rule in FalloutScriptStudio.Core.Analysis.ScriptDiagnosticCodes.All)
			{
				yield return ProblemFactory.ScriptDiagnostic(new FalloutScriptStudio.Core.Analysis.ScriptDiagnostic(rule.Code, rule.Severity, rule.MessageKey, new[] { "a", "b", "c", "d" }, 5, 3, 2, 1) { Fix = new FalloutScriptStudio.Core.Analysis.ScriptQuickFix(FalloutScriptStudio.Core.Analysis.ScriptQuickFix.RenameId, "other", 5, 3) }, @"C:\s\a.ssl", owner);
			}

			foreach (FalloutScriptStudio.Core.Dialogue.ScriptMessageFindingKind kind in Enum.GetValues<FalloutScriptStudio.Core.Dialogue.ScriptMessageFindingKind>())
			{
				yield return ProblemFactory.ScriptMessage(new FalloutScriptStudio.Core.Dialogue.ScriptMessageFinding(kind, FalloutScriptStudio.Core.Dialogue.ScriptMessageSeverity.Info, 101, FalloutScriptStudio.Core.Dialogue.ScriptMessageUsage.OwnFile, true, 5, 3, 3, 4), @"C:\s\a.ssl", @"C:\s\a.msg", new[] { 101 }, 1000, owner);
			}

			yield return ProblemFactory.ScriptMessage(new FalloutScriptStudio.Core.Dialogue.ScriptMessageFinding(FalloutScriptStudio.Core.Dialogue.ScriptMessageFindingKind.MissingMessage, FalloutScriptStudio.Core.Dialogue.ScriptMessageSeverity.Error, 7, 124, true, 5, 3, 1), @"C:\s\a.ssl", null, Array.Empty<Int32>(), 1000, owner);

			foreach (FalloutScriptStudio.Services.ScriptTools.Replace.ReplaceFailureReason reason in Enum.GetValues<FalloutScriptStudio.Services.ScriptTools.Replace.ReplaceFailureReason>())
			{
				yield return ProblemFactory.ReplaceFileSkipped(@"C:\mods\a\scripts\a.ssl", reason, 3, "access denied");
			}

			foreach (FalloutScriptStudio.Core.Globals.GlobalFindingKind kind in Enum.GetValues<FalloutScriptStudio.Core.Globals.GlobalFindingKind>())
			{
				yield return ProblemFactory.GlobalVariable(new FalloutScriptStudio.Core.Globals.GlobalFinding(kind, FalloutScriptStudio.Core.Globals.GlobalFindingSeverity.Warning, "GVAR_A", "GVAR_B", 3, @"C:\s\a.ssl", 5, "start"));
			}

			foreach (FalloutScriptStudio.Core.Dialogue.DialogueQualityCode code in Enum.GetValues<FalloutScriptStudio.Core.Dialogue.DialogueQualityCode>())
			{
				yield return ProblemFactory.DialogueQuality(
					new FalloutScriptStudio.Core.Dialogue.DialogueQualityFinding(code, FalloutScriptStudio.Core.Dialogue.DialogueQualitySeverity.Warning, "Node001", 12, "Node002", 3, new[] { 101, 102 }, new[] { "Node001", "Node002" }),
					@"C:\s\a.ssl",
					owner);
			}

			foreach (FalloutScriptStudio.Core.Messages.MessageComparisonStatus status in Enum.GetValues<FalloutScriptStudio.Core.Messages.MessageComparisonStatus>())
			{
				FalloutScriptStudio.Core.Messages.MessageComparisonRow row = new(5, FalloutScriptStudio.Core.Messages.MessageEntry.CreateMessage(5, "Hello"), FalloutScriptStudio.Core.Messages.MessageEntry.CreateMessage(5, "Hello"), 3, 4, status, status == FalloutScriptStudio.Core.Messages.MessageComparisonStatus.Ok ? FalloutScriptStudio.Core.Messages.MessageComparisonIssues.TrailingSpaces : FalloutScriptStudio.Core.Messages.MessageComparisonIssues.None);
				foreach (Problem problem in ProblemFactory.MessageComparison(row, @"C:\t\english\a.msg", @"C:\t\french\a.msg", FalloutScriptStudio.ViewModels.ToolWindows.ComparisonTexts.IssueNames))
				{
					yield return problem;
				}
			}


			yield return ProblemFactory.ReplaceFileSkipped("<unsaved>", FalloutScriptStudio.Services.ScriptTools.Replace.ReplaceFailureReason.Closed, 2, String.Empty);
		}

		[Fact]
		public void An_invalid_project_file_is_a_warning_that_points_at_the_error_and_offers_to_open_the_file()
		{
			Problem problem = ProblemFactory.ProjectFileInvalid(@"C:\mods\a\fss-project.json", new ProjectFileError("Expected a ':'", 3, 7));

			Assert.Equal(ProblemSeverity.Warning, problem.Severity);
			Assert.Equal(ProblemSource.Settings, problem.Source);
			Assert.Equal(@"C:\mods\a\fss-project.json", problem.File);
			Assert.Equal(3, problem.Line);
			Assert.Equal(7, problem.Column);
			Assert.Equal(ProblemOrigins.ProjectFile, problem.Origin);
			Assert.Equal("fss-project.json is not valid: Expected a ':' (line 3, column 7). The last valid project settings stay in effect.", problem.Message);
			Assert.Equal(ProblemActionKind.OpenFile, problem.Action!.Kind);
			Assert.Equal(@"C:\mods\a\fss-project.json", problem.Action.FilePath);
			Assert.Equal("Open the file", problem.Action.Label);
		}

		[Fact]
		public void An_invalid_project_file_without_a_position_has_no_line_to_go_to()
		{
			Problem problem = ProblemFactory.ProjectFileInvalid(@"C:\mods\a\fss-project.json", new ProjectFileError("The file does not contain a settings object"));

			Assert.Equal(-1, problem.Line);
			Assert.Equal(-1, problem.Column);
			Assert.Equal("fss-project.json is not valid: The file does not contain a settings object. The last valid project settings stay in effect.", problem.Message);
		}

		[Fact]
		public void The_documents_of_a_project_are_offered_with_an_information_and_a_restore_link()
		{
			Problem problem = ProblemFactory.ProjectDocumentsAvailable(@"C:\mods\a", "Great Mod", 4);

			Assert.Equal(ProblemSeverity.Info, problem.Severity);
			Assert.Equal(ProblemOrigins.ProjectSession, problem.Origin);
			Assert.Equal("4 file(s) were open the last time project Great Mod was open.", problem.Message);
			Assert.Equal(ProblemActionKind.RestoreDocuments, problem.Action!.Kind);
			Assert.Equal(@"C:\mods\a", problem.Action.FolderPath);
			Assert.Equal("Restore open files", problem.Action.Label);
		}

		public static IEnumerable<Object[]> Problems()
		{
			return Enumerable.Range(0, AllProblems().Count()).Select(index => new Object[] { index });
		}

		[Theory]
		[MemberData(nameof(Problems))]
		public void Every_problem_has_a_message_and_a_hint_that_exist_and_are_fully_formatted(Int32 index)
		{
			Problem problem = AllProblems().ElementAt(index);
			Assert.NotNull(problem.MessageKey);
			Assert.True(Loc.TryGet(problem.MessageKey!, out String? messageFormat), problem.MessageKey);
			Assert.NotNull(problem.HintKey);
			Assert.True(Loc.TryGet(problem.HintKey!, out String? hintFormat), problem.HintKey);
			Assert.DoesNotMatch(new Regex(@"\{\d+\}"), problem.Message);
			Assert.DoesNotMatch(new Regex(@"\{\d+\}"), problem.Hint!);
			Assert.NotEqual(problem.MessageKey, problem.Message);

			// Every placeholder of the text has an argument.
			Assert.True(CountPlaceholders(messageFormat!) <= problem.MessageArguments.Count, problem.MessageKey);
			Assert.True(CountPlaceholders(hintFormat!) <= problem.HintArguments.Count, problem.HintKey);
		}

		[Theory]
		[MemberData(nameof(Problems))]
		public void Every_action_has_a_label(Int32 index)
		{
			if (AllProblems().ElementAt(index).Action is { } action)
			{
				Assert.False(String.IsNullOrWhiteSpace(action.Label));
				Assert.NotEqual("Str.Shell.Problem.ActionSettings", action.Label);
			}
		}

		[Fact]
		public void A_missing_tool_is_an_error_with_the_folder_in_its_hint_and_the_compiler_settings_as_action()
		{
			Problem problem = ProblemFactory.FromToolFailure(new ToolFailure(ToolFailureKind.NotFound, @"C:\app\resources\compile.exe"));

			Assert.Equal(ProblemSeverity.Error, problem.Severity);
			Assert.Equal(ProblemSource.Tool, problem.Source);
			Assert.Equal(@"compile.exe was not found: C:\app\resources\compile.exe", problem.Message);
			Assert.Contains(@"C:\app\resources", problem.Hint, StringComparison.Ordinal);
			Assert.Equal(ProblemActionKind.OpenSettings, problem.Action!.Kind);
			Assert.Equal(SettingsPage.Compiler, problem.Action.Page);
			Assert.Equal("Open the settings: Compiler", problem.Action.Label);
			Assert.Equal(ProblemOrigins.BuildTool, problem.Origin);
		}

		[Fact]
		public void A_tool_that_cannot_start_shows_the_reason()
		{
			Problem problem = ProblemFactory.FromToolFailure(new ToolFailure(ToolFailureKind.StartFailed, @"C:\app\resources\compile.exe", "not a valid application"));

			Assert.Equal("compile.exe could not be started: not a valid application", problem.Message);
		}

		[Fact]
		public void The_native_parser_is_only_a_note_when_its_library_is_missing_and_a_warning_when_it_broke()
		{
			Problem missing = ProblemFactory.NativeParser(false, "The native parser was not found");
			Problem broken = ProblemFactory.NativeParser(true, "crashed three times");

			Assert.Equal(ProblemSeverity.Info, missing.Severity);
			Assert.Equal(ProblemSeverity.Warning, broken.Severity);
			Assert.Contains("crashed three times", broken.Message, StringComparison.Ordinal);
			Assert.Equal(SettingsPage.Parser, missing.Action!.Page);
			Assert.Equal(ProblemOrigins.NativeParser, broken.Origin);
		}

		[Fact]
		public void A_save_failure_of_a_read_only_file_explains_the_attribute()
		{
			using TempFolder folder = new TempFolder();
			String path = folder.Write("a.ssl", "x");
			File.SetAttributes(path, FileAttributes.ReadOnly);
			try
			{
				Problem readOnly = ProblemFactory.SaveFailed(path, new UnauthorizedAccessException("denied"), null);
				Problem other = ProblemFactory.SaveFailed(folder.Combine("missing.ssl"), new IOException("disk full"), null);

				Assert.Contains("read-only", readOnly.Hint, StringComparison.Ordinal);
				Assert.DoesNotContain("read-only", other.Hint, StringComparison.Ordinal);
				Assert.Equal(ProblemOrigins.Save, readOnly.Origin);
				Assert.Equal(path, readOnly.File);
			}
			finally
			{
				File.SetAttributes(path, FileAttributes.Normal);
			}
		}

		[Fact]
		public void Settings_problems_are_a_warning_when_the_file_is_damaged_and_an_error_otherwise()
		{
			Problem damaged = ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.Damaged, @"C:\app\settings\settings.json"));
			Problem unreadable = ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.Unreadable, @"C:\app\settings\settings.json", "locked"));
			Problem save = ProblemFactory.FromSettings(new SettingsProblem(SettingsProblemKind.SaveFailed, @"C:\app\settings\settings.json", "disk full"));

			Assert.Equal(ProblemSeverity.Warning, damaged.Severity);
			Assert.Equal(ProblemSeverity.Error, unreadable.Severity);
			Assert.Contains("disk full", save.Message, StringComparison.Ordinal);
			Assert.All(new[] { damaged, unreadable, save }, problem =>
			{
				Assert.Equal(ProblemSource.Settings, problem.Source);
				Assert.Equal(ProblemActionKind.OpenFolder, problem.Action!.Kind);
				Assert.Equal(@"C:\app\settings", problem.Action.FolderPath);
			});
		}

		[Fact]
		public void An_encoding_note_is_information_owned_by_the_document()
		{
			Object document = new Object();

			Problem problem = ProblemFactory.EncodingFallback(@"C:\s\a.ssl", "Western European (Windows)", document);

			Assert.Equal(ProblemSeverity.Info, problem.Severity);
			Assert.Same(document, problem.Owner);
			Assert.Equal("a.ssl is not valid UTF-8 and was read as Western European (Windows).", problem.Message);
			Assert.Equal(SettingsPage.Editor, problem.Action!.Page);
		}

		[Theory]
		[InlineData(BuildErrorType.Error, ProblemSeverity.Error)]
		[InlineData(BuildErrorType.Warning, ProblemSeverity.Warning)]
		[InlineData(BuildErrorType.Message, ProblemSeverity.Info)]
		[InlineData(BuildErrorType.Parser, ProblemSeverity.Info)]
		public void A_log_entry_keeps_its_place_text_and_kind(BuildErrorType type, ProblemSeverity severity)
		{
			BuildError error = new BuildError(type, "text from the tool", @"C:\s\a.ssl", 12, 4, 3);

			Problem problem = Problem.FromBuildError(error, ProblemSource.Build);

			Assert.Equal(severity, problem.Severity);
			Assert.Equal("text from the tool", problem.Message);
			Assert.Equal(@"C:\s\a.ssl", problem.File);
			Assert.Equal(error, problem.ToBuildError());
		}

		[Fact]
		public void A_log_entry_without_a_file_has_no_file()
		{
			Problem problem = Problem.FromBuildError(new BuildError(BuildErrorType.Error, "oops", BuildError.NoFile, 0), ProblemSource.Build);

			Assert.Null(problem.File);
			Assert.Equal(BuildError.NoFile, problem.ToBuildError().FileName);
		}

		private static Int32 CountPlaceholders(String format)
		{
			return Regex.Matches(format, @"\{\d+\}").Select(match => match.Value).Distinct().Count();
		}
	}
}