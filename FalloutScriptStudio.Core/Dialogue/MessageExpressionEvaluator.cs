namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Parsing;

	/// <summary>
	/// Works out the value of the integer expression of a message argument: numbers (decimal and <c>0x</c> hexadecimal), macros of the
	/// script and of its headers (followed to their numbers, with their arguments), <c>+ - * / %</c>, parentheses and
	/// <c>random(a, b)</c>. The value of an expression is a range; only <c>random</c> makes it wider than one number. Anything else (a
	/// variable, a function call, an unknown name) gives no value.
	/// </summary>
	internal sealed class MessageExpressionEvaluator
	{
		private const Int32 MaxDepth = 16;

		private readonly ProgramInfo _info;

		public MessageExpressionEvaluator(ProgramInfo info)
		{
			this._info = info;
		}

		/// <summary>Works out the value of an expression.</summary>
		/// <param name="expression">The expression, without comments.</param>
		/// <param name="low">The smallest value.</param>
		/// <param name="high">The largest value.</param>
		/// <returns><see langword="false"/> when the value cannot be worked out.</returns>
		public Boolean TryEvaluate(String expression, out Int32 low, out Int32 high)
		{
			Boolean ok = this.TryEvaluate(expression, null, 0, out Int64 lo, out Int64 hi) && lo >= Int32.MinValue && hi <= Int32.MaxValue;
			low = ok ? (Int32)lo : 0;
			high = ok ? (Int32)hi : 0;
			return ok;
		}

		/// <summary>Gets the parameter names of a function-like macro (<c>mstr(x)</c> gives <c>x</c>).</summary>
		/// <param name="macro">The macro.</param>
		/// <returns>The parameters, or <see langword="null"/> when the macro has no parameter list.</returns>
		public static IReadOnlyList<String>? GetParameters(ScriptMacro macro)
		{
			String definition = macro.DefinitionName;
			Int32 open = definition.IndexOf('(', StringComparison.Ordinal);
			Int32 close = definition.LastIndexOf(')');
			if (open < 0 || close < open)
			{
				return null;
			}

			String list = definition.Substring(open + 1, close - open - 1);
			List<String> parameters = [];
			if (list.Trim().Length == 0)
			{
				return parameters;
			}

			foreach (String part in list.Split(','))
			{
				parameters.Add(part.Trim());
			}

			return parameters;
		}

		private Boolean TryEvaluate(String expression, Dictionary<String, (Int64 Low, Int64 High)>? environment, Int32 depth, out Int64 low, out Int64 high)
		{
			low = 0;
			high = 0;
			if (depth > MaxDepth)
			{
				return false;
			}

			List<Token>? tokens = Tokenize(expression);
			if (tokens is null)
			{
				return false;
			}

			ExpressionParser parser = new ExpressionParser(this, tokens, environment, depth);
			try
			{
				return parser.TryParse(out low, out high);
			}
			catch (OverflowException)
			{
				return false;
			}
		}

		private static List<Token>? Tokenize(String text)
		{
			List<Token> tokens = [];
			Int32 i = 0;
			while (i < text.Length)
			{
				Char c = text[i];
				if (Char.IsWhiteSpace(c))
				{
					i++;
				}
				else if (Char.IsDigit(c))
				{
					Int32 start = i;
					Boolean hex = c == '0' && i + 1 < text.Length && (text[i + 1] == 'x' || text[i + 1] == 'X');
					i += hex ? 2 : 1;
					while (i < text.Length && (hex ? Char.IsAsciiHexDigit(text[i]) : Char.IsDigit(text[i])))
					{
						i++;
					}

					if (i < text.Length && (Char.IsLetter(text[i]) || text[i] == '_'))
					{
						return null;
					}

					String digits = hex ? text.Substring(start + 2, i - start - 2) : text.Substring(start, i - start);
					Boolean parsed = Int64.TryParse(digits, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out Int64 value);
					if (!parsed || value > Int32.MaxValue)
					{
						return null;
					}

					tokens.Add(new Token(TokenKind.Number, String.Empty, value));
				}
				else if (Char.IsLetter(c) || c == '_')
				{
					Int32 start = i;
					while (i < text.Length && (Char.IsLetterOrDigit(text[i]) || text[i] == '_'))
					{
						i++;
					}

					tokens.Add(new Token(TokenKind.Name, text.Substring(start, i - start), 0));
				}
				else
				{
					TokenKind? kind = c switch
					{
						'+' => TokenKind.Plus,
						'-' => TokenKind.Minus,
						'*' => TokenKind.Star,
						'/' => TokenKind.Slash,
						'%' => TokenKind.Percent,
						'(' => TokenKind.Open,
						')' => TokenKind.Close,
						',' => TokenKind.Comma,
						_ => null,
					};
					if (kind is null)
					{
						return null;
					}

					tokens.Add(new Token(kind.Value, String.Empty, 0));
					i++;
				}
			}

			return tokens;
		}

		private enum TokenKind
		{
			Number,
			Name,
			Plus,
			Minus,
			Star,
			Slash,
			Percent,
			Open,
			Close,
			Comma,
		}

		private readonly record struct Token(TokenKind Kind, String Text, Int64 Value);

		private sealed class ExpressionParser
		{
			private readonly MessageExpressionEvaluator _owner;
			private readonly List<Token> _tokens;
			private readonly Dictionary<String, (Int64 Low, Int64 High)>? _environment;
			private readonly Int32 _depth;
			private Int32 _position;

			public ExpressionParser(MessageExpressionEvaluator owner, List<Token> tokens, Dictionary<String, (Int64 Low, Int64 High)>? environment, Int32 depth)
			{
				this._owner = owner;
				this._tokens = tokens;
				this._environment = environment;
				this._depth = depth;
			}

			public Boolean TryParse(out Int64 low, out Int64 high)
			{
				return this.ParseSum(out low, out high) && this._position == this._tokens.Count;
			}

			private TokenKind? Peek => this._position < this._tokens.Count ? this._tokens[this._position].Kind : null;

			private Boolean ParseSum(out Int64 low, out Int64 high)
			{
				if (!this.ParseProduct(out low, out high))
				{
					return false;
				}

				while (this.Peek is TokenKind.Plus or TokenKind.Minus)
				{
					Boolean plus = this._tokens[this._position++].Kind == TokenKind.Plus;
					if (!this.ParseProduct(out Int64 nextLow, out Int64 nextHigh))
					{
						return false;
					}

					if (plus)
					{
						low = checked(low + nextLow);
						high = checked(high + nextHigh);
					}
					else
					{
						(low, high) = (checked(low - nextHigh), checked(high - nextLow));
					}
				}

				return true;
			}

			private Boolean ParseProduct(out Int64 low, out Int64 high)
			{
				if (!this.ParseUnary(out low, out high))
				{
					return false;
				}

				while (this.Peek is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
				{
					TokenKind op = this._tokens[this._position++].Kind;
					if (!this.ParseUnary(out Int64 nextLow, out Int64 nextHigh) || low != high || nextLow != nextHigh)
					{
						return false;
					}

					if (op != TokenKind.Star && nextLow == 0)
					{
						return false;
					}

					Int64 value = op switch
					{
						TokenKind.Star => checked(low * nextLow),
						TokenKind.Slash => low / nextLow,
						_ => low % nextLow,
					};
					(low, high) = (value, value);
				}

				return true;
			}

			private Boolean ParseUnary(out Int64 low, out Int64 high)
			{
				if (this.Peek == TokenKind.Minus)
				{
					this._position++;
					if (!this.ParseUnary(out Int64 innerLow, out Int64 innerHigh))
					{
						low = 0;
						high = 0;
						return false;
					}

					(low, high) = (-innerHigh, -innerLow);
					return true;
				}

				if (this.Peek == TokenKind.Plus)
				{
					this._position++;
				}

				return this.ParsePrimary(out low, out high);
			}

			private Boolean ParsePrimary(out Int64 low, out Int64 high)
			{
				low = 0;
				high = 0;
				if (this._position >= this._tokens.Count)
				{
					return false;
				}

				Token token = this._tokens[this._position++];
				switch (token.Kind)
				{
					case TokenKind.Number:
						(low, high) = (token.Value, token.Value);
						return true;
					case TokenKind.Open:
						if (!this.ParseSum(out low, out high) || this.Peek != TokenKind.Close)
						{
							return false;
						}

						this._position++;
						return true;
					case TokenKind.Name:
						return this.Peek == TokenKind.Open ? this.ParseCall(token.Text, out low, out high) : this.ParseName(token.Text, out low, out high);
					default:
						return false;
				}
			}

			private Boolean ParseName(String name, out Int64 low, out Int64 high)
			{
				low = 0;
				high = 0;
				if (this._environment is not null && this._environment.TryGetValue(name, out (Int64 Low, Int64 High) bound))
				{
					(low, high) = bound;
					return true;
				}

				ScriptMacro? macro = this._owner._info.FindMacro(name);
				if (macro is null || MessageExpressionEvaluator.GetParameters(macro) is { Count: > 0 })
				{
					return false;
				}

				return this._owner.TryEvaluate(macro.Code, null, this._depth + 1, out low, out high);
			}

			private Boolean ParseCall(String name, out Int64 low, out Int64 high)
			{
				low = 0;
				high = 0;
				this._position++;
				List<(Int64 Low, Int64 High)> arguments = [];
				if (this.Peek == TokenKind.Close)
				{
					this._position++;
				}
				else
				{
					while (true)
					{
						if (!this.ParseSum(out Int64 argumentLow, out Int64 argumentHigh))
						{
							return false;
						}

						arguments.Add((argumentLow, argumentHigh));
						if (this.Peek == TokenKind.Comma)
						{
							this._position++;
							continue;
						}

						if (this.Peek != TokenKind.Close)
						{
							return false;
						}

						this._position++;
						break;
					}
				}

				if (String.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
				{
					if (arguments.Count != 2 || arguments[0].Low > arguments[1].High)
					{
						return false;
					}

					(low, high) = (arguments[0].Low, arguments[1].High);
					return true;
				}

				ScriptMacro? macro = this._owner._info.FindMacro(name);
				IReadOnlyList<String>? parameters = macro is null ? null : MessageExpressionEvaluator.GetParameters(macro);
				if (macro is null || parameters is null || parameters.Count != arguments.Count)
				{
					return false;
				}

				Dictionary<String, (Int64 Low, Int64 High)> bound = new(StringComparer.Ordinal);
				for (Int32 i = 0; i < parameters.Count; i++)
				{
					bound[parameters[i]] = arguments[i];
				}

				return this._owner.TryEvaluate(macro.Code, bound, this._depth + 1, out low, out high);
			}
		}
	}
}