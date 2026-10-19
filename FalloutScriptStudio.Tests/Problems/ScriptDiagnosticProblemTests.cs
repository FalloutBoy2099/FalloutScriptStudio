namespace FalloutScriptStudio.Tests.Problems
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The problems made from the findings of the semantic analysis and the texts of every rule.</summary>
	public sealed class ScriptDiagnosticProblemTests
	{
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static String RepositoryRoot()
		{
			DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FalloutScriptStudio.sln")))
			{
				directory = directory.Parent;
			}

			return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found.");
		}

		private static HashSet<String> Keys(String language)
		{
			String file = Path.Combine(RepositoryRoot(), "FalloutScriptStudio", "Localization", "Strings", "Editor." + language + ".xaml");
			return XDocument.Load(file).Root!.Elements().Select(element => (String?)element.Attribute(XamlNamespace + "Key")).Where(key => key is not null).Select(key => key!).ToHashSet(StringComparer.Ordinal);
		}

		[Fact]
		public void Every_rule_has_a_message_and_a_hint_in_every_language()
		{
			List<String> missing = [];
			foreach (LanguageInfo language in Loc.Languages)
			{
				HashSet<String> keys = Keys(language.Code);
				foreach (ScriptDiagnosticRule rule in ScriptDiagnosticCodes.All)
				{
					foreach (String key in new[] { rule.MessageKey, rule.HintKey })
					{
						if (!keys.Contains(key))
						{
							missing.Add(language.Code + ": " + key);
						}
					}
				}

				foreach (String key in new[] { "Str.Editor.Fix.Rename", "Str.Editor.Fix.ForwardDeclare", "Str.Editor.Fix.RemoveDeclaration", "Str.Editor.Fix.Generic", "Str.Editor.Diag.QuickFixHint", "Str.Editor.Cmd.QuickFix" })
				{
					if (!keys.Contains(key))
					{
						missing.Add(language.Code + ": " + key);
					}
				}
			}

			Assert.Empty(missing);
		}

		[Fact]
		public void The_messages_and_hints_of_every_finding_of_the_table_are_formatted_without_a_placeholder_left()
		{
			Int32 checkedFindings = 0;
			foreach (ScriptDiagnosticCase item in ScriptDiagnosticCase.All)
			{
				foreach (ScriptDiagnostic diagnostic in ScriptDiagnostics.Analyze(item.Crlf, new ScriptEnvironment { Functions = DiagnosticsFixture.ShippedCatalog }))
				{
					Problem problem = ProblemFactory.ScriptDiagnostic(diagnostic, @"C:\s\a.ssl", new Object());

					Assert.True(problem.Message.Length > 0 && !problem.Message.Contains('{', StringComparison.Ordinal) && !problem.Message.StartsWith("Str.", StringComparison.Ordinal), item.Name + ": " + problem.Message);
					Assert.True(problem.Hint is { Length: > 0 } hint && !hint.Contains('{', StringComparison.Ordinal) && !hint.StartsWith("Str.", StringComparison.Ordinal), item.Name + ": " + problem.Hint);
					checkedFindings++;
				}
			}

			Assert.True(checkedFindings >= 50, "findings: " + checkedFindings);
		}

		[Fact]
		public void A_finding_becomes_a_problem_of_the_parser_with_its_place_hint_and_fix()
		{
			Object owner = new Object();
			ScriptDiagnostic diagnostic = new ScriptDiagnostic("SSL1002", ScriptDiagnosticSeverity.Error, "Str.Editor.Diag.UndefinedProcedure", new[] { "display_msgg" }, 40, 12, 7, 5)
			{
				Fix = new ScriptQuickFix(ScriptQuickFix.RenameId, "display_msg", 40, 12),
			};

			Problem problem = ProblemFactory.ScriptDiagnostic(diagnostic, @"C:\s\a.ssl", owner);

			Assert.Equal(ProblemSeverity.Error, problem.Severity);
			Assert.Equal(ProblemSource.Parse, problem.Source);
			Assert.Equal(ProblemOrigins.LiveDiagnostics, problem.Origin);
			Assert.Same(owner, problem.Owner);
			Assert.Equal(@"C:\s\a.ssl", problem.File);
			Assert.Equal((7, 5, 12), (problem.Line, problem.Column, problem.Length));
			Assert.Equal("Unknown name 'display_msgg': no procedure, macro or function has this name.", problem.Message);
			Assert.Equal("Str.Editor.Diag.UndefinedProcedureHint", problem.HintKey);
			Assert.Equal(ProblemActionKind.ApplyQuickFix, problem.Action!.Kind);
			Assert.Equal(@"C:\s\a.ssl", problem.Action.FilePath);
			Assert.Equal((7, 5), (problem.Action.Line, problem.Action.Column));
			Assert.Equal("Did you mean 'display_msg'?", problem.Action.Label);
		}

		[Theory]
		[InlineData(ScriptDiagnosticSeverity.Error, ProblemSeverity.Error)]
		[InlineData(ScriptDiagnosticSeverity.Warning, ProblemSeverity.Warning)]
		[InlineData(ScriptDiagnosticSeverity.Info, ProblemSeverity.Info)]
		public void The_severity_of_a_finding_is_the_severity_of_the_problem(ScriptDiagnosticSeverity severity, ProblemSeverity expected)
		{
			ScriptDiagnostic diagnostic = new ScriptDiagnostic("SSL2001", severity, "Str.Editor.Diag.FunctionArgumentCount", new[] { "a", "1", "2" }, 0, 1, 1, 1);

			Problem problem = ProblemFactory.ScriptDiagnostic(diagnostic, null, null);

			Assert.Equal(expected, problem.Severity);
			Assert.Null(problem.Action);
			Assert.Null(problem.File);
		}

		[Fact]
		public void A_finding_in_a_script_that_was_never_saved_has_no_action_because_there_is_no_file_to_fix()
		{
			ScriptDiagnostic diagnostic = new ScriptDiagnostic("SSL1002", ScriptDiagnosticSeverity.Error, "Str.Editor.Diag.UndefinedProcedure", new[] { "x" }, 0, 1, 1, 1) { Fix = new ScriptQuickFix(ScriptQuickFix.RenameId, "y", 0, 1) };

			Assert.Null(ProblemFactory.ScriptDiagnostic(diagnostic, null, null).Action);
		}

		[Theory]
		[InlineData(ScriptQuickFix.RenameId, "display_msg", "Did you mean 'display_msg'?")]
		[InlineData(ScriptQuickFix.ForwardDeclareId, "procedure foo;", "Add the forward declaration procedure foo;")]
		[InlineData(ScriptQuickFix.RemoveDeclarationId, "statement", "Remove the repeated declaration")]
		[InlineData("unknown", "", "Apply the quick fix")]
		public void A_fix_is_described_in_words(String id, String argument, String expected)
		{
			Assert.Equal(expected, FalloutScriptStudio.Editor.Text.ScriptFixText.Describe(id, argument));
		}
	}
}