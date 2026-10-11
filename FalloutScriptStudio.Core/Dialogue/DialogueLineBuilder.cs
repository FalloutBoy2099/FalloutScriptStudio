namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// Reads one dialogue statement out of a line of code (the body of the legacy <c>DialogueParser</c> constructor and its helpers).
	/// All state of the legacy parser (current line, program info, next search position) is held here instead of in static fields.
	/// </summary>
	internal sealed class DialogueLineBuilder
	{
		private const Int32 MaxMacroDepth = 16;
		private static readonly Char[] _trimming = { ' ', '(', ')' };
		private static readonly Char[] _digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

		private readonly DialogueFunctionRules _rules;
		private readonly ProgramInfo _info;
		private readonly Int32 _lineIndex;
		private DialogueOpcode _opcode;
		private readonly String _fullCode;
		private String? _shortCode;
		private String? _toNode;
		private String? _iq;
		private Int32 _numberMsgLine;
		private Int32 _numberMsgFile = -1;

		private DialogueLineBuilder(DialogueFunctionRules rules, ProgramInfo info, DialogueOpcode opcode, String code, Int32 lineIndex)
		{
			this._rules = rules;
			this._info = info;
			this._opcode = opcode;
			this._fullCode = code;
			this._lineIndex = lineIndex;
		}

		/// <summary>Gets the position at which the search for the next statement of the same line continues, or -1.</summary>
		internal Int32 NextPosition { get; private set; } = -1;

		/// <summary>
		/// Parses the statement whose opening parenthesis is at <paramref name="offset"/> (for <see cref="DialogueOpcode.Call"/>, the
		/// text after the <c>call</c> keyword).
		/// </summary>
		/// <param name="rules">The dialogue function rules.</param>
		/// <param name="info">The parse result of the script, used to resolve macros and procedure names.</param>
		/// <param name="opcode">What kind of statement was found by the scan.</param>
		/// <param name="name">The function or macro name (<c>null</c> for <c>call</c>).</param>
		/// <param name="code">The line of code (without a trailing comment when comments are excluded).</param>
		/// <param name="offset">See above.</param>
		/// <param name="lineIndex">The zero-based index of the line.</param>
		/// <param name="builder">The parsed statement; its <see cref="NextPosition"/> is set even when parsing fails.</param>
		/// <returns>The line, or <c>null</c> when the statement is malformed.</returns>
		internal static DialogueLine? TryParse(
			DialogueFunctionRules rules,
			ProgramInfo info,
			DialogueOpcode opcode,
			String? name,
			String code,
			Int32 offset,
			Int32 lineIndex,
			out DialogueLineBuilder builder)
		{
			builder = new DialogueLineBuilder(rules, info, opcode, code, lineIndex);
			try
			{
				switch (opcode)
				{
					case DialogueOpcode.Reply:
					case DialogueOpcode.Message:
					case DialogueOpcode.GsayReply:
					case DialogueOpcode.GsayMessage:
						builder._opcode = builder.ReplyOpcode(name!, code, offset);
						break;
					case DialogueOpcode.Option:
					case DialogueOpcode.GsayOption:
					case DialogueOpcode.GiqOption:
						builder.OptionOpcode(offset, name!, code);
						break;
					case DialogueOpcode.CallMacro:
						builder._opcode = builder.MiscCode(name!, code, offset);
						break;
					case DialogueOpcode.Call:
						builder.CallOpcode(code, offset);
						break;
				}
			}
			catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException)
			{
				builder.NextPosition = offset + 1;
				return null;
			}

			return builder.ToLine(code);
		}

		/// <summary>Creates a plain code line.</summary>
		/// <param name="code">The line.</param>
		/// <param name="lineIndex">The zero-based index of the line.</param>
		/// <returns>The line.</returns>
		internal static DialogueLine CreatePlainLine(String code, Int32 lineIndex)
		{
			return new DialogueLine { Opcode = DialogueOpcode.None, Code = code, CodeLineIndex = lineIndex };
		}

		/// <summary>
		/// Returns the position of the opening parenthesis of the function call that contains the keyword found at
		/// <paramref name="index"/> and the name of the function; the function name must be preceded by white space or the line start
		/// (legacy <c>GetOpcodeName</c>).
		/// </summary>
		/// <param name="code">The line of code.</param>
		/// <param name="index">In: an index inside the function name. Out: the index of the opening parenthesis.</param>
		/// <returns>The function name, or <c>null</c> when the text is not a call.</returns>
		internal static String? GetOpcodeName(String code, ref Int32 index)
		{
			Int32 end = -1;
			for (Int32 i = index + 1; i < code.Length; i++)
			{
				Char c = code[i];
				if (c != '_' && !Char.IsLetterOrDigit(c))
				{
					while (Char.IsWhiteSpace(c) && ++i < code.Length)
					{
						c = code[i];
					}

					if (c == '(')
					{
						end = i;
					}

					break;
				}
			}

			if (end == -1)
			{
				return null;
			}

			Int32 start = index > 0 ? -1 : 0;
			if (start != 0)
			{
				for (Int32 i = index - 1; i >= 0; i--)
				{
					Char c = code[i];
					if (c != '_' && !Char.IsLetterOrDigit(c))
					{
						if (Char.IsWhiteSpace(c))
						{
							start = i + 1;
						}

						break;
					}

					if (i == 0)
					{
						start = 0;
					}
				}
			}

			index = end;
			return start != -1 ? code.Substring(start, end - start).TrimEnd() : null;
		}

		private static Boolean TryParseInt(String text, out Int32 value)
		{
			return Int32.TryParse(text.Trim(_trimming), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		private static Int32 CountArguments(String code)
		{
			Int32 count = 1;
			foreach (Char c in code)
			{
				if (c == ',')
				{
					count++;
				}
			}

			return count;
		}

		private static String CleanNodeName(String text)
		{
			return text.Trim(_trimming).TrimStart('@');
		}

		/// <summary>
		/// Finds the end of a statement: the <c>;</c> that follows the opening parenthesis, or the position after the matching
		/// closing parenthesis (legacy <c>GetOpcodeEndPosition</c>).
		/// </summary>
		private static Int32 GetOpcodeEndPosition(String code, Int32 offset)
		{
			Int32 x = code.IndexOf(';', offset);
			if (x != -1)
			{
				return x;
			}

			for (; offset < code.Length; ++offset)
			{
				if (code[offset] != ' ')
				{
					offset++;
					break;
				}
			}

			Int32 brackets = 1;
			for (; offset < code.Length; ++offset)
			{
				Char ch = code[offset];
				if (ch == '(')
				{
					++brackets;
				}
				else if (ch == ')')
				{
					--brackets;
					if (brackets == 0)
					{
						return offset + 1;
					}
				}
				else if (ch == '"')
				{
					while (++offset < code.Length && code[offset] != '"')
					{
					}
				}
				else if ((ch == '/' || ch == '*') && offset > 0 && code[offset - 1] == '/')
				{
					break;
				}
			}

			return -1;
		}

		private DialogueLine ToLine(String code)
		{
			return new DialogueLine
			{
				Opcode = this._opcode,
				Code = code,
				CodeLineIndex = this._lineIndex,
				ShortCode = this._shortCode,
				ToNode = this._toNode,
				Iq = this._iq,
				MessageNumber = this._numberMsgLine,
				MessageFileNumber = this._numberMsgFile,
			};
		}

		private void SetNextPosition(ref Int32 end, Int32 offset)
		{
			if (end != -1)
			{
				this.NextPosition = end;
				end--;
			}
			else
			{
				this.NextPosition = offset + 1;
			}
		}

		private Int32 ParseMessageFile(String text)
		{
			return TryParseInt(text, out Int32 number) ? number : this.MessageSubCode(text);
		}

		/// <summary>gsay_message(msg_list, msg_num, reaction); gsay_reply(msg_list, msg_num); Reply / Message macros.</summary>
		private DialogueOpcode ReplyOpcode(String name, String code, Int32 offset)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			String lineMsg;
			if (this._rules.TryGet(name, out DialogueFunctionRule? template))
			{
				if (template.TotalArgs > 1)
				{
					if (CountArguments(code) != template.TotalArgs)
					{
						lineMsg = this.ReplyParseCode(offset, x, name, ref code);
					}
					else
					{
						code = code.Replace(';', ' ');
						String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);
						lineMsg = args[template.MessageArg];
						if (template.MessageFileArg != -1)
						{
							this._numberMsgFile = this.ParseMessageFile(args[template.MessageFileArg]);
						}
					}
				}
				else
				{
					Int32 z = code.IndexOf(')', offset);
					lineMsg = code.Substring(offset + 1, z - offset);
				}

				if (this._shortCode is null)
				{
					Int32 start = offset - name.Length;
					this._shortCode = this._fullCode.Substring(start, (x > -1 ? x : this._fullCode.Length - 1) - start + 1);
				}
			}
			else
			{
				lineMsg = this.ReplyParseCode(offset, x, name, ref code);
			}

			if (lineMsg.Length > 0)
			{
				this._numberMsgLine = TryParseInt(lineMsg, out Int32 number) ? number : this.MessageSubCode(lineMsg);
			}

			if (this._opcode is DialogueOpcode.Message or DialogueOpcode.GsayMessage)
			{
				this._toNode = "[Message]";
				return DialogueOpcode.Message;
			}

			this._toNode = "[Reply]";
			return DialogueOpcode.Reply;
		}

		private String ReplyParseCode(Int32 offset, Int32 x, String name, ref String code)
		{
			if (x < 0)
			{
				return String.Empty;
			}

			Int32 start = offset - name.Length;
			this._shortCode = code.Substring(start, x + 1 - start);
			if (code[x] == ')')
			{
				x--;
			}

			try
			{
				code = code.Substring(offset + 1, x - offset);
			}
			catch (ArgumentOutOfRangeException)
			{
				return String.Empty;
			}

			if (this._opcode == DialogueOpcode.GsayMessage)
			{
				code = code.Remove(code.LastIndexOf(','));
			}

			if (this._opcode is DialogueOpcode.GsayReply or DialogueOpcode.GsayMessage)
			{
				Int32 z = code.IndexOf(',');
				String fileNumber = code.Remove(z++);
				this._numberMsgFile = this.ParseMessageFile(fileNumber);
				code = code.Substring(z);
			}

			return code;
		}

		/// <summary>gsay_option(msg_list, msg_num, procedure, reaction); giq_option(iq, msg_list, msg_num, procedure, reaction); NOption(msg, node, iq) macros.</summary>
		private void OptionOpcode(Int32 offset, String name, String code)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			String lineMsg;
			if (this._rules.TryGet(name, out DialogueFunctionRule? template))
			{
				if (CountArguments(code) != template.TotalArgs)
				{
					lineMsg = this.ParseOptionCode(offset, x, name, ref code);
				}
				else
				{
					code = code.Replace(';', ' ');
					String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);

					lineMsg = args[template.MessageArg];
					this._toNode = CleanNodeName(args[template.NodeArg]);

					if (template.MessageFileArg != -1)
					{
						this._numberMsgFile = this.ParseMessageFile(args[template.MessageFileArg]);
					}

					if (template.IqArg != -1)
					{
						if (TryParseInt(args[template.IqArg], out Int32 iq))
						{
							this._iq = iq.ToString(CultureInfo.InvariantCulture);
						}
					}
					else
					{
						this._iq = "-";
					}

					Int32 start = offset - name.Length;
					this._shortCode = code.Substring(start, (x > -1 ? x : code.Length - 1) - start + 1);
					code = args[template.MessageArg];
				}
			}
			else
			{
				lineMsg = this.ParseOptionCode(offset, x, name, ref code);
			}

			if (lineMsg.Length > 0 && !TryParseInt(lineMsg, out this._numberMsgLine))
			{
				this._numberMsgLine = this.MessageSubCode(code);
			}
		}

		private String ParseOptionCode(Int32 offset, Int32 x, String name, ref String code)
		{
			if (x < offset)
			{
				this._opcode = DialogueOpcode.None;
				return String.Empty;
			}

			Int32 start = offset - name.Length;
			this._shortCode = code.Substring(start, x + 1 - start);
			try
			{
				code = code.Substring(++offset, x - offset);
			}
			catch (ArgumentOutOfRangeException)
			{
				return String.Empty;
			}

			Int32 z = code.LastIndexOf(',') + 1;
			String msgNum = String.Empty;

			switch (this._opcode)
			{
				case DialogueOpcode.GsayOption:
				case DialogueOpcode.GiqOption:
					if (this._opcode == DialogueOpcode.GiqOption)
					{
						x = code.IndexOf(',');
						this._iq = code.Remove(x).Trim(_trimming);
						code = code.Substring(++x);
						z -= x;
					}
					else
					{
						this._iq = "-";
					}

					code = code.Remove(z - 1);
					z = code.LastIndexOf(',') + 1;
					this._toNode = CleanNodeName(code.Substring(z));
					code = code.Remove(z - 1);

					z = code.IndexOf(',');
					String fileNumber = code.Remove(z);
					x = fileNumber.IndexOf(',') + 1;
					this._numberMsgFile = TryParseInt(fileNumber.Substring(x), out Int32 file) ? file : this.MessageSubCode(fileNumber);
					code = code.Substring(z + 1);

					msgNum = code;
					z = msgNum.IndexOf(',');
					if (z > 0)
					{
						msgNum = msgNum.Remove(z);
					}

					break;

				case DialogueOpcode.Option:
					String last = code.Substring(z).Trim(_trimming);
					if (Int32.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 iq))
					{
						this._iq = iq.ToString(CultureInfo.InvariantCulture);
						code = code.Remove(z - 1);
					}
					else
					{
						this._toNode = last.TrimStart('@');
					}

					if (this._toNode is null)
					{
						Int32 y = code.LastIndexOf(',') + 1;
						this._toNode = CleanNodeName(code.Substring(y, --z - y));
						z = y;
					}

					msgNum = code = code.Remove(z - 1);
					break;
			}

			return msgNum;
		}

		/// <summary>User macros that jump to a node without a message (rules of type <c>Call</c>).</summary>
		private DialogueOpcode MiscCode(String name, String code, Int32 offset)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			DialogueFunctionRule template = this._rules.TryGet(name, out DialogueFunctionRule? found) ? found : throw new KeyNotFoundException(name);
			if (template.TotalArgs > 1)
			{
				if (CountArguments(code) != template.TotalArgs)
				{
					this._toNode = this.FindNodeByKeyword(code, offset) ?? "<Failed get node name>";
				}
				else
				{
					code = code.Replace(';', ' ');
					String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);
					this._toNode = CleanNodeName(args[template.NodeArg]);
				}
			}
			else
			{
				Int32 z = code.IndexOf(')', offset);
				this._toNode = CleanNodeName(code.Substring(offset + 1, z - offset));
			}

			Int32 start = offset - name.Length;
			this._shortCode = this._fullCode.Substring(start, (x > -1 ? x : this._fullCode.Length - 1) - start + 1);
			this._numberMsgLine = -1;
			return DialogueOpcode.Call;
		}

		/// <summary>Finds a procedure name that contains the word "node" in a statement whose arguments cannot be split reliably.</summary>
		private String? FindNodeByKeyword(String code, Int32 offset)
		{
			Int32 z = offset;
			while (z != -1 && z < code.Length)
			{
				z = code.IndexOf("node", z, StringComparison.OrdinalIgnoreCase);
				if (z == -1)
				{
					break;
				}

				String word = WordScanner.GetWordAt(code, z);
				ScriptProcedure? procedure = word.Length == 0 ? null : this._info.GetProcedure(word);
				if (procedure is not null)
				{
					return procedure.Name;
				}

				z++;
			}

			return null;
		}

		/// <summary>The standard <c>call Node;</c> statement.</summary>
		private void CallOpcode(String code, Int32 offset)
		{
			Int32 end = code.IndexOf(';', offset);
			if (end == -1)
			{
				end = code.Length;
			}

			Int32 start = offset - 4;
			this._shortCode = code.Substring(start, end - start);

			Int32 paren = code.IndexOf('(', offset);
			if (paren > 0 && paren < end)
			{
				end = paren;
			}

			String target = code.Substring(offset, end - offset).Trim();
			Int32 length = 0;
			while (length < target.Length && WordScanner.IsWordChar(target[length]))
			{
				length++;
			}

			this._toNode = length > 0 ? target.Substring(0, length) : "<Failed get call node name>";
			this._numberMsgLine = -1;
		}

		/// <summary>
		/// Works out a message number from an expression: a macro, a <c>message_str(FILE, n)</c> call, <c>random(a, b)</c>,
		/// a sum of numbers, or the first number found (legacy <c>messageSubCode</c>).
		/// </summary>
		/// <returns>The message number, or -1 when it could not be determined. May set the message file number as a side effect.</returns>
		private Int32 MessageSubCode(String scode)
		{
			Int32 result = -1;
			Int32 macroValue = -1;
			String? token = null;
			Boolean isMessageStr = false;

			Int32 j = scode.IndexOf('(');
			if (j != -1)
			{
				Int32 i = scode.IndexOf("message_str", StringComparison.Ordinal);
				if (i == -1)
				{
					i = scode.IndexOf("random", StringComparison.Ordinal);
				}

				if (i != -1)
				{
					token = GetOpcodeName(scode, ref i);
				}

				if (token is not null)
				{
					switch (token.ToLowerInvariant())
					{
						case "message_str":
							isMessageStr = true;
							break;
						case "random":
							j = scode.IndexOf(',') - 1;
							i = scode.IndexOf('(', i);
							token = scode.Substring(i + 1, j - i).Trim(_trimming);
							return this.CheckMacroValue(token);
					}
				}
				else
				{
					token = scode.Remove(j).Trim(_trimming);
				}
			}

			ScriptMacro? macro = token is null ? null : this._info.FindMacro(token);
			if (token is not null && (isMessageStr || macro is not null))
			{
				String definition = isMessageStr ? scode : macro!.Code;
				Boolean hasMessageStr = definition.Contains("message_str", StringComparison.OrdinalIgnoreCase);
				Int32 z = definition.IndexOf('(');
				Int32 y = definition.IndexOf(',', z);

				if (hasMessageStr)
				{
					String fileArgument = definition.Substring(z + 1, y - z - 1).Trim(_trimming);
					macroValue = this.CheckMacroValue(fileArgument);
					if (macroValue == -1)
					{
						return macroValue;
					}
				}

				z = isMessageStr ? scode.IndexOf(',') : scode.IndexOf('(');
				if (z == -1)
				{
					return result;
				}

				this._numberMsgFile = macroValue;

				z++;
				if (!isMessageStr)
				{
					y = scode.IndexOf(',', z);
				}

				if (y == -1 || isMessageStr)
				{
					y = scode.IndexOf(')', z);
				}

				String lineArgument = scode.Substring(z, y - z);
				result = TryParseInt(lineArgument, out Int32 number) ? number : this.MessageSubCode(lineArgument);
			}
			else if (this._info.FindMacro(scode) is not null)
			{
				result = this.CheckMacroValue(scode);
			}
			else
			{
				result = this.SumNumbers(scode);
			}

			return result;
		}

		/// <summary>Evaluates a sum of numbers and macros (<c>MSG_BASE + 5</c>); a term that is neither contributes its first number.</summary>
		private Int32 SumNumbers(String expression)
		{
			Int32 total = 0;
			foreach (String term in expression.Split('+'))
			{
				String trimmed = term.Trim(_trimming);
				Int32 value;
				if (TryParseInt(trimmed, out Int32 number))
				{
					value = number;
				}
				else if (this._info.FindMacro(trimmed) is not null)
				{
					value = Math.Max(this.CheckMacroValue(trimmed), 0);
				}
				else
				{
					value = FirstNumber(term);
				}

				total += value;
			}

			return total;
		}

		private static Int32 FirstNumber(String text)
		{
			Int32 start = text.IndexOfAny(_digits);
			if (start == -1)
			{
				return 0;
			}

			Int32 end = start;
			while (end < text.Length && Char.IsDigit(text[end]))
			{
				end++;
			}

			return Int32.TryParse(text.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out Int32 value) ? value : 0;
		}

		/// <summary>Follows macro definitions until a number is reached (legacy <c>CheckMacrosValue</c>).</summary>
		private Int32 CheckMacroValue(String argument)
		{
			return this.ResolveMacro(argument, 0);
		}

		private Int32 ResolveMacro(String argument, Int32 startDepth)
		{
			String token = argument;
			for (Int32 depth = startDepth; depth < MaxMacroDepth; depth++)
			{
				ScriptMacro? macro = this._info.FindMacro(token);
				if (macro is null)
				{
					if (TryParseInt(token, out Int32 plain))
					{
						return plain;
					}

					return token.Contains('+', StringComparison.Ordinal) ? this.EvaluateSum(token, depth + 1) : -1;
				}

				token = macro.Code.Trim('(', ')');
				Int32 close = token.IndexOf(')');
				if (close > 0)
				{
					token = token.Remove(close);
				}

				if (Int32.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 value))
				{
					return value;
				}
			}

			return -1;
		}

		/// <summary>Evaluates a sum of numbers and macros whose terms must all be known.</summary>
		private Int32 EvaluateSum(String expression, Int32 depth)
		{
			Int32 total = 0;
			foreach (String term in expression.Split('+'))
			{
				String trimmed = term.Trim(_trimming);
				Int32 value = TryParseInt(trimmed, out Int32 number) ? number : this.ResolveMacro(trimmed, depth);
				if (value < 0)
				{
					return -1;
				}

				total += value;
			}

			return total;
		}
	}
}
