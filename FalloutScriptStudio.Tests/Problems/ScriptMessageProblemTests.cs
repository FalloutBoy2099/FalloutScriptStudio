namespace FalloutScriptStudio.Tests.Problems
{
	using System;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Problems;
	using Xunit;

	public sealed class ScriptMessageProblemTests
	{
		private const String Script = @"C:\mod\scripts\npc.ssl";
		private const String Messages = @"C:\mod\text\english\dialog\npc.msg";

		private static Problem Create(ScriptMessageFinding finding, String? messageFile = Messages, Int32[]? missing = null)
		{
			return ProblemFactory.ScriptMessage(finding, Script, messageFile, missing ?? new[] { 101, 110 }, 1000, new Object());
		}

		[Fact]
		public void A_missing_message_points_to_the_script_and_offers_to_add_every_missing_number()
		{
			Problem problem = Create(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 110, ScriptMessageUsage.OwnFile, true, 12, 9, 3));

			Assert.Equal(ProblemSeverity.Error, problem.Severity);
			Assert.Equal(ProblemSource.Message, problem.Source);
			Assert.Equal(ProblemOrigins.MessageConsistency, problem.Origin);
			Assert.Equal((Script, 12, 9, 3), (problem.File, problem.Line, problem.Column, problem.Length));
			Assert.Equal("Message 110 is used by the script but is not in the message file npc.msg.", problem.Message);
			Assert.Equal(ProblemActionKind.AddMissingMessages, problem.Action!.Kind);
			Assert.Equal(new[] { 101, 110 }, problem.Action.Numbers);
			Assert.Equal(Script, problem.Action.FilePath);
			Assert.Equal("Add the missing messages to the message file", problem.Action.Label);
		}

		[Fact]
		public void A_message_of_another_message_file_names_the_file_number_and_has_no_fix()
		{
			Problem problem = Create(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 18, 101, true, 394, 31, 2));

			Assert.Equal("Message 18 is used by the script but is not in the message file #101.", problem.Message);
			Assert.Null(problem.Action);
		}

		[Theory]
		[InlineData(ScriptMessageFindingKind.UnusedMessage, ProblemSeverity.Info, "Message 7 of the message file npc.msg is never used by the script.")]
		[InlineData(ScriptMessageFindingKind.DuplicateMessage, ProblemSeverity.Error, "Message number 7 appears more than once in the message file npc.msg.")]
		[InlineData(ScriptMessageFindingKind.EmptyMessage, ProblemSeverity.Warning, "Message 7 is used by the script but its text in the message file npc.msg is empty.")]
		[InlineData(ScriptMessageFindingKind.MissingFemaleVariant, ProblemSeverity.Info, "Message 7 has no female variant (1007) in the message file npc.msg.")]
		public void The_problems_of_the_message_file_point_to_its_line(ScriptMessageFindingKind kind, ProblemSeverity severity, String message)
		{
			ScriptMessageSeverity core = severity switch
			{
				ProblemSeverity.Error => ScriptMessageSeverity.Error,
				ProblemSeverity.Warning => ScriptMessageSeverity.Warning,
				_ => ScriptMessageSeverity.Info,
			};

			Problem problem = Create(new ScriptMessageFinding(kind, core, 7, ScriptMessageUsage.OwnFile, false, 33, 1, -1));

			Assert.Equal(severity, problem.Severity);
			Assert.Equal(Messages, problem.File);
			Assert.Equal(33, problem.Line);
			Assert.Equal(message, problem.Message);
			Assert.Null(problem.Action);
		}

		[Fact]
		public void Without_a_message_file_the_problem_offers_to_create_or_associate_it()
		{
			Problem problem = Create(new ScriptMessageFinding(ScriptMessageFindingKind.MessageFileMissing, ScriptMessageSeverity.Warning, 0, ScriptMessageUsage.OwnFile, false, -1, -1, -1, 5), null);

			Assert.Equal(ProblemSeverity.Warning, problem.Severity);
			Assert.Equal(Script, problem.File);
			Assert.Equal("npc.ssl uses 5 message(s) but has no message file.", problem.Message);
			Assert.Equal(ProblemActionKind.CreateMessageFile, problem.Action!.Kind);
			Assert.Equal(Script, problem.Action.FilePath);
		}

		[Fact]
		public void Two_findings_at_different_places_are_two_problems()
		{
			Problem first = Create(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 110, ScriptMessageUsage.OwnFile, true, 12, 9, 3));
			Problem second = Create(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 110, ScriptMessageUsage.OwnFile, true, 20, 9, 3));

			Assert.NotEqual(first.Identity, second.Identity);
			Assert.Equal(first.Identity, Create(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, 110, ScriptMessageUsage.OwnFile, true, 12, 9, 3)).Identity);
		}
	}
}