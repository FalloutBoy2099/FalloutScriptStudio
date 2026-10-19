namespace FalloutScriptStudio.Tests.Shell.Support
{
	using FalloutScriptStudio.Core.Dialogue;

	/// <summary>A dialogue rules source with the macros of a typical header (<c>NMessage</c>, <c>Reply</c>, <c>NOption</c>, <c>NLowOption</c>).</summary>
	internal sealed class FixedDialogueRules : IDialogueRulesSource
	{
		public DialogueFunctionRules Current { get; set; } = new DialogueFunctionRules(new[]
		{
			DialogueFunctionRule.FromOneBased(DialogueOpcode.Message, "NMessage", messageArg: 1, totalArgs: 1),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Reply", messageArg: 1, totalArgs: 1),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
			DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NLowOption", messageArg: 1, nodeArg: 2, totalArgs: 2),
		});
	}
}