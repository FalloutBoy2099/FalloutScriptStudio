namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Globalization;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Works out message numbers and message file numbers from expressions: literals, macros, sums, <c>message_str</c> and <c>random</c>.</summary>
	internal sealed partial class DialogueLineBuilder
	{
		private const Int32 MaxMacroDepth = 16;

		private static readonly Char[] _digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

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
