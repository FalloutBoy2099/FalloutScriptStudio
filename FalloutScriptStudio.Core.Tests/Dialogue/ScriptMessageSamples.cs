namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.TextTools;

	/// <summary>Scripts, rules and message files for the tests of the script / message file consistency check.</summary>
	internal static class ScriptMessageSamples
	{
		public const String ScriptPath = @"C:\mod\scripts\npc.ssl";

		/// <summary>The header lines every sample script starts with: the script number, a base number and the macros of a typical header.</summary>
		public static readonly String[] Header =
		{
			"#define NAME SCRIPT_NPC",
			"#define SCRIPT_NPC 123",
			"#define MSG_BASE 200",
			"#define MSG(x) (MSG_BASE + x)",
			"#define mstr(x) message_str(NAME, x)",
			"#define floater(x) float_msg(self_obj, mstr(x), 0)",
		};

		/// <summary>The rules of a typical header: <c>NMessage(msg)</c>, <c>Reply(msg)</c>, <c>NOption(msg, node, iq)</c>, <c>NLowOption(msg, node)</c>.</summary>
		public static DialogueFunctionRules Rules()
		{
			return new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Message, "NMessage", messageArg: 1, totalArgs: 1),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Reply", messageArg: 1, totalArgs: 1),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "NLowOption", messageArg: 1, nodeArg: 2, totalArgs: 2),
			});
		}

		/// <summary>Builds a script: the header, then the given lines.</summary>
		public static String Script(params String[] body)
		{
			return TestScripts.Crlf(Header.Concat(body).ToArray());
		}

		public static ProgramInfo Info(String text)
		{
			SortedDictionary<String, ScriptMacro> macros = new MacroCollector(new IncludeResolver(null, null)).CollectFromText(text, ScriptPath, false);
			return new InternalParser().Parse(text, ScriptPath).WithScriptMacros(macros);
		}

		public static MessageDocument Messages(params String[] lines)
		{
			return MessageDocument.Parse(lines);
		}

		public static ScriptMessageScan Scan(String script, DialogueFunctionRules? rules = null)
		{
			return ScriptMessageScanner.Scan(script, Info(script), rules ?? Rules());
		}

		public static ScriptMessageReport Check(String script, MessageDocument? messages, DialogueFunctionRules? rules = null, IReadOnlyDictionary<Int32, MessageDocument>? others = null, ScriptMessageOptions? options = null)
		{
			return ScriptMessageConsistency.Check(script, Info(script), rules ?? Rules(), messages, others, options);
		}
	}
}