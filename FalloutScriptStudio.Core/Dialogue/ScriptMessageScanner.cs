namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using Parsing;
	using TextTools;

	/// <summary>
	/// Finds every message number a script uses. The calls that show a message are those of the dialogue rules (built in and the
	/// user's, as the dialogue preview and the flowchart read them: <c>NMessage(100)</c>, <c>Reply(100)</c>, <c>NOption(101, node, 4)</c>,
	/// <c>gsay_reply(NAME, 100)</c>...), <c>message_str(file, n)</c> and the macros that expand to them (<c>mstr(n)</c>, <c>floater(n)</c>).
	/// Comments, string literals and preprocessor directives are ignored, a call may span several lines, and the number may be a macro
	/// or a sum (<c>MSG_BASE + 5</c>).
	/// </summary>
	public static class ScriptMessageScanner
	{
		private const Int32 MaxRange = 64;
		private const Int32 Unknown = -2;

		/// <summary>Scans a script.</summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script; it supplies the macros of the script and of its headers and the script number (the <c>NAME</c> macro).</param>
		/// <param name="rules">The dialogue function rules.</param>
		/// <returns>What the script uses.</returns>
		public static ScriptMessageScan Scan(String scriptText, ProgramInfo info, DialogueFunctionRules rules)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(rules);

			String masked = ScriptMessageMasker.Mask(scriptText);
			MessageCallResolver resolver = new MessageCallResolver(info, rules);
			MessageExpressionEvaluator evaluator = new MessageExpressionEvaluator(info);
			TextLineMap lines = new TextLineMap(scriptText);
			Int32 ownId = info.ScriptNameId;

			List<ScriptMessageUsage> usages = [];
			Int32 unresolved = 0;
			String previous = String.Empty;
			Int32 i = 0;
			while (i < masked.Length)
			{
				Char c = masked[i];
				if (Char.IsDigit(c))
				{
					while (i < masked.Length && (Char.IsLetterOrDigit(masked[i]) || masked[i] == '_'))
					{
						i++;
					}

					continue;
				}

				if (!MessageCallResolver.IsNameStart(c))
				{
					i++;
					continue;
				}

				Int32 start = i;
				while (i < masked.Length && (Char.IsLetterOrDigit(masked[i]) || masked[i] == '_'))
				{
					i++;
				}

				String name = masked.Substring(start, i - start);
				String before = previous;
				previous = name;

				Int32 open = i;
				while (open < masked.Length && Char.IsWhiteSpace(masked[open]))
				{
					open++;
				}

				if (open >= masked.Length || masked[open] != '(' || String.Equals(before, "procedure", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				MessageCallSpec? spec = resolver.Find(name);
				MessageCallArguments? arguments = spec is null ? null : MessageCallArguments.Parse(masked, open);
				if (spec is null || arguments is null || arguments.Count < spec.MinArguments || arguments.Count > spec.MaxArguments || arguments.Count <= spec.MessageArg)
				{
					continue;
				}

				Int32 file = ResolveFile(spec, arguments, evaluator, ownId);
				if (file == Unknown)
				{
					continue;
				}

				String expression = arguments[spec.MessageArg];
				if (spec.IsHeuristic && expression.StartsWith('"'))
				{
					continue;
				}

				if (!evaluator.TryEvaluate(expression, out Int32 low, out Int32 high) || high - low > MaxRange)
				{
					// An argument that is itself a call that shows a message ("Reply(mstr(110))") is counted where that call is.
					if (file == ScriptMessageUsage.OwnFile && !ContainsMessageCall(expression, resolver))
					{
						unresolved++;
					}

					continue;
				}

				Int32 offset = arguments.StartOf(spec.MessageArg);
				Int32 line = lines.GetLineFromOffset(offset);
				Int32 column = lines.GetColumnFromOffset(offset);
				for (Int32 number = low; number <= high; number++)
				{
					Int32 shown = number + spec.Offset;
					if (shown > 0)
					{
						usages.Add(new ScriptMessageUsage(shown, file, line, column + 1, expression.Length, spec.Kind));
					}
				}
			}

			return new ScriptMessageScan(usages, unresolved, MentionsGender(masked));
		}

		private static Int32 ResolveFile(MessageCallSpec spec, MessageCallArguments arguments, MessageExpressionEvaluator evaluator, Int32 ownId)
		{
			String? expression = spec.FileExpression;
			if (spec.FileArg >= 0)
			{
				if (arguments.Count <= spec.FileArg)
				{
					return Unknown;
				}

				expression = arguments[spec.FileArg];
			}

			if (expression is null)
			{
				return ScriptMessageUsage.OwnFile;
			}

			if (evaluator.TryEvaluate(expression, out Int32 low, out Int32 high) && low == high)
			{
				return ownId > 0 && low == ownId ? ScriptMessageUsage.OwnFile : low;
			}

			// NAME is the script number by definition; when the header that defines it is missing it still means the script itself.
			return String.Equals(expression, "NAME", StringComparison.Ordinal) ? ScriptMessageUsage.OwnFile : Unknown;
		}

		private static Boolean ContainsMessageCall(String expression, MessageCallResolver resolver)
		{
			Int32 i = 0;
			while (i < expression.Length)
			{
				if (!MessageCallResolver.IsNameStart(expression[i]))
				{
					i++;
					continue;
				}

				Int32 start = i;
				while (i < expression.Length && (Char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
				{
					i++;
				}

				Int32 open = i;
				while (open < expression.Length && Char.IsWhiteSpace(expression[open]))
				{
					open++;
				}

				if (open < expression.Length && expression[open] == '(' && resolver.Find(expression.Substring(start, i - start)) is not null)
				{
					return true;
				}
			}

			return false;
		}

		private static Boolean MentionsGender(String masked)
		{
			return masked.Contains("female", StringComparison.OrdinalIgnoreCase) || masked.Contains("gender", StringComparison.OrdinalIgnoreCase);
		}
	}
}