namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>The outline pass: the procedures, the variables and the begin / end structure.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		private void Outline()
		{
			Int32 i = 0;
			while (i < this._count)
			{
				if ((i & 0x3FF) == 0)
				{
					this._cancellation.ThrowIfCancellationRequested();
				}

				if (this.Skip(i))
				{
					if (!this._tokens[i].InDirective)
					{
						this.NoteSkippedDeclaration(i);
					}

					i++;
					continue;
				}

				i = this._current is null ? this.TopLevel(i) : this.Body(i);
			}

			if (this._current is not null)
			{
				this.CloseUnterminated(this._current, this._count - 1);
			}
		}

		/// <summary>Remembers the names declared in a branch that is not analysed, so that they are never reported as missing.</summary>
		private void NoteSkippedDeclaration(Int32 index)
		{
			if (this._tokens[index].Kind != SslTokenKind.Identifier)
			{
				return;
			}

			if (this.IsKeyword(index, "procedure") && this.IsIdentifier(index + 1) && !this._tokens[index + 1].InDirective)
			{
				this._maybeProcedures.Add(this._tokens.GetText(index + 1));
			}
			else if (this.IsKeyword(index, "variable"))
			{
				Int32 i = index + 1;
				Int32 limit = Math.Min(this._count, index + 128);
				while (i < limit && this.IsIdentifier(i) && !this._tokens[i].InDirective)
				{
					this._maybeVariables.Add(this._tokens.GetText(i));
					Int32 depth = 0;
					i++;
					while (i < limit && !(depth == 0 && (this.IsPunct(i, ',') || this.IsPunct(i, ';'))))
					{
						if (this.IsPunct(i, '(') || this.IsPunct(i, '[') || this.IsPunct(i, '{'))
						{
							depth++;
						}
						else if (this.IsPunct(i, ')') || this.IsPunct(i, ']') || this.IsPunct(i, '}'))
						{
							depth--;
						}

						i++;
					}

					if (i < limit && this.IsPunct(i, ','))
					{
						i++;
					}
					else
					{
						break;
					}
				}
			}
		}

		private Int32 TopLevel(Int32 start)
		{
			Int32 first = start;
			Boolean isImport = false;
			Boolean isExport = false;
			Int32 k = start;
			while (k < this._count && this.IsIdentifier(k) && SslScriptModel.IsModifier(this.Span(k)))
			{
				if (this.IsKeyword(k, "import"))
				{
					isImport = true;
				}
				else if (this.IsKeyword(k, "export"))
				{
					isExport = true;
				}

				k = this.NextCode(k);
			}

			if (k < this._count && this.IsKeyword(k, "procedure"))
			{
				return this.ParseProcedureHeader(first, k, isImport, isExport, k > first);
			}

			if (k < this._count && this.IsKeyword(k, "variable"))
			{
				return this.ParseVariables(first, k, VariableScopeKind.Global, null);
			}

			if (k > start)
			{
				return k;
			}

			if (this._tokens[start].Kind == SslTokenKind.Identifier)
			{
				ReadOnlySpan<Char> word = this.Span(start);
				if (this.IsKeyword(start, "end"))
				{
					this._structure.Add(new PendingDiagnostic(ScriptDiagnosticCodes.UnexpectedEnd, start, Array.Empty<String>()));
				}
				else if (this.IsKeyword(start, "begin"))
				{
					// A body without a header: follow it so that its end is not reported as a stray one.
					this._current = new ProcedureDeclaration(String.Empty, start, start, ProcedureKind.Definition, this._frameOf[start]) { BeginToken = start };
					this._blocks.Clear();
					this._blocks.Push(start);
					this._parenDepth = 0;
					this.TaintGlobal(start);
				}
				else if (!ScriptBuiltins.IsKeyword(word))
				{
					// Something at the top level that is not a declaration: a use of a macro (which may declare names) or garbage.
					MacroTraits? traits = this.LookupMacro(word);
					if (traits is null || traits.IsDeclaring)
					{
						this.TaintGlobal(start);
					}
					else if (traits.IsStructural)
					{
						this._structuralMacroUsed = true;
					}
				}
			}

			return start + 1;
		}

		private Int32 ParseProcedureHeader(Int32 first, Int32 procedureToken, Boolean isImport, Boolean isExport, Boolean hasModifiers)
		{
			Int32 nameToken = this.NextCode(procedureToken);
			if (nameToken >= this._count || this._tokens[nameToken].Kind != SslTokenKind.Identifier)
			{
				return procedureToken + 1;
			}

			Boolean nameIsMacro = this.LookupMacro(this.Span(nameToken)) is not null;
			ProcedureDeclaration declaration = new ProcedureDeclaration(this._tokens.GetText(nameToken), nameToken, first, ProcedureKind.Forward, this._frameOf[nameToken])
			{
				IsImport = isImport,
				IsExport = isExport,
				HasModifiers = hasModifiers,
			};

			Int32 i = this.NextCode(nameToken);
			if (i < this._count && this.IsPunct(i, '('))
			{
				i = this.ParseParameters(i, declaration);
			}

			while (i < this._count)
			{
				if (this.Skip(i))
				{
					i++;
					continue;
				}

				if (this.IsPunct(i, ';'))
				{
					declaration.Kind = isImport ? ProcedureKind.Import : isExport ? ProcedureKind.ExportForward : ProcedureKind.Forward;
					declaration.SemicolonToken = i;
					this.Register(declaration, nameIsMacro);
					return i + 1;
				}

				if (this._tokens[i].Kind == SslTokenKind.Identifier)
				{
					if (this.IsKeyword(i, "begin"))
					{
						declaration.Kind = ProcedureKind.Definition;
						declaration.BeginToken = i;
						this.Register(declaration, nameIsMacro);
						this._current = declaration;
						this._blocks.Clear();
						this._blocks.Push(i);
						this._parenDepth = 0;
						return i + 1;
					}

					if (this.IsKeyword(i, "procedure") || this.IsKeyword(i, "variable") || this.IsKeyword(i, "end"))
					{
						break;
					}
				}

				i++;
			}

			return i;
		}

		/// <summary>Parses the parameter list of a header; <paramref name="open"/> is the <c>(</c>. Returns the index after the <c>)</c>, or where the list went wrong.</summary>
		private Int32 ParseParameters(Int32 open, ProcedureDeclaration declaration)
		{
			Int32 i = this.NextCode(open);
			while (i < this._count)
			{
				if (this.IsPunct(i, ')'))
				{
					return i + 1;
				}

				if (this.IsPunct(i, ',') || this._tokens[i].Kind != SslTokenKind.Identifier)
				{
					if (this.IsPunct(i, ';'))
					{
						return i;
					}

					i = this.NextCode(i);
					continue;
				}

				if (this.IsKeyword(i, "begin") || this.IsKeyword(i, "procedure") || this.IsKeyword(i, "end"))
				{
					return i;
				}

				if (this.IsKeyword(i, "variable"))
				{
					i = this.NextCode(i);
					continue;
				}

				Int32 nameToken = i;
				this._declaresName[nameToken] = true;
				i = this.NextCode(i);
				Boolean hasDefault = false;
				if (i < this._count && (this.IsPunct(i, ":=") || this.IsPunct(i, '=')))
				{
					hasDefault = true;
					i = this.SkipExpression(this.NextCode(i), true);
				}

				declaration.Parameters.Add(new ParameterDeclaration(this._tokens.GetText(nameToken), nameToken, hasDefault));
				VariableDeclarationInfo parameter = new VariableDeclarationInfo(this._tokens.GetText(nameToken), nameToken, VariableScopeKind.Parameter, declaration, this._frameOf[nameToken]) { HasInitializer = hasDefault, StatementToken = nameToken };
				this.AddLocal(declaration, parameter);
			}

			return i;
		}

		private void Register(ProcedureDeclaration declaration, Boolean nameIsMacro)
		{
			Int32 required = 0;
			Boolean defaultSeen = false;
			ParameterDeclaration? firstDefault = null;
			foreach (ParameterDeclaration parameter in declaration.Parameters)
			{
				if (parameter.HasDefault)
				{
					defaultSeen = true;
					declaration.HasDefaults = true;
					firstDefault ??= parameter;
				}
				else
				{
					required++;
					if (defaultSeen && declaration.Kind == ProcedureKind.Definition)
					{
						this.Report(ScriptDiagnosticCodes.DefaultBeforeRequired, parameter.Token, new[] { declaration.Name, parameter.Name });
					}
				}
			}

			declaration.MinArguments = required;
			declaration.MaxArguments = declaration.Parameters.Count;
			this._procedures.Add(declaration);
			if (nameIsMacro)
			{
				this.TaintGlobal(declaration.NameToken);
				return;
			}

			if (!this._proceduresByName.TryGetValue(declaration.Name, out List<ProcedureDeclaration>? list))
			{
				list = [];
				this._proceduresByName[declaration.Name] = list;
			}

			list.Add(declaration);
			if (declaration.Kind == ProcedureKind.Definition)
			{
				foreach (VariableDeclarationInfo local in declaration.LocalList)
				{
					this._localOwners.TryAdd(local.Name, declaration);
				}
			}
		}

		private void AddLocal(ProcedureDeclaration procedure, VariableDeclarationInfo variable)
		{
			if (!procedure.Locals.TryGetValue(variable.Name, out List<VariableDeclarationInfo>? list))
			{
				list = new(1);
				procedure.Locals[variable.Name] = list;
			}

			list.Add(variable);
			procedure.LocalList.Add(variable);
			if (procedure.Kind == ProcedureKind.Definition && variable.Scope != VariableScopeKind.Parameter)
			{
				this._localOwners.TryAdd(variable.Name, procedure);
			}
		}

		/// <summary>One token of a procedure body.</summary>
		private Int32 Body(Int32 index)
		{
			ProcedureDeclaration procedure = this._current!;
			SslToken token = this._tokens[index];
			if (token.Kind == SslTokenKind.Punctuation)
			{
				if (this.IsPunct(index, '('))
				{
					this._parenDepth++;
				}
				else if (this.IsPunct(index, ')'))
				{
					this._parenDepth = Math.Max(this._parenDepth - 1, 0);
				}

				return index + 1;
			}

			if (token.Kind != SslTokenKind.Identifier)
			{
				return index + 1;
			}

			if (this.IsKeyword(index, "begin"))
			{
				this._blocks.Push(index);
				return index + 1;
			}

			if (this.IsKeyword(index, "end"))
			{
				this._blocks.Pop();
				if (this._blocks.Count == 0)
				{
					procedure.EndToken = index;
					procedure.IsClosed = true;
					this._current = null;
				}

				return index + 1;
			}

			if (this.IsKeyword(index, "procedure") && this.IsProcedureStart(index))
			{
				this.CloseUnterminated(procedure, index - 1);
				return index;
			}

			if (this.IsKeyword(index, "variable"))
			{
				return this.ParseVariables(index, index, this._parenDepth > 0 ? VariableScopeKind.Loop : VariableScopeKind.Local, procedure);
			}

			ReadOnlySpan<Char> word = this.Span(index);
			if (!ScriptBuiltins.IsKeyword(word))
			{
				MacroTraits? traits = this.LookupMacro(word);
				if (traits is not null)
				{
					if (traits.IsStructural)
					{
						this._structuralMacroUsed = true;
					}

					if (traits.IsDeclaring && procedure.TaintFrom < 0)
					{
						procedure.TaintFrom = index;
					}
				}
			}

			return index + 1;
		}

		/// <summary>A <c>procedure</c> in the middle of a body that is followed by a name and a header: the previous body was not closed.</summary>
		private Boolean IsProcedureStart(Int32 index)
		{
			Int32 name = this.NextCode(index);
			if (name >= this._count || this._tokens[name].Kind != SslTokenKind.Identifier)
			{
				return false;
			}

			Int32 after = this.NextCode(name);
			return after < this._count && (this.IsPunct(after, '(') || this.IsPunct(after, ';') || this.IsKeyword(after, "begin"));
		}

		private void CloseUnterminated(ProcedureDeclaration procedure, Int32 lastToken)
		{
			procedure.EndToken = lastToken;
			if (this._blocks.Count > 0)
			{
				Int32 open = this._blocks.Peek();
				String name = procedure.Name.Length == 0 ? this._tokens.GetText(open) : procedure.Name;
				this._structure.Add(new PendingDiagnostic(ScriptDiagnosticCodes.MissingEnd, open, new[] { name }));
			}

			this._blocks.Clear();
			this._current = null;
		}

		/// <summary>
		/// Parses <c>variable a, b := 1, c[10];</c> (or the variables of a loop header). <paramref name="first"/> is the first token of the
		/// statement (a modifier or the keyword), <paramref name="variableToken"/> the <c>variable</c> keyword. Returns the index after the statement.
		/// </summary>
		private Int32 ParseVariables(Int32 first, Int32 variableToken, VariableScopeKind scope, ProcedureDeclaration? procedure)
		{
			Boolean loop = scope == VariableScopeKind.Loop;
			Int32 i = this.NextCode(variableToken);
			if (i < this._count && this.IsKeyword(i, "begin"))
			{
				return this.ParseVariableBlock(first, i, scope, procedure);
			}

			while (i < this._count && this._tokens[i].Kind == SslTokenKind.Identifier && !ScriptBuiltins.IsKeyword(this.Span(i)))
			{
				Int32 nameToken = i;
				this.Declare(nameToken, first, scope, procedure);
				i = this.NextCode(i);
				if (i < this._count && this.IsPunct(i, '['))
				{
					i = this.SkipBrackets(i);
				}

				if (loop && i < this._count && this.IsPunct(i, ':') && this.IsIdentifier(this.NextCode(i)))
				{
					// foreach (variable key: value in array): the value variable is declared by the same keyword.
					i = this.NextCode(i);
					this.Declare(i, first, scope, procedure);
					i = this.NextCode(i);
				}

				if (i < this._count && (this.IsPunct(i, ":=") || this.IsPunct(i, '=')))
				{
					this.MarkInitializer(nameToken, scope, procedure);
					i = this.SkipExpression(this.NextCode(i), loop);
				}

				if (i < this._count && this.IsPunct(i, ','))
				{
					i = this.NextCode(i);
					continue;
				}

				break;
			}

			if (!loop && i < this._count && this.IsPunct(i, ';'))
			{
				return i + 1;
			}

			return Math.Max(i, variableToken + 1);
		}

		/// <summary>Parses the block form, <c>variable begin a := 1; b; end</c>. Returns the index after the closing <c>end</c>.</summary>
		private Int32 ParseVariableBlock(Int32 first, Int32 beginToken, VariableScopeKind scope, ProcedureDeclaration? procedure)
		{
			Int32 i = this.NextCode(beginToken);
			while (i < this._count && !this.IsKeyword(i, "end"))
			{
				if (this._tokens[i].Kind == SslTokenKind.Identifier && !ScriptBuiltins.IsKeyword(this.Span(i)))
				{
					Int32 nameToken = i;
					this.Declare(nameToken, first, scope, procedure);
					i = this.NextCode(i);
					if (i < this._count && this.IsPunct(i, '['))
					{
						i = this.SkipBrackets(i);
					}

					if (i < this._count && (this.IsPunct(i, ":=") || this.IsPunct(i, '=')))
					{
						this.MarkInitializer(nameToken, scope, procedure);
						i = this.SkipExpression(this.NextCode(i), false);
					}
				}
				else if (this.IsKeyword(i, "procedure") || this.IsKeyword(i, "begin"))
				{
					return i;
				}
				else
				{
					i = this.NextCode(i);
				}
			}

			return i < this._count ? i + 1 : i;
		}

		private void MarkInitializer(Int32 nameToken, VariableScopeKind scope, ProcedureDeclaration? procedure)
		{
			VariableDeclarationInfo? declaration = this.FindDeclaration(nameToken, procedure);
			if (declaration is not null)
			{
				declaration.HasInitializer = true;
			}
		}

		private VariableDeclarationInfo? FindDeclaration(Int32 nameToken, ProcedureDeclaration? procedure)
		{
			List<VariableDeclarationInfo>? list = null;
			String name = this._tokens.GetText(nameToken);
			if (procedure is null)
			{
				this._globals.TryGetValue(name, out list);
			}
			else
			{
				procedure.Locals.TryGetValue(name, out list);
			}

			if (list is null)
			{
				return null;
			}

			foreach (VariableDeclarationInfo candidate in list)
			{
				if (candidate.Token == nameToken)
				{
					return candidate;
				}
			}

			return null;
		}

		private void Declare(Int32 nameToken, Int32 first, VariableScopeKind scope, ProcedureDeclaration? procedure)
		{
			this._declaresName[nameToken] = true;
			String name = this._tokens.GetText(nameToken);
			VariableDeclarationInfo declaration = new VariableDeclarationInfo(name, nameToken, scope, procedure, this._frameOf[nameToken])
			{
				StatementToken = first,
				IsInParentheses = scope == VariableScopeKind.Loop,
			};

			if (procedure is null)
			{
				if (!this._globals.TryGetValue(name, out List<VariableDeclarationInfo>? list))
				{
					list = new(1);
					this._globals[name] = list;
				}

				list.Add(declaration);
			}
			else
			{
				this.AddLocal(procedure, declaration);
			}
		}

		/// <summary>Skips a bracketed array size, <c>[10]</c>; returns the index after the closing bracket.</summary>
		private Int32 SkipBrackets(Int32 open)
		{
			Int32 depth = 0;
			Int32 i = open;
			while (i < this._count)
			{
				if (!this.Skip(i))
				{
					if (this.IsPunct(i, '['))
					{
						depth++;
					}
					else if (this.IsPunct(i, ']'))
					{
						depth--;
						if (depth == 0)
						{
							return this.NextCode(i);
						}
					}
					else if (this.IsPunct(i, ';'))
					{
						return i;
					}
				}

				i++;
			}

			return i;
		}

		/// <summary>
		/// Skips an initial value or a default value up to the comma, semicolon or closing parenthesis that ends it. Returns the index of
		/// that token; the keywords that cannot be part of a value also end it, so a missing semicolon does not swallow the next statement.
		/// </summary>
		private Int32 SkipExpression(Int32 start, Boolean insideParentheses)
		{
			Int32 nesting = 0;
			Int32 i = start;
			while (i < this._count)
			{
				if (this.Skip(i))
				{
					i++;
					continue;
				}

				SslToken token = this._tokens[i];
				if (token.Kind == SslTokenKind.Punctuation)
				{
					if (this.IsPunct(i, '(') || this.IsPunct(i, '[') || this.IsPunct(i, '{'))
					{
						nesting++;
					}
					else if (this.IsPunct(i, ')') || this.IsPunct(i, ']') || this.IsPunct(i, '}'))
					{
						if (nesting == 0)
						{
							return i;
						}

						nesting--;
					}
					else if (nesting == 0 && (this.IsPunct(i, ',') || this.IsPunct(i, ';')))
					{
						return i;
					}
				}
				else if (token.Kind == SslTokenKind.Identifier && nesting == 0
					&& (this.IsKeyword(i, "begin") || this.IsKeyword(i, "end") || this.IsKeyword(i, "procedure") || this.IsKeyword(i, "variable") || (insideParentheses && this.IsKeyword(i, "in"))))
				{
					return i;
				}

				i++;
			}

			return i;
		}
	}
}