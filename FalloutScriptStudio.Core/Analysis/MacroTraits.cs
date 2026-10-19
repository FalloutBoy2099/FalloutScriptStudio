namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>
	/// What the analysis needs to know about the body of a macro, to decide how a use of the macro must be treated: a macro can expand to
	/// a literal (it cannot be assigned), to several arguments, to a declaration or to the <c>begin</c> of a block, and then the
	/// text around its use cannot be analysed as if the macro were a plain name.
	/// </summary>
	internal sealed class MacroTraits
	{
		private MacroTraits()
		{
		}

		/// <summary>Gets a value indicating whether the body is made of literals and operators only (<c>(5)</c>, <c>"text"</c>), so it is a constant.</summary>
		public Boolean IsConstant { get; private init; }

		/// <summary>Gets a value indicating whether the body declares a variable or a procedure.</summary>
		public Boolean IsDeclaring { get; private init; }

		/// <summary>Gets a value indicating whether the body contains <c>begin</c>, <c>end</c>, <c>then</c> or <c>do</c>, which the structure of the script depends on.</summary>
		public Boolean IsStructural { get; private init; }

		/// <summary>Gets a value indicating whether the body contains a comma or unbalanced parentheses, so a use can stand for several arguments.</summary>
		public Boolean SpreadsArguments { get; private init; }

		/// <summary>Gets a value indicating whether the body uses the stringizing or pasting operators (<c>#</c>, <c>##</c>), which turn arguments into names or text.</summary>
		public Boolean ChangesArguments { get; private init; }

		/// <summary>Analyses the body of a macro.</summary>
		/// <param name="code">The body, without the name and the parameter list.</param>
		/// <returns>The traits.</returns>
		public static MacroTraits From(String code)
		{
			ArgumentNullException.ThrowIfNull(code);

			Boolean pastes = code.Contains('#', StringComparison.Ordinal);
			SslTokenStream tokens = SslTokenizer.Tokenize(code);
			Boolean declaring = false;
			Boolean structural = false;
			Boolean spreads = false;
			Boolean identifiers = false;
			Boolean literal = false;
			Boolean other = false;
			Int32 depth = 0;
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				switch (tokens[i].Kind)
				{
					case SslTokenKind.Identifier:
						if (tokens.IsKeyword(i, "variable") || tokens.IsKeyword(i, "procedure"))
						{
							declaring = true;
						}
						else if (tokens.IsKeyword(i, "begin") || tokens.IsKeyword(i, "end") || tokens.IsKeyword(i, "then") || tokens.IsKeyword(i, "do"))
						{
							structural = true;
						}

						identifiers = true;
						break;
					case SslTokenKind.Number:
					case SslTokenKind.String:
						literal = true;
						break;
					case SslTokenKind.Punctuation:
						if (tokens.IsPunctuation(i, ','))
						{
							if (depth <= 0)
							{
								spreads = true;
							}
						}
						else if (tokens.IsPunctuation(i, '('))
						{
							depth++;
						}
						else if (tokens.IsPunctuation(i, ')'))
						{
							depth--;
							if (depth < 0)
							{
								spreads = true;
							}
						}
						else if (tokens.IsPunctuation(i, ';') || tokens.IsPunctuation(i, '{') || tokens.IsPunctuation(i, '}') || tokens.IsPunctuation(i, '[') || tokens.IsPunctuation(i, ']') || tokens.IsPunctuation(i, ":=") || tokens.IsPunctuation(i, '=') || tokens.IsPunctuation(i, '.') || tokens.IsPunctuation(i, ':'))
						{
							other = true;
						}

						break;
					default:
						other = true;
						break;
				}
			}

			return new MacroTraits
			{
				IsConstant = literal && !identifiers && !other && !spreads && depth == 0,
				IsDeclaring = declaring,
				IsStructural = structural,
				SpreadsArguments = spreads || depth != 0,
				ChangesArguments = pastes,
			};
		}
	}
}