namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using FalloutScriptStudio.Core.Analysis;
	using Xunit;

	public class NameSuggesterTests
	{
		[Theory]
		[InlineData("kitten", "kitten", 0)]
		[InlineData("kitten", "sitting", 3)]
		[InlineData("abc", "acb", 1)]
		[InlineData("display_msg", "display_msgg", 1)]
		[InlineData("display_msg", "Display_Msg", 0)]
		[InlineData("", "abc", 3)]
		public void The_distance_counts_insertions_deletions_replacements_and_swaps_ignoring_the_case(String a, String b, Int32 expected)
		{
			Assert.Equal(expected, NameSuggester.GetDistance(a, b, 10));
			Assert.Equal(expected, NameSuggester.GetDistance(b, a, 10));
		}

		[Fact]
		public void A_distance_above_the_limit_is_only_known_to_be_above_it()
		{
			Assert.True(NameSuggester.GetDistance("abcdefgh", "zzzzzzzz", 2) > 2);
			Assert.True(NameSuggester.GetDistance("a", "abcdefgh", 2) > 2);
		}

		[Theory]
		[InlineData(3, 0)]
		[InlineData(4, 1)]
		[InlineData(7, 1)]
		[InlineData(8, 2)]
		[InlineData(11, 2)]
		[InlineData(12, 3)]
		[InlineData(40, 3)]
		public void The_number_of_edits_allowed_grows_with_the_length(Int32 length, Int32 expected)
		{
			Assert.Equal(expected, NameSuggester.GetMaximumDistance(length));
		}

		[Fact]
		public void The_nearest_candidate_wins_and_the_first_one_of_equal_distance()
		{
			String[] candidates = { "get_critter_state", "get_critter_stat", "get_critter_stats" };

			Assert.Equal("get_critter_stat", NameSuggester.Suggest("get_critter_sta", candidates));
			Assert.Equal("get_critter_state", NameSuggester.Suggest("get_critter_statt", new[] { "get_critter_state", "get_critter_stats" }));
		}

		[Fact]
		public void A_name_with_nothing_near_or_too_short_gets_no_suggestion()
		{
			Assert.Null(NameSuggester.Suggest("zzzzzzzz", new[] { "display_msg", "dude_obj" }));
			Assert.Null(NameSuggester.Suggest("dsp", new[] { "dsp1", "dsa" }));
			Assert.Null(NameSuggester.Suggest("display_msg", Array.Empty<String>()));
		}

		[Fact]
		public void The_same_name_in_another_case_is_not_a_suggestion()
		{
			Assert.Null(NameSuggester.Suggest("display_msg", new[] { "DISPLAY_MSG" }));
		}

		[Fact]
		public void A_swap_of_two_letters_is_one_edit()
		{
			Assert.Equal("dude_obj", NameSuggester.Suggest("dude_ojb", new[] { "dude_obj" }));
		}
	}
}