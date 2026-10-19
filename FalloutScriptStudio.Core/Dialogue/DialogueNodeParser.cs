namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Parsing;

	/// <summary>
	/// Default <see cref="IDialogueNodeParser"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The statements are found the way the legacy editor found them: by searching the keywords <c>Reply</c>, <c>Message</c>
	/// and <c>Option</c> (as part of any function name such as <c>NOption</c> or <c>gsay_reply</c>), the names of the user's
	/// rules and the <c>call</c> keyword, then reading the arguments with the help of the rules and of the macros of the script.
	/// </para>
	/// <para>
	/// Fixes of the legacy behaviour: user rules are only searched by the pass of their own kind (messages were reported twice),
	/// the node of a macro call and of <c>call Node in 5;</c> is cleaned to a bare name, <c>MSG_BASE + 5</c> sums macros, a
	/// self-referencing macro cannot loop forever, a malformed statement is skipped instead of throwing, and statements in a
	/// trailing <c>//</c> comment are ignored when comments are excluded.
	/// </para>
	/// </remarks>
	public sealed class DialogueNodeParser : IDialogueNodeParser
	{
		private const Int32 TabWidth = 3;

		private static readonly Regex _callRegex = new Regex(@"\bcall\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private readonly IDialogueRulesSource _rules;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueNodeParser"/> class.
		/// </summary>
		/// <param name="rules">Supplies the dialogue function rules in effect.</param>
		public DialogueNodeParser(IDialogueRulesSource rules)
		{
			ArgumentNullException.ThrowIfNull(rules);
			this._rules = rules;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueNodeParser"/> class with fixed rules.
		/// </summary>
		/// <param name="rules">The dialogue function rules.</param>
		public DialogueNodeParser(DialogueFunctionRules rules)
			: this(new FixedRulesSource(rules))
		{
		}

		/// <inheritdoc />
		public IReadOnlyList<String> GetNodeProcedureNames(IEnumerable<ScriptProcedure> procedures)
		{
			ArgumentNullException.ThrowIfNull(procedures);
			return procedures.Where(p => IsNodeName(p.NormalizedName)).Select(p => p.Name).ToList();
		}

		/// <inheritdoc />
		public Boolean IsNodeProcedureName(String procedureName)
		{
			ArgumentNullException.ThrowIfNull(procedureName);
			return IsNodeName(procedureName.ToLowerInvariant());
		}

		/// <inheritdoc />
		public IReadOnlyList<DialogueLine> ParseNode(String nodeCode, ProgramInfo info, DialogueNodeParseOptions options)
		{
			ArgumentNullException.ThrowIfNull(nodeCode);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(options);

			DialogueFunctionRules rules = this._rules.Current;
			String[] lines = nodeCode.Split('\n');
			var comments = new CommentTracker();
			var result = new List<DialogueLine>();

			for (Int32 index = 0; index < lines.Length; index++)
			{
				String line = lines[index].TrimEnd();
				if (line.Length == 0)
				{
					continue;
				}

				String code = line;
				if (options.ExcludeComments || !options.IncludePlainCode)
				{
					String probe = line.TrimStart();
					if (comments.ShouldSkip(ref probe))
					{
						continue;
					}

					if (options.ExcludeComments)
					{
						code = RemoveTrailingComment(probe);
					}
				}

				Int32 before = result.Count;

				ScanNpcText(result, rules, info, code, line, index, DialogueOpcode.Message);
				ScanNpcText(result, rules, info, code, line, index, DialogueOpcode.Reply);
				ScanOptions(result, rules, info, code, line, index);
				ScanMacroCalls(result, rules, info, code, line, index);
				ScanCalls(result, rules, info, code, line, index);

				if (options.IncludePlainCode && before == result.Count)
				{
					result.Add(DialogueLineBuilder.CreatePlainLine(line, index));
				}
			}

			return result;
		}

		/// <inheritdoc />
		public IReadOnlyList<DialogueLine> ParseNodeForDiagram(String nodeCode, ProgramInfo info, Boolean excludeComments)
		{
			ArgumentNullException.ThrowIfNull(nodeCode);
			ArgumentNullException.ThrowIfNull(info);

			String[] lines = nodeCode.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			for (Int32 i = 0; i < lines.Length; i++)
			{
				lines[i] = SplitElseBranches(lines[i].TrimEnd().Replace("\t", new String(' ', TabWidth), StringComparison.Ordinal), excludeComments);
			}

			return this.ParseNode(
				String.Join("\n", lines),
				info,
				new DialogueNodeParseOptions { IncludePlainCode = true, ExcludeComments = excludeComments });
		}

		private static Boolean IsNodeName(String lowerCaseName)
		{
			return DialogueNodeRule.IsNode(lowerCaseName);
		}

		/// <summary>Puts <c>else</c> that follows a statement on the same line on a line of its own, with the indentation of the statement.</summary>
		private static String SplitElseBranches(String line, Boolean excludeComments)
		{
			Int32 n = 0;
			while (n >= 0)
			{
				n = line.IndexOf(';', n);
				if (n < 0)
				{
					break;
				}

				if (++n >= line.Length)
				{
					break;
				}

				Int32 elseIndex = line.IndexOf("else", n, StringComparison.Ordinal);
				if (excludeComments && elseIndex != -1 && line.Contains("//", StringComparison.Ordinal))
				{
					elseIndex = -1;
				}

				if (elseIndex < 0)
				{
					break;
				}

				Int32 indent = 0;
				while (indent < line.Length && Char.IsWhiteSpace(line[indent]))
				{
					indent++;
				}

				String insertion = Environment.NewLine + new String(' ', indent);
				line = line.Insert(elseIndex, insertion);
				n = elseIndex + insertion.Length + "else".Length;
			}

			return line;
		}

		private static String RemoveTrailingComment(String line)
		{
			Boolean inString = false;
			for (Int32 i = 0; i < line.Length; i++)
			{
				Char c = line[i];
				if (c == '"')
				{
					inString = !inString;
				}
				else if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
				{
					return line.Substring(0, i).TrimEnd();
				}
			}

			return line;
		}

		private static void ScanNpcText(List<DialogueLine> result, DialogueFunctionRules rules, ProgramInfo info, String code, String original, Int32 lineIndex, DialogueOpcode kind)
		{
			String keyword = kind == DialogueOpcode.Message ? "Message" : "Reply";
			Int32 n = 0;
			do
			{
				DialogueOpcode opcode = kind;
				Int32 from = n;
				n = code.IndexOf(keyword, from, StringComparison.OrdinalIgnoreCase);
				if (n == -1)
				{
					n = FindUserRule(rules, code, from, kind);
				}

				if (n <= -1)
				{
					continue;
				}

				String? name = DialogueLineBuilder.GetOpcodeName(code, ref n);
				if (name is null)
				{
					break;
				}

				String lower = name.ToLowerInvariant();
				if (kind == DialogueOpcode.Message && lower == "message_str")
				{
					break;
				}

				if (lower is "gsay_reply" or "gsay_message")
				{
					opcode = kind == DialogueOpcode.Reply ? DialogueOpcode.GsayReply : DialogueOpcode.GsayMessage;
				}

				if (n + 2 < code.Length)
				{
					Add(result, rules, info, opcode, name, code, original, n, lineIndex, out n);
				}
			}
			while (n > -1 && n < code.Length);
		}

		private static void ScanOptions(List<DialogueLine> result, DialogueFunctionRules rules, ProgramInfo info, String code, String original, Int32 lineIndex)
		{
			Int32 n = 0;
			do
			{
				DialogueOpcode opcode = DialogueOpcode.Option;
				Int32 from = n;
				n = code.IndexOf("Option", from, StringComparison.OrdinalIgnoreCase);
				if (n == -1)
				{
					n = FindUserRule(rules, code, from, DialogueOpcode.Option);
				}

				if (n <= -1)
				{
					continue;
				}

				String? name = DialogueLineBuilder.GetOpcodeName(code, ref n);
				if (name is null)
				{
					break;
				}

				String lower = name.ToLowerInvariant();
				if (lower == "gsay_option")
				{
					opcode = DialogueOpcode.GsayOption;
				}
				else if (lower == "giq_option")
				{
					opcode = DialogueOpcode.GiqOption;
				}

				Add(result, rules, info, opcode, name, code, original, n, lineIndex, out n);
			}
			while (n > -1 && n < code.Length);
		}

		private static void ScanMacroCalls(List<DialogueLine> result, DialogueFunctionRules rules, ProgramInfo info, String code, String original, Int32 lineIndex)
		{
			Int32 n = 0;
			do
			{
				Int32 from = n;
				n = FindUserRule(rules, code, from, DialogueOpcode.CallMacro);
				if (n <= -1)
				{
					continue;
				}

				String? name = DialogueLineBuilder.GetOpcodeName(code, ref n);
				if (name is null)
				{
					break;
				}

				Add(result, rules, info, DialogueOpcode.CallMacro, name, code, original, n, lineIndex, out n);
			}
			while (n > -1 && n < code.Length);
		}

		private static void ScanCalls(List<DialogueLine> result, DialogueFunctionRules rules, ProgramInfo info, String code, String original, Int32 lineIndex)
		{
			foreach (Match match in _callRegex.Matches(code))
			{
				Add(result, rules, info, DialogueOpcode.Call, null, code, original, match.Index + 4, lineIndex, out _);
			}
		}

		/// <summary>Finds the first occurrence in the code, from an index, of the name of a user rule of a kind (whatever the order of the rules).</summary>
		private static Int32 FindUserRule(DialogueFunctionRules rules, String code, Int32 from, DialogueOpcode kind)
		{
			Int32 first = -1;
			foreach (DialogueFunctionRule rule in rules.Rules)
			{
				if (rule.IsDefault || rule.Opcode != kind)
				{
					continue;
				}

				Int32 index = code.IndexOf(rule.Name, from, StringComparison.OrdinalIgnoreCase);
				if (index != -1 && (first == -1 || index < first))
				{
					first = index;
				}
			}

			return first;
		}

		private static void Add(
			List<DialogueLine> result,
			DialogueFunctionRules rules,
			ProgramInfo info,
			DialogueOpcode opcode,
			String? name,
			String code,
			String original,
			Int32 offset,
			Int32 lineIndex,
			out Int32 next)
		{
			DialogueLine? line = DialogueLineBuilder.TryParse(rules, info, opcode, name, code, offset, lineIndex, out DialogueLineBuilder builder);
			next = builder.NextPosition;
			if (line is not null)
			{
				result.Add(new DialogueLine
				{
					Opcode = line.Opcode,
					Code = original,
					CodeLineIndex = line.CodeLineIndex,
					ShortCode = line.ShortCode,
					ToNode = line.ToNode,
					Iq = line.Iq,
					MessageNumber = line.MessageNumber,
					MessageFileNumber = line.MessageFileNumber,
				});
			}
		}

		private sealed class FixedRulesSource : IDialogueRulesSource
		{
			internal FixedRulesSource(DialogueFunctionRules rules)
			{
				ArgumentNullException.ThrowIfNull(rules);
				this.Current = rules;
			}

			public DialogueFunctionRules Current { get; }
		}
	}
}