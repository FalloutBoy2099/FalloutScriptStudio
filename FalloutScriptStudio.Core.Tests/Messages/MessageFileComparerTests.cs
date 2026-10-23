namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;

	public class MessageFileComparerTests
	{
		private static MessageDocument Doc(params String[] lines)
		{
			return MessageDocument.Parse(lines);
		}

		private static MessageComparisonRow Row(MessageComparison comparison, Int32 number)
		{
			return comparison.Rows.Single(row => row.Number == number);
		}

		[Fact]
		public void The_rows_are_the_numbers_of_both_files_in_ascending_order()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{300}{}{Three.}", "{100}{}{One.}", "{200}{}{Two.}"),
				Doc("{200}{}{Deux.}", "{150}{}{Extra.}", "{100}{}{Un.}"));

			Assert.Equal(new[] { 100, 150, 200, 300 }, comparison.Rows.Select(row => row.Number).ToArray());
			Assert.Equal(MessageComparisonStatus.Ok, Row(comparison, 100).Status);
			Assert.Equal(MessageComparisonStatus.ExtraInTarget, Row(comparison, 150).Status);
			Assert.Equal(MessageComparisonStatus.MissingInTarget, Row(comparison, 300).Status);
			Assert.Equal(new[] { 300 }, comparison.GetMissingNumbers());
		}

		[Fact]
		public void A_message_the_target_lacks_or_has_in_excess_has_one_side_only()
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc("{1}{}{Only here.}"), Doc("{2}{}{Only there.}"));

			MessageComparisonRow missing = Row(comparison, 1);
			Assert.NotNull(missing.Reference);
			Assert.Null(missing.Target);
			Assert.Equal("Only here.", missing.ReferenceText);
			Assert.Equal(String.Empty, missing.TargetText);
			MessageComparisonRow extra = Row(comparison, 2);
			Assert.Null(extra.Reference);
			Assert.Equal("Only there.", extra.TargetText);
			Assert.All(comparison.Rows, row => Assert.True(row.NeedsAttention));
		}

		[Fact]
		public void The_text_of_the_reference_is_not_a_translation()
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc("{1}{}{Hello there.}"), Doc("{1}{}{Hello there.}"));

			Assert.Equal(MessageComparisonStatus.SameText, Row(comparison, 1).Status);
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.SameText));
		}

		[Theory]
		[InlineData("%d", "%d")]
		[InlineData("---", "---")]
		[InlineData("100", "100")]
		[InlineData("<br> %s", "<br> %s")]
		public void A_text_without_a_letter_needs_no_translation(String reference, String target)
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc("{1}{}{" + reference + "}"), Doc("{1}{}{" + target + "}"));

			Assert.Equal(MessageComparisonStatus.Ok, Row(comparison, 1).Status);
		}

		[Fact]
		public void Line_breaks_are_not_compared()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{First line", "second line}"),
				Doc("{1}{}{First line", "second line}"));

			Assert.Equal(MessageComparisonStatus.SameText, Row(comparison, 1).Status);
		}

		[Theory]
		[InlineData("You have %d caps.", "You have %s caps.")]
		[InlineData("Hello <name>.", "Hello <nom>.")]
		[InlineData("Hello @PCName@.", "Hello @PCNom@.")]
		[InlineData("Hello  $name$ .", "Hello $nom$.")]
		public void A_text_that_differs_only_by_its_placeholders_or_tags_is_reported_as_such(String reference, String target)
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc("{1}{}{" + reference + "}"), Doc("{1}{}{" + target + "}"));

			Assert.Equal(MessageComparisonStatus.PlaceholdersOnly, Row(comparison, 1).Status);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void An_empty_target_text_is_reported_unless_the_reference_is_empty_too(String target)
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc("{1}{}{Something.}", "{2}{}{}"), Doc("{1}{}{" + target + "}", "{2}{}{" + target + "}"));

			Assert.Equal(MessageComparisonStatus.EmptyInTarget, Row(comparison, 1).Status);
			Assert.Equal(MessageComparisonStatus.Ok, Row(comparison, 2).Status);
			Assert.Equal(target.Length > 0, (Row(comparison, 1).Issues & MessageComparisonIssues.WhitespaceOnly) != 0);
		}

		[Fact]
		public void A_player_tab_or_a_description_that_differs_is_reported_when_the_text_is_translated()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{Good day.}", "\t{2}{}{Good bye.}", "{3}{}{Thanks.}# polite", "{4}{}{Yes.}"),
				Doc("{1}{}{Bonjour.}", "{2}{}{Au revoir.}", "{3}{}{Merci.}# poli", "{4}{}{Oui.}"));

			MessageComparisonRow tab = Row(comparison, 2);
			Assert.Equal(MessageComparisonStatus.PlayerOrDescription, tab.Status);
			Assert.True((tab.Issues & MessageComparisonIssues.PlayerTabDiffers) != 0);
			MessageComparisonRow description = Row(comparison, 3);
			Assert.Equal(MessageComparisonStatus.PlayerOrDescription, description.Status);
			Assert.True((description.Issues & MessageComparisonIssues.DescriptionDiffers) != 0);
			Assert.Equal(MessageComparisonStatus.Ok, Row(comparison, 1).Status);
			Assert.Equal(MessageComparisonStatus.Ok, Row(comparison, 4).Status);
		}

		[Fact]
		public void The_conversions_of_a_translation_may_change_order_but_not_number_or_kind()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{%s gives %d caps.}", "{2}{}{%s gives %d caps.}", "{3}{}{100%% sure, %s.}", "{4}{}{Rate: %02d.}"),
				Doc("{1}{}{%d caps from %s.}", "{2}{}{%s donne.}", "{3}{}{Sûr à 100%%, %s.}", "{4}{}{Taux : %d.}"));

			Assert.Equal(MessageComparisonIssues.None, Row(comparison, 1).Issues);
			Assert.True((Row(comparison, 2).Issues & MessageComparisonIssues.PlaceholderCount) != 0);
			Assert.Equal(MessageComparisonIssues.None, Row(comparison, 3).Issues);
			Assert.Equal(MessageComparisonIssues.None, Row(comparison, 4).Issues);
		}

		[Fact]
		public void Tags_and_variables_that_are_not_kept_are_issues()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{Hi <b>you</b>.}", "{2}{}{Hello @PCName@.}", "{3}{}{Take $item$.}", "{4}{}{Hi <b>you</b>.}"),
				Doc("{1}{}{Salut <b>toi.}", "{2}{}{Bonjour @Nom@.}", "{3}{}{Prends $objet$.}", "{4}{}{Salut <b>toi</b>.}"));

			Assert.True((Row(comparison, 1).Issues & MessageComparisonIssues.TagCount) != 0);
			Assert.True((Row(comparison, 2).Issues & MessageComparisonIssues.TagCount) != 0);
			Assert.True((Row(comparison, 3).Issues & MessageComparisonIssues.VariableCount) != 0);
			Assert.Equal(MessageComparisonIssues.None, Row(comparison, 4).Issues);
		}

		[Fact]
		public void Trailing_spaces_and_odd_characters_of_the_target_are_issues()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{Hello.}", "{2}{}{Hello. }", "{3}{}{Hello.}"),
				Doc("{1}{}{Bonjour. }", "{2}{}{Bonjour. }", "{3}{}{Bonjour \u0081 �.}"));

			Assert.True((Row(comparison, 1).Issues & MessageComparisonIssues.TrailingSpaces) != 0);
			Assert.Equal(MessageComparisonIssues.None, Row(comparison, 2).Issues);
			Assert.True((Row(comparison, 3).Issues & MessageComparisonIssues.SuspiciousCharacters) != 0);
			Assert.Equal(2, comparison.IssueCount);
		}

		[Fact]
		public void The_lines_are_the_physical_lines_of_the_files()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("# comment", "{1}{}{First", "continued}", "", "{2}{}{Second.}"),
				Doc("{2}{}{Seconde.}", "{1}{}{Premier}"));

			Assert.Equal(2, Row(comparison, 1).ReferenceLine);
			Assert.Equal(5, Row(comparison, 2).ReferenceLine);
			Assert.Equal(2, Row(comparison, 1).TargetLine);
			Assert.Equal(1, Row(comparison, 2).TargetLine);
		}

		[Fact]
		public void Duplicate_numbers_and_unrecognized_lines_are_noted_and_the_first_message_is_compared()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{First.}", "{1}{}{Again.}"),
				Doc("{1}{}{Premier.}", "{1}{}{Encore.}", "{2}{}{Deux.}", "garbage line"));

			Assert.Equal("First.", Row(comparison, 1).ReferenceText);
			Assert.Equal("Premier.", Row(comparison, 1).TargetText);
			Assert.Contains(comparison.Notes, note => note.Kind == MessageComparisonNoteKind.DuplicateInReference && note.Count == 1);
			Assert.Contains(comparison.Notes, note => note.Kind == MessageComparisonNoteKind.DuplicateInTarget && note.Count == 1);
			Assert.Contains(comparison.Notes, note => note.Kind == MessageComparisonNoteKind.UnrecognizedLines && note.Count == 1);
		}

		[Fact]
		public void The_counts_add_up_to_the_rows()
		{
			MessageComparison comparison = MessageFileComparer.Compare(
				Doc("{1}{}{Same text.}", "{2}{}{Missing.}", "{3}{}{Fine.}", "{4}{}{Empty.}"),
				Doc("{1}{}{Same text.}", "{3}{}{Bien.}", "{4}{}{}", "{5}{}{Extra.}"));

			Int32 total = Enum.GetValues<MessageComparisonStatus>().Sum(comparison.Count);
			Assert.Equal(comparison.Rows.Count, total);
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.Ok));
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.SameText));
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.MissingInTarget));
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.ExtraInTarget));
			Assert.Equal(1, comparison.Count(MessageComparisonStatus.EmptyInTarget));
		}

		[Fact]
		public void Two_empty_files_compare_to_nothing()
		{
			MessageComparison comparison = MessageFileComparer.Compare(Doc(), Doc());

			Assert.Empty(comparison.Rows);
			Assert.Empty(comparison.Notes);
		}
	}
}