namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;

	public class MessagePlaceholdersTests
	{
		[Theory]
		[InlineData("", "", MessageComparisonIssues.None)]
		[InlineData("Hello.", "Bonjour.", MessageComparisonIssues.None)]
		[InlineData("%s", "%s", MessageComparisonIssues.None)]
		[InlineData("%s and %d", "%d et %s", MessageComparisonIssues.None)]
		[InlineData("%s", "%d", MessageComparisonIssues.PlaceholderCount)]
		[InlineData("%s", "", MessageComparisonIssues.PlaceholderCount)]
		[InlineData("", "%s", MessageComparisonIssues.PlaceholderCount)]
		[InlineData("%s %s", "%s", MessageComparisonIssues.PlaceholderCount)]
		[InlineData("%02d", "%d", MessageComparisonIssues.None)]
		[InlineData("%-5.2f", "%f", MessageComparisonIssues.None)]
		[InlineData("%ld", "%d", MessageComparisonIssues.None)]
		[InlineData("100%% of %s", "100%% de %s", MessageComparisonIssues.None)]
		[InlineData("100%% of %s", "100% de %s", MessageComparisonIssues.None)]
		[InlineData("50% done", "50% fait", MessageComparisonIssues.None)]
		[InlineData("<a>", "<a><b>", MessageComparisonIssues.TagCount)]
		[InlineData("<Sigh>", "<Soupir>", MessageComparisonIssues.None)]
		[InlineData("@PCName@ is here", "@pcname@ est ici", MessageComparisonIssues.None)]
		[InlineData("@PCName@ is here", "@Nom@ est ici", MessageComparisonIssues.TagCount)]
		[InlineData("$item$", "$item$", MessageComparisonIssues.None)]
		[InlineData("$item$", "$objet$", MessageComparisonIssues.VariableCount)]
		[InlineData("cost: $5", "coût : 5 $", MessageComparisonIssues.None)]
		[InlineData("%s <b>", "%d <b> $x$", MessageComparisonIssues.PlaceholderCount | MessageComparisonIssues.VariableCount)]
		public void The_placeholders_of_two_texts_are_compared(String reference, String target, MessageComparisonIssues expected)
		{
			Assert.Equal(expected, MessagePlaceholders.Compare(reference, target));
		}

		[Theory]
		[InlineData("Hello %s, you have %d caps.", "Hello , you have caps.")]
		[InlineData("Hello <name>  and   @who@ with $v$", "Hello and with")]
		[InlineData("  spaced\r\n text  ", "spaced text")]
		[InlineData("100%% sure", "100 sure")]
		[InlineData("%s", "")]
		[InlineData("Plain", "Plain")]
		public void Stripping_removes_the_placeholders_and_collapses_the_white_space(String text, String expected)
		{
			Assert.Equal(expected, MessagePlaceholders.Strip(text));
		}
	}
}