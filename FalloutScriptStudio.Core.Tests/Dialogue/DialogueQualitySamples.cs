namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Scripts and helpers for the tests of <see cref="DialogueQualityChecker"/>.</summary>
	internal static class DialogueQualitySamples
	{
		/// <summary>The rules of a typical header: the five option macros of <c>dialog.h</c>, <c>Reply</c> and <c>NMessage</c>.</summary>
		public static DialogueFunctionRules Rules()
		{
			return new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Message, "NMessage", messageArg: 1, totalArgs: 1),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Reply", messageArg: 1, totalArgs: 1),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "GOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "BOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NLowOption", messageArg: 1, nodeArg: 2, totalArgs: 2),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "BLowOption", messageArg: 1, nodeArg: 2, totalArgs: 2),
			});
		}

		/// <summary>Builds a script: the usual header (<see cref="ScriptMessageSamples.Header"/>), then the given lines.</summary>
		public static String Script(params String[] body)
		{
			return ScriptMessageSamples.Script(body);
		}

		/// <summary>Checks a script.</summary>
		public static DialogueQualityReport Check(String script, DialogueQualityOptions? options = null, ScriptMessageReport? messages = null)
		{
			ProgramInfo info = ScriptMessageSamples.Info(script);
			return DialogueQualityChecker.Check(script, info, new DialogueNodeParser(Rules()), options, messages);
		}

		/// <summary>Gets the 1-based line of the first line of the script that contains a text.</summary>
		public static Int32 LineOf(String script, String text, Int32 occurrence = 1)
		{
			String[] lines = script.Split("\r\n");
			Int32 seen = 0;
			for (Int32 i = 0; i < lines.Length; i++)
			{
				if (lines[i].Contains(text, StringComparison.Ordinal) && ++seen == occurrence)
				{
					return i + 1;
				}
			}

			throw new InvalidOperationException("The script has no line with: " + text);
		}

		/// <summary>The dialogue the tests start from: nothing in it is wrong.</summary>
		public static String[] WellFormed()
		{
			return new[]
			{
				"procedure talk_p_proc begin",
				"   Reply(100);",
				"   NOption(101, Node001, 4);",
				"   NLowOption(102, Node999);",
				"end",
				"procedure Node001 begin",
				"   Reply(110);",
				"   NOption(111, Node002, 4);",
				"   GOption(112, Node999, 5);",
				"   NLowOption(113, Node999);",
				"end",
				"procedure Node002 begin",
				"   NMessage(120);",
				"end",
				"procedure Node999 begin",
				"end",
			};
		}

		/// <summary>Gets the well formed dialogue with more lines added before the procedure <c>Node999</c>.</summary>
		public static String[] WithNodes(params String[] extra)
		{
			String[] well = WellFormed();
			Int32 index = Array.IndexOf(well, "procedure Node999 begin");
			return well.Take(index).Concat(extra).Concat(well.Skip(index)).ToArray();
		}
	}
}