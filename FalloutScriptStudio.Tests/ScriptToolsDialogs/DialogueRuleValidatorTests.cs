namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using Xunit;

	public sealed class DialogueRuleValidatorTests
	{
		[Theory]
		[InlineData("NOption", true)]
		[InlineData("_x1", true)]
		[InlineData("1abc", false)]
		[InlineData("a b", false)]
		[InlineData("a,b", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void IsValidName_AcceptsOnlyIdentifiers(String? name, Boolean expected)
		{
			Assert.Equal(expected, DialogueRuleValidator.IsValidName(name));
		}

		[Fact]
		public void ValidateName_RejectsEmptyAndBuiltIn()
		{
			Assert.NotNull(DialogueRuleValidator.ValidateName("  "));
			Assert.NotNull(DialogueRuleValidator.ValidateName("GSAY_OPTION"));
			Assert.Null(DialogueRuleValidator.ValidateName(" NOption "));
		}

		[Theory]
		[InlineData("Option", 3, 1, 2, 3, 0)]
		[InlineData("Option", 2, 1, 2, 0, 0)]
		[InlineData("Reply", 1, 1, 0, 0, 0)]
		[InlineData("Message", 2, 2, 0, 0, 1)]
		[InlineData("Call", 1, 0, 1, 0, 0)]
		public void ValidateArguments_AcceptsConsistentRules(String kind, Int32 total, Int32 message, Int32 node, Int32 iq, Int32 file)
		{
			Assert.Null(DialogueRuleValidator.ValidateArguments(kind, total, message, node, iq, file));
		}

		[Theory]
		[InlineData("Bogus", 2, 1, 2, 0, 0)]
		[InlineData("Option", 0, 1, 1, 0, 0)]
		[InlineData("Option", 33, 1, 2, 0, 0)]
		[InlineData("Option", 2, 3, 1, 0, 0)]
		[InlineData("Option", 2, -1, 1, 0, 0)]
		[InlineData("Option", 2, 0, 1, 0, 0)]
		[InlineData("Reply", 2, 0, 0, 0, 0)]
		[InlineData("Option", 2, 1, 0, 0, 0)]
		[InlineData("Call", 2, 0, 0, 0, 0)]
		[InlineData("Option", 3, 1, 1, 0, 0)]
		[InlineData("Reply", 3, 2, 0, 0, 2)]
		public void ValidateArguments_RejectsInconsistentRules(String kind, Int32 total, Int32 message, Int32 node, Int32 iq, Int32 file)
		{
			Assert.NotNull(DialogueRuleValidator.ValidateArguments(kind, total, message, node, iq, file));
		}
	}
}
