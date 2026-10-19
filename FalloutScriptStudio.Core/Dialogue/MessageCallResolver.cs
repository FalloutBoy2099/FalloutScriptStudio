namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using Parsing;

	/// <summary>
	/// Tells which names of a script show a message and where their message number is: the dialogue rules (built in and the user's),
	/// <c>message_str</c>, the macros that expand to one of those (<c>#define mstr(x) message_str(NAME, x)</c>, followed through
	/// several levels) and, like the legacy parser, any macro whose name says <c>Reply</c>, <c>Message</c> or <c>Option</c>.
	/// </summary>
	internal sealed class MessageCallResolver
	{
		private const Int32 MaxDepth = 8;
		private const String MessageStr = "message_str";

		private static readonly Regex _offsetRegex = new Regex(@"^(?<a>\w+)\s*\+\s*(?<b>\w+)$", RegexOptions.CultureInvariant);

		private readonly ProgramInfo _info;
		private readonly DialogueFunctionRules _rules;
		private readonly Dictionary<String, MessageCallSpec?> _cache = new(StringComparer.Ordinal);

		public MessageCallResolver(ProgramInfo info, DialogueFunctionRules rules)
		{
			this._info = info;
			this._rules = rules;
		}

		/// <summary>Finds the description of a function or macro that shows a message.</summary>
		/// <param name="name">The name as written in the script.</param>
		/// <returns>The description, or <see langword="null"/> when the name shows no message.</returns>
		public MessageCallSpec? Find(String name)
		{
			if (!this._cache.TryGetValue(name, out MessageCallSpec? spec))
			{
				spec = this.Resolve(name, 0);
				this._cache[name] = spec;
			}

			return spec;
		}

		private MessageCallSpec? Resolve(String name, Int32 depth)
		{
			if (depth > MaxDepth)
			{
				return null;
			}

			if (this._rules.TryGet(name, out DialogueFunctionRule? rule))
			{
				return FromRule(rule);
			}

			if (String.Equals(name, MessageStr, StringComparison.OrdinalIgnoreCase))
			{
				return new MessageCallSpec(1, 0, 0, null, ScriptMessageUsageKind.Display, 2, 2);
			}

			ScriptMacro? macro = this._info.FindMacro(name);
			if (macro is not null && MessageExpressionEvaluator.GetParameters(macro) is { Count: > 0 } parameters)
			{
				MessageCallSpec? expanded = this.Expand(macro, parameters, depth);
				if (expanded is not null)
				{
					return expanded;
				}
			}

			return this.FromKeyword(name, macro is not null);
		}

		private static MessageCallSpec? FromRule(DialogueFunctionRule rule)
		{
			if (rule.MessageArg < 0 || rule.Opcode is DialogueOpcode.None or DialogueOpcode.Call or DialogueOpcode.CallMacro)
			{
				return null;
			}

			ScriptMessageUsageKind kind = rule.Opcode.IsOption() ? ScriptMessageUsageKind.Option : ScriptMessageUsageKind.NpcText;
			return new MessageCallSpec(rule.MessageArg, 0, rule.MessageFileArg, null, kind, Math.Max(rule.MessageArg, rule.MessageFileArg) + 1, Int32.MaxValue);
		}

		/// <summary>The legacy parser took every name that contains Reply, Message or Option for a dialogue statement whose first argument is the message.</summary>
		private MessageCallSpec? FromKeyword(String name, Boolean isMacro)
		{
			// Engine functions are written in lower case (create_message_window, message_str_game); macros of dialogue headers are not.
			if (name.StartsWith("message_", StringComparison.OrdinalIgnoreCase) || this._info.ProcedureExists(name) || (!isMacro && String.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal)))
			{
				return null;
			}

			if (name.Contains("Option", StringComparison.OrdinalIgnoreCase))
			{
				return new MessageCallSpec(0, 0, -1, null, ScriptMessageUsageKind.Option, 2, 4, true);
			}

			if (name.Contains("Reply", StringComparison.OrdinalIgnoreCase) || name.Contains("Message", StringComparison.OrdinalIgnoreCase))
			{
				return new MessageCallSpec(0, 0, -1, null, ScriptMessageUsageKind.NpcText, 1, 1, true);
			}

			return null;
		}

		/// <summary>Looks in the body of a macro for a call that shows a message whose number is one of the macro's parameters.</summary>
		private MessageCallSpec? Expand(ScriptMacro macro, IReadOnlyList<String> parameters, Int32 depth)
		{
			String body = ScriptMessageMasker.Mask(macro.Code, false);
			Int32 i = 0;
			while (i < body.Length)
			{
				if (Char.IsDigit(body[i]))
				{
					while (i < body.Length && (Char.IsLetterOrDigit(body[i]) || body[i] == '_'))
					{
						i++;
					}

					continue;
				}

				if (!IsNameStart(body[i]))
				{
					i++;
					continue;
				}

				Int32 start = i;
				while (i < body.Length && (Char.IsLetterOrDigit(body[i]) || body[i] == '_'))
				{
					i++;
				}

				String name = body.Substring(start, i - start);
				Int32 open = i;
				while (open < body.Length && Char.IsWhiteSpace(body[open]))
				{
					open++;
				}

				if (open >= body.Length || body[open] != '(' || String.Equals(name, macro.Token, StringComparison.Ordinal))
				{
					continue;
				}

				MessageCallSpec? inner = this.Resolve(name, depth + 1);
				MessageCallArguments? arguments = inner is null ? null : MessageCallArguments.Parse(body, open);
				if (inner is not null && arguments is not null && arguments.Count >= inner.MinArguments && arguments.Count > inner.MessageArg)
				{
					MessageCallSpec? spec = Bind(parameters, inner, arguments);
					if (spec is not null)
					{
						return spec;
					}
				}
			}

			return null;
		}

		private static MessageCallSpec? Bind(IReadOnlyList<String> parameters, MessageCallSpec inner, MessageCallArguments arguments)
		{
			if (!TryFindParameter(parameters, arguments[inner.MessageArg], out Int32 parameter, out Int32 offset))
			{
				return null;
			}

			Int32 fileArg = -1;
			String? fileExpression = inner.FileExpression;
			if (inner.FileArg >= 0 && inner.FileArg < arguments.Count)
			{
				String file = arguments[inner.FileArg];
				Int32 fileParameter = IndexOf(parameters, file);
				if (fileParameter >= 0)
				{
					fileArg = fileParameter;
				}
				else
				{
					fileExpression = file;
				}
			}

			return new MessageCallSpec(parameter, offset + inner.Offset, fileArg, fileArg >= 0 ? null : fileExpression, inner.Kind, parameters.Count, parameters.Count);
		}

		private static Boolean TryFindParameter(IReadOnlyList<String> parameters, String expression, out Int32 parameter, out Int32 offset)
		{
			offset = 0;
			parameter = IndexOf(parameters, expression);
			if (parameter >= 0)
			{
				return true;
			}

			Match match = _offsetRegex.Match(expression);
			if (!match.Success)
			{
				return false;
			}

			String left = match.Groups["a"].Value;
			String right = match.Groups["b"].Value;
			if (Int32.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out Int32 rightNumber) && (parameter = IndexOf(parameters, left)) >= 0)
			{
				offset = rightNumber;
				return true;
			}

			if (Int32.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out Int32 leftNumber) && (parameter = IndexOf(parameters, right)) >= 0)
			{
				offset = leftNumber;
				return true;
			}

			parameter = -1;
			return false;
		}

		private static Int32 IndexOf(IReadOnlyList<String> parameters, String name)
		{
			for (Int32 i = 0; i < parameters.Count; i++)
			{
				if (String.Equals(parameters[i], name, StringComparison.Ordinal))
				{
					return i;
				}
			}

			return -1;
		}

		internal static Boolean IsNameStart(Char c)
		{
			return Char.IsLetter(c) || c == '_';
		}
	}
}