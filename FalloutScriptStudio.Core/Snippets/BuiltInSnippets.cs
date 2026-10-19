namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The snippets that ship with the editor. The user can override one with a snippet of the same trigger, or disable it. None of the
	/// triggers is one of the words of the block completion (<c>if</c>, <c>while</c>, <c>proc</c>...): those keep their own behaviour.
	/// </summary>
	public static class BuiltInSnippets
	{
		/// <summary>The prefix of the localization keys of the descriptions.</summary>
		public const String DescriptionKeyPrefix = "Str.Editor.Snippet.";

		private static readonly Snippet[] _all =
		{
			Create("procedure", "Procedure", "A procedure with its body.", "procedure ${1:name} begin\n\t$0\nend"),
			Create("ifb", "IfBegin", "if ... then begin ... end (also surrounds the selection).", "if (${1:condition}) then begin\n\t${SELECTION}$0\nend"),
			Create("ifelse", "IfElse", "if ... then begin ... end else begin ... end.", "if (${1:condition}) then begin\n\t${SELECTION}$0\nend else begin\n\t${2}\nend"),
			Create("whileb", "WhileBegin", "while ... do begin ... end (also surrounds the selection).", "while (${1:condition}) do begin\n\t${SELECTION}$0\nend"),
			Create("forx", "For", "A for loop (also surrounds the selection).", "for (${1:i} := ${2:0}; ${1:i} < ${3:count}; ${1:i}++) begin\n\t${SELECTION}$0\nend"),
			Create("foreachx", "Foreach", "A foreach loop over an array (also surrounds the selection).", "foreach (variable ${1:item} in ${2:array}) begin\n\t${SELECTION}$0\nend"),
			Create("callp", "Call", "A call of a procedure.", "call ${1:procedure_name}(${2});$0"),
			Create("dispmsg", "DisplayMsg", "display_msg with a text.", "display_msg(${1:\"message\"});$0"),
			Create("talkproc", "TalkProc", "The talk_p_proc procedure of a dialogue, with a reply and an option.", "procedure talk_p_proc begin\n\tReply(${1:100});\n\tNOption(${2:101}, ${3:Node001}, 4);\n\tNLowOption(${4:102}, $3);\nend$0"),
			Create("node", "Node", "A dialogue node: a reply, an option and a low intelligence option.", "procedure ${1:Node001} begin\n\tReply(${2:100});\n\tNOption(${3:101}, ${4:Node999}, 4);\n\tNLowOption(${5:102}, $4);\nend$0"),
			Create("nodeif", "NodeIf", "A dialogue node whose option depends on a condition.", "procedure ${1:Node001} begin\n\tReply(${2:100});\n\tif (${3:true}) then\n\t\tNOption(${4:101}, ${5:Node999}, 4);\n\telse\n\t\tNOption(${6:102}, $5, 4);\nend$0"),
			Create("nodemsg", "NodeMessage", "A dialogue node that only shows a message.", "procedure ${1:Node001} begin\n\tNMessage(${2:100});\nend$0"),
			Create("vars", "Variables", "Two variables with initial values.", "variable ${1:first} := ${2:0};\nvariable ${3:second} := ${4:0};\n$0"),
			Create("def", "Define", "A #define macro.", "#define ${1:NAME} ${2:value}$0"),
		};

		/// <summary>Gets the built-in snippets.</summary>
		public static IReadOnlyList<Snippet> All => _all;

		private static Snippet Create(String trigger, String key, String description, String body)
		{
			return new Snippet(trigger, description, body) { DescriptionKey = DescriptionKeyPrefix + key, Source = SnippetSource.BuiltIn };
		}
	}
}