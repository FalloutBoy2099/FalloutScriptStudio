namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The structure of one script text found with the tokens: the procedures that have a body and every declared variable (parameters,
	/// local variables and script level variables). It works on the text only, so it is available when no parser is, and it never throws
	/// on a half-typed script: whatever cannot be understood is left out.
	/// </summary>
	public sealed class SslScriptModel
	{
		private static readonly String[] _modifiers = { "import", "export", "inline", "pure", "critical" };

		private readonly List<SslProcedureModel> _procedures = new List<SslProcedureModel>();
		private readonly List<VariableDeclaration> _declarations = new List<VariableDeclaration>();

		private SslScriptModel(SslTokenStream tokens)
		{
			this.Tokens = tokens;
		}

		/// <summary>Gets the tokens of the text.</summary>
		public SslTokenStream Tokens { get; }

		/// <summary>Gets the procedures that have a body, in text order.</summary>
		public IReadOnlyList<SslProcedureModel> Procedures => this._procedures;

		/// <summary>Gets every declared variable in text order: script level variables, parameters and local variables.</summary>
		public IReadOnlyList<VariableDeclaration> Declarations => this._declarations;

		/// <summary>Analyzes a text.</summary>
		/// <param name="text">The script text.</param>
		/// <returns>The model.</returns>
		public static SslScriptModel Analyze(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return Analyze(SslTokenizer.Tokenize(text));
		}

		/// <summary>Analyzes tokens.</summary>
		/// <param name="tokens">The tokens of a text.</param>
		/// <returns>The model.</returns>
		public static SslScriptModel Analyze(SslTokenStream tokens)
		{
			ArgumentNullException.ThrowIfNull(tokens);
			SslScriptModel model = new SslScriptModel(tokens);
			model.Build();
			return model;
		}

		/// <summary>Finds the procedure whose body contains a token.</summary>
		/// <param name="tokenIndex">The index of the token.</param>
		/// <returns>The procedure, or <see langword="null"/> when the token is outside every body (or in a procedure header).</returns>
		public SslProcedureModel? FindProcedureAt(Int32 tokenIndex)
		{
			Int32 low = 0;
			Int32 high = this._procedures.Count - 1;
			while (low <= high)
			{
				Int32 middle = low + ((high - low) / 2);
				SslProcedureModel procedure = this._procedures[middle];
				if (tokenIndex < procedure.BeginToken)
				{
					high = middle - 1;
				}
				else if (tokenIndex > procedure.EndToken)
				{
					low = middle + 1;
				}
				else
				{
					return procedure;
				}
			}

			return null;
		}

		/// <summary>Determines whether a word is a modifier that can precede <c>procedure</c> or <c>variable</c>.</summary>
		/// <param name="word">The word.</param>
		/// <returns><see langword="true"/> for <c>import</c>, <c>export</c>, <c>inline</c>, <c>pure</c> and <c>critical</c>.</returns>
		public static Boolean IsModifier(ReadOnlySpan<Char> word)
		{
			foreach (String modifier in _modifiers)
			{
				if (word.Equals(modifier, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private void Build()
		{
			SslTokenStream tokens = this.Tokens;
			Int32 count = tokens.Count;
			Int32 i = 0;
			while (i < count)
			{
				SslToken token = tokens[i];
				if (token.InDirective || token.Kind != SslTokenKind.Identifier)
				{
					i++;
					continue;
				}

				Int32 first = i;
				Int32 keyword = i;
				VariableDeclarationKind variableKind = VariableDeclarationKind.Global;
				while (keyword < count && tokens[keyword].Kind == SslTokenKind.Identifier && !tokens[keyword].InDirective && IsModifier(tokens.Span(keyword)))
				{
					if (tokens.IsKeyword(keyword, "import"))
					{
						variableKind = VariableDeclarationKind.Import;
					}
					else if (tokens.IsKeyword(keyword, "export"))
					{
						variableKind = VariableDeclarationKind.Export;
					}

					keyword++;
				}

				if (keyword < count && !tokens[keyword].InDirective && tokens.IsKeyword(keyword, "procedure"))
				{
					i = this.ParseProcedure(first, keyword);
				}
				else if (keyword < count && !tokens[keyword].InDirective && tokens.IsKeyword(keyword, "variable"))
				{
					i = this.ParseDeclarations(keyword + 1, tokens[first].Offset, variableKind, null, null, false);
				}
				else
				{
					i = Math.Max(keyword, first + 1);
				}
			}
		}

		/// <summary>Parses a procedure from its <c>procedure</c> keyword and returns the index of the token after it.</summary>
		private Int32 ParseProcedure(Int32 first, Int32 procedureToken)
		{
			SslTokenStream tokens = this.Tokens;
			Int32 count = tokens.Count;
			Int32 nameToken = procedureToken + 1;
			if (nameToken >= count || tokens[nameToken].Kind != SslTokenKind.Identifier || tokens[nameToken].InDirective)
			{
				return procedureToken + 1;
			}

			SslProcedureModel procedure = new SslProcedureModel(tokens.GetText(nameToken), first, procedureToken, nameToken);
			Int32 i = nameToken + 1;
			Int32 depth = 0;
			Int32 begin = -1;
			Boolean parametersParsed = false;
			while (i < count)
			{
				SslToken token = tokens[i];
				if (token.InDirective)
				{
					i++;
					continue;
				}

				if (depth == 0 && tokens.IsPunctuation(i, ';'))
				{
					return i + 1;
				}

				if (depth == 0 && tokens.IsKeyword(i, "begin"))
				{
					begin = i;
					break;
				}

				if (tokens.IsKeyword(i, "procedure") || tokens.IsKeyword(i, "end"))
				{
					return i;
				}

				if (tokens.IsPunctuation(i, '('))
				{
					if (depth == 0 && !parametersParsed)
					{
						parametersParsed = true;
						i = this.ParseParameters(i, procedure);
						continue;
					}

					depth++;
				}
				else if (tokens.IsPunctuation(i, ')'))
				{
					depth = Math.Max(depth - 1, 0);
				}

				i++;
			}

			if (begin < 0)
			{
				return i;
			}

			procedure.BeginToken = begin;
			this._declarations.AddRange(procedure.Declarations);
			procedure.EndToken = count - 1;
			Int32 blocks = 1;
			Int32 parentheses = 0;
			i = begin + 1;
			while (i < count)
			{
				SslToken token = tokens[i];
				if (token.InDirective || token.Kind != SslTokenKind.Identifier && token.Kind != SslTokenKind.Punctuation)
				{
					i++;
					continue;
				}

				if (token.Kind == SslTokenKind.Punctuation)
				{
					if (tokens.IsPunctuation(i, '('))
					{
						parentheses++;
					}
					else if (tokens.IsPunctuation(i, ')'))
					{
						parentheses = Math.Max(parentheses - 1, 0);
					}

					i++;
					continue;
				}

				if (tokens.IsKeyword(i, "begin"))
				{
					blocks++;
				}
				else if (tokens.IsKeyword(i, "end"))
				{
					blocks--;
					if (blocks == 0)
					{
						procedure.EndToken = i;
						procedure.IsClosed = true;
						this._procedures.Add(procedure);
						return i + 1;
					}
				}
				else if (tokens.IsKeyword(i, "procedure") && IsProcedureStart(i))
				{
					procedure.EndToken = i - 1;
					this._procedures.Add(procedure);
					return i;
				}
				else if (tokens.IsKeyword(i, "variable"))
				{
					i = this.ParseDeclarations(i + 1, token.Offset, VariableDeclarationKind.Local, procedure.Name, procedure, parentheses > 0);
					continue;
				}

				i++;
			}

			this._procedures.Add(procedure);
			return i;
		}

		private Boolean IsProcedureStart(Int32 index)
		{
			// A "procedure" word in the middle of a statement cannot start a procedure: it must be followed by a name and then by "(", ";", or "begin".
			SslTokenStream tokens = this.Tokens;
			return index + 2 < tokens.Count
				&& tokens[index + 1].Kind == SslTokenKind.Identifier
				&& (tokens.IsPunctuation(index + 2, '(') || tokens.IsPunctuation(index + 2, ';') || tokens.IsKeyword(index + 2, "begin"));
		}

		/// <summary>Parses the parameter list of a procedure; <paramref name="open"/> is the index of the <c>(</c>. Returns the index after the <c>)</c>.</summary>
		private Int32 ParseParameters(Int32 open, SslProcedureModel procedure)
		{
			SslTokenStream tokens = this.Tokens;
			Int32 count = tokens.Count;
			Int32 i = open + 1;
			while (i < count && !tokens.IsPunctuation(i, ')'))
			{
				if (tokens[i].InDirective || tokens.IsKeyword(i, "begin") || tokens.IsKeyword(i, "procedure") || tokens.IsPunctuation(i, ';'))
				{
					return i;
				}

				if (tokens.IsPunctuation(i, ','))
				{
					i++;
					continue;
				}

				Int32 statementStart = i;
				if (tokens.IsKeyword(i, "variable"))
				{
					i++;
				}

				if (i >= count || tokens[i].Kind != SslTokenKind.Identifier || tokens.IsKeyword(i, "begin"))
				{
					i = Math.Max(i, statementStart + 1);
					continue;
				}

				DeclarationStatement statement = new DeclarationStatement(tokens[statementStart].Offset, true);
				i = this.ParseItem(i, VariableDeclarationKind.Parameter, procedure.Name, procedure, statement, false);
				statement.End = tokens[i - 1].End;
			}

			return i < count ? i + 1 : i;
		}

		/// <summary>
		/// Parses the variables that follow a <c>variable</c> keyword: <c>a, b := 1[, ...]</c> up to the semicolon.
		/// Returns the index of the token after the declaration.
		/// </summary>
		private Int32 ParseDeclarations(Int32 start, Int32 statementOffset, VariableDeclarationKind kind, String? procedureName, SslProcedureModel? procedure, Boolean inParentheses)
		{
			SslTokenStream tokens = this.Tokens;
			Int32 count = tokens.Count;
			DeclarationStatement statement = new DeclarationStatement(statementOffset, inParentheses);
			Int32 i = start;
			while (i < count && tokens[i].Kind == SslTokenKind.Identifier && !tokens[i].InDirective && !tokens.IsKeyword(i, "begin") && !tokens.IsKeyword(i, "end"))
			{
				i = this.ParseItem(i, kind, procedureName, procedure, statement, true);
				if (tokens.IsPunctuation(i, ',') && i + 1 < count && tokens[i + 1].Kind == SslTokenKind.Identifier && !tokens.IsKeyword(i + 1, "variable"))
				{
					i++;
					continue;
				}

				break;
			}

			if (statement.Items.Count == 0)
			{
				return Math.Max(i, start);
			}

			if (i < count && tokens.IsPunctuation(i, ';'))
			{
				statement.IsTerminated = true;
				statement.End = tokens[i].End;
				return i + 1;
			}

			statement.End = tokens[i - 1].End;
			return i;
		}

		/// <summary>Parses one variable (<c>name[N] := value</c>) at a name token and returns the index of the token after it.</summary>
		private Int32 ParseItem(Int32 nameToken, VariableDeclarationKind kind, String? procedureName, SslProcedureModel? procedure, DeclarationStatement statement, Boolean register)
		{
			SslTokenStream tokens = this.Tokens;
			Int32 count = tokens.Count;
			SslToken name = tokens[nameToken];
			VariableDeclaration declaration = new VariableDeclaration(tokens.GetText(nameToken), kind, procedureName, nameToken, name.Offset, name.Line, tokens.GetColumn(name.Offset));
			statement.Add(declaration);
			if (register)
			{
				this._declarations.Add(declaration);
			}

			procedure?.Add(declaration);

			Int32 i = nameToken + 1;
			declaration.ItemEnd = name.End;
			if (tokens.IsPunctuation(i, '['))
			{
				Int32 depth = 0;
				while (i < count)
				{
					if (tokens.IsPunctuation(i, '['))
					{
						depth++;
					}
					else if (tokens.IsPunctuation(i, ']'))
					{
						depth--;
						if (depth == 0)
						{
							i++;
							break;
						}
					}
					else if (tokens.IsPunctuation(i, ';'))
					{
						break;
					}

					i++;
				}

				declaration.ItemEnd = tokens[Math.Min(i, count) - 1].End;
			}

			if (!tokens.IsPunctuation(i, ":=") && !tokens.IsPunctuation(i, '='))
			{
				return i;
			}

			declaration.HasInitializer = true;
			i++;
			Int32 nesting = 0;
			while (i < count)
			{
				SslToken token = tokens[i];
				if (token.InDirective)
				{
					break;
				}

				if (token.Kind == SslTokenKind.Punctuation)
				{
					if (tokens.IsPunctuation(i, '(') || tokens.IsPunctuation(i, '[') || tokens.IsPunctuation(i, '{'))
					{
						nesting++;
					}
					else if (tokens.IsPunctuation(i, ')') || tokens.IsPunctuation(i, ']') || tokens.IsPunctuation(i, '}'))
					{
						if (nesting == 0)
						{
							break;
						}

						nesting--;
					}
					else if (nesting == 0 && (tokens.IsPunctuation(i, ',') || tokens.IsPunctuation(i, ';')))
					{
						break;
					}
					else if (tokens.IsPunctuation(i, ":=") || tokens.IsPunctuation(i, '=') || tokens.IsPunctuation(i, "++") || tokens.IsPunctuation(i, "--") || tokens.IsPunctuation(i, "+=") || tokens.IsPunctuation(i, "-=")
						|| tokens.IsPunctuation(i, "*=") || tokens.IsPunctuation(i, "/="))
					{
						declaration.InitializerHasSideEffects = true;
					}
				}
				else if (token.Kind == SslTokenKind.Identifier)
				{
					if (tokens.IsKeyword(i, "begin") || tokens.IsKeyword(i, "end") || tokens.IsKeyword(i, "variable") || tokens.IsKeyword(i, "in"))
					{
						break;
					}

					if (tokens.IsPunctuation(i + 1, '(') || tokens.IsKeyword(i, "call"))
					{
						declaration.InitializerHasSideEffects = true;
					}
				}

				i++;
			}

			declaration.ItemEnd = tokens[i - 1].End;
			return i;
		}
	}
}
