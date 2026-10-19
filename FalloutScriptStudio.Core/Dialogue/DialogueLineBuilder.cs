namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Parsing;

	/// <summary>
	/// Reads one dialogue statement out of a line of code (the body of the legacy <c>DialogueParser</c> constructor and its helpers).
	/// All state of the legacy parser (current line, program info, next search position) is held here instead of in static fields.
	/// </summary>
	internal sealed partial class DialogueLineBuilder
	{
		private static readonly Char[] _trimming = { ' ', '(', ')' };

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
	}
}