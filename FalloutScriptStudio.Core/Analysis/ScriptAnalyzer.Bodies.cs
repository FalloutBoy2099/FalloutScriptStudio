namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>The pass over the bodies of the procedures: the resolution of every name.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		private readonly List<ArgumentSegment> _arguments = [];

		private readonly record struct ArgumentSegment(Int32 First, Int32 Last);

		private Int32 ExternalVisibleFrom => this._firstIncludeToken == Int32.MaxValue ? -1 : this._firstIncludeToken;

		private void CheckBodies()
		{
			foreach (ProcedureDeclaration procedure in this._procedures)
			{
				if (procedure.Kind != ProcedureKind.Definition)
				{
					continue;
				}

				this._cancellation.ThrowIfCancellationRequested();
				if (this.LimitReached)
				{
					return;
				}

				this.CheckBody(procedure);
			}
		}

		private void CheckBody(ProcedureDeclaration procedure)
		{
			Int32 last = procedure.IsClosed ? procedure.EndToken - 1 : procedure.EndToken;
			Int32 previous = procedure.BeginToken;
			Int32 parentheses = 0;
			Int32 callTarget = -1;
			for (Int32 i = procedure.BeginToken + 1; i <= last && i < this._count; i++)
			{
				if ((i & 0xFF) == 0 && this.LimitReached)
				{
					return;
				}

				if (this.Skip(i))
				{
					continue;
				}

				SslToken token = this._tokens[i];
				switch (token.Kind)
				{
					case SslTokenKind.Punctuation:
						if (this.IsPunct(i, '('))
						{
							parentheses++;
						}
						else if (this.IsPunct(i, ')'))
						{
							parentheses = Math.Max(parentheses - 1, 0);
						}

						break;
					case SslTokenKind.Number:
					case SslTokenKind.String:
						this.CheckLiteralAssignment(i, previous, parentheses);
						break;
					case SslTokenKind.Identifier:
						if (this._declaresName[i] || this.IsPunct(previous, '.'))
						{
							break;
						}

						if (ScriptBuiltins.IsKeyword(this.Span(i)))
						{
							if (this.IsKeyword(i, "call"))
							{
								callTarget = this.NextCode(i);
							}
							else if (this.IsKeyword(i, "if"))
							{
								this.CheckThenOrDo(i, "then", ScriptDiagnosticCodes.MissingThen);
							}
							else if (this.IsKeyword(i, "while"))
							{
								this.CheckThenOrDo(i, "do", ScriptDiagnosticCodes.MissingDo);
							}

							break;
						}

						this.CheckName(i, procedure, previous, parentheses, callTarget == i);
						break;
					default:
						break;
				}

				previous = i;
			}
		}

		private Boolean IsStatementStart(Int32 previous)
		{
			return this.IsPunct(previous, ';')
				|| this.IsKeyword(previous, "begin")
				|| this.IsKeyword(previous, "then")
				|| this.IsKeyword(previous, "else")
				|| this.IsKeyword(previous, "do")
				|| this.IsKeyword(previous, "end");
		}

		private Boolean IsAssignment(Int32 index)
		{
			return index < this._count
				&& this._tokens[index].Kind == SslTokenKind.Punctuation
				&& (this.IsPunct(index, ":=") || this.IsPunct(index, '=') || this.IsPunct(index, "+=") || this.IsPunct(index, "-=") || this.IsPunct(index, "*=")
					|| this.IsPunct(index, "/=") || this.IsPunct(index, "%=") || this.IsPunct(index, "++") || this.IsPunct(index, "--"));
		}

		private void CheckLiteralAssignment(Int32 index, Int32 previous, Int32 parentheses)
		{
			if (parentheses == 0 && this.IsStatementStart(previous) && this.IsAssignment(this.NextCode(index)))
			{
				this.Report(ScriptDiagnosticCodes.AssignToConstant, index, new[] { this._tokens.GetText(index) });
			}
		}

		/// <summary>Resolves one name of a body in the order of the compiler: macro, variable, procedure, built-in; then explains why it was not found.</summary>
		private void CheckName(Int32 index, ProcedureDeclaration procedure, Int32 previous, Int32 parentheses, Boolean isCallTarget)
		{
			ReadOnlySpan<Char> word = this.Span(index);
			Int32 next = this.NextCode(index);

			MacroTraits? macro = this.LookupMacro(word);
			if (macro is not null)
			{
				if (macro.IsConstant && parentheses == 0 && this.IsStatementStart(previous) && this.IsAssignment(next))
				{
					this.Report(ScriptDiagnosticCodes.AssignToConstant, index, new[] { this._tokens.GetText(index) });
				}

				return;
			}

			if (this.IsVisibleVariable(word, index, procedure))
			{
				return;
			}

			if (this.TryGetVisibleProcedure(word, index, out Int32 minimum, out Int32 maximum, out Boolean ambiguous))
			{
				this.CheckProcedureUse(index, word, previous, parentheses, next, isCallTarget, minimum, maximum, ambiguous);
				return;
			}

			if (ScriptBuiltins.IsBuiltin(word))
			{
				if (parentheses == 0 && this.IsStatementStart(previous) && this.IsAssignment(next))
				{
					this.Report(ScriptDiagnosticCodes.AssignToConstant, index, new[] { this._tokens.GetText(index) });
					return;
				}

				this.CheckFunctionCall(index, word, next, isCallTarget);
				return;
			}

			if (this.IsUnreliable(index, procedure))
			{
				return;
			}

			if (this.ReportDeclaredElsewhere(index, word, procedure))
			{
				return;
			}

			if (this._environment.Functions.Contains(word))
			{
				this.CheckFunctionCall(index, word, next, isCallTarget);
				return;
			}

			Boolean isCall = isCallTarget || (next < this._count && this.IsPunct(next, '('));
			this.ReportUndefined(index, procedure, isCall);
		}

		private Boolean IsVisibleVariable(ReadOnlySpan<Char> word, Int32 index, ProcedureDeclaration procedure)
		{
			if (procedure.Locals.Count > 0 && procedure.Locals.GetAlternateLookup<ReadOnlySpan<Char>>().TryGetValue(word, out List<VariableDeclarationInfo>? locals))
			{
				foreach (VariableDeclarationInfo local in locals)
				{
					if (local.Token < index)
					{
						return true;
					}
				}
			}

			if (this._globalLookup.TryGetValue(word, out List<VariableDeclarationInfo>? globals))
			{
				foreach (VariableDeclarationInfo global in globals)
				{
					if (global.Token < index)
					{
						return true;
					}
				}
			}

			if (this._environment.Symbols.VariableCount > 0 && index > this.ExternalVisibleFrom && this._environment.Symbols.ContainsVariable(word))
			{
				return true;
			}

			return this._maybeVariables.Count > 0 && this._maybeVariableLookup.Contains(word);
		}

		/// <summary>Finds the procedures a name can refer to at a place: those declared or defined before it, and those of the headers.</summary>
		private Boolean TryGetVisibleProcedure(ReadOnlySpan<Char> word, Int32 index, out Int32 minimum, out Int32 maximum, out Boolean ambiguous)
		{
			Boolean found = false;
			minimum = Int32.MaxValue;
			maximum = 0;
			ambiguous = false;
			if (this._procedureLookup.TryGetValue(word, out List<ProcedureDeclaration>? declarations))
			{
				foreach (ProcedureDeclaration declaration in declarations)
				{
					if (declaration.NameToken < index)
					{
						found = true;
						minimum = Math.Min(minimum, declaration.MinArguments);
						maximum = Math.Max(maximum, declaration.MaxArguments);
					}
				}
			}

			if (this._environment.Symbols.ProcedureCount > 0 && index > this.ExternalVisibleFrom && this._environment.Symbols.TryGetProcedure(word, out ExternalProcedure? external) && external is not null)
			{
				found = true;
				minimum = Math.Min(minimum, external.MinArguments);
				maximum = Math.Max(maximum, external.MaxArguments);
			}

			if (this._maybeProcedures.Count > 0 && this._maybeProcedureLookup.Contains(word))
			{
				found = true;
				ambiguous = true;
			}

			return found;
		}

		private void CheckProcedureUse(Int32 index, ReadOnlySpan<Char> word, Int32 previous, Int32 parentheses, Int32 next, Boolean isCallTarget, Int32 minimum, Int32 maximum, Boolean ambiguous)
		{
			this._referencedProcedures.GetAlternateLookup<ReadOnlySpan<Char>>().Add(word);
			String name = this._tokens.GetText(index);
			Boolean hasParentheses = next < this._count && this.IsPunct(next, '(');
			if (!isCallTarget && parentheses == 0 && this.IsStatementStart(previous) && (hasParentheses || (next < this._count && this.IsPunct(next, ';'))))
			{
				this.Report(ScriptDiagnosticCodes.MissingCall, index, new[] { name });
				return;
			}

			if (ambiguous)
			{
				return;
			}

			Int32 count = -1;
			Boolean uncertain = false;
			if (hasParentheses)
			{
				if (!this.TryReadArguments(next, out Int32 close, out count, out uncertain))
				{
					return;
				}

				if (isCallTarget && this.IsKeyword(this.NextCode(close), "in"))
				{
					this.Report(ScriptDiagnosticCodes.CallInWithArguments, index, new[] { name });
				}
			}
			else if (isCallTarget)
			{
				count = 0;
			}

			if (count >= 0 && !uncertain && (count < minimum || count > maximum))
			{
				this.Report(ScriptDiagnosticCodes.WrongArgumentCount, index, new[] { name, FormatRange(minimum, maximum), count.ToString(System.Globalization.CultureInfo.InvariantCulture) });
			}
		}

		private static String FormatRange(Int32 minimum, Int32 maximum)
		{
			String low = minimum.ToString(System.Globalization.CultureInfo.InvariantCulture);
			if (maximum == Int32.MaxValue)
			{
				return low + "+";
			}

			return minimum == maximum ? low : low + "-" + maximum.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Reads the arguments of a call. <paramref name="open"/> is the <c>(</c>. Returns <see langword="false"/> when the parentheses are not
		/// closed before the end of the statement. The segments of the arguments are left in <see cref="_arguments"/>.
		/// </summary>
		private Boolean TryReadArguments(Int32 open, out Int32 close, out Int32 count, out Boolean uncertain)
		{
			this._arguments.Clear();
			close = -1;
			count = 0;
			uncertain = false;
			Int32 depth = 0;
			Int32 segmentFirst = -1;
			Int32 segmentLast = -1;
			Boolean any = false;
			for (Int32 i = open; i < this._count; i++)
			{
				if (this.Skip(i))
				{
					continue;
				}

				SslToken token = this._tokens[i];
				if (token.Kind == SslTokenKind.Punctuation)
				{
					if (this.IsPunct(i, '(') || this.IsPunct(i, '[') || this.IsPunct(i, '{'))
					{
						depth++;
						if (depth > 1)
						{
							any = true;
							segmentFirst = segmentFirst < 0 ? i : segmentFirst;
							segmentLast = i;
						}

						continue;
					}

					if (this.IsPunct(i, ')') || this.IsPunct(i, ']') || this.IsPunct(i, '}'))
					{
						depth--;
						if (depth == 0)
						{
							close = i;
							if (any)
							{
								count++;
								this._arguments.Add(new ArgumentSegment(segmentFirst, segmentLast));
							}

							return true;
						}

						segmentLast = i;
						continue;
					}

					if (depth == 1 && this.IsPunct(i, ','))
					{
						count++;
						this._arguments.Add(new ArgumentSegment(segmentFirst, segmentLast));
						segmentFirst = -1;
						segmentLast = -1;
						any = true;
						continue;
					}

					if (this.IsPunct(i, ';'))
					{
						return false;
					}
				}
				else if (token.Kind == SslTokenKind.Identifier)
				{
					if (this.IsKeyword(i, "begin") || this.IsKeyword(i, "end") || this.IsKeyword(i, "procedure"))
					{
						return false;
					}

					if (!uncertain)
					{
						MacroTraits? traits = this.LookupMacro(this.Span(i));
						if (traits is not null && traits.SpreadsArguments)
						{
							uncertain = true;
						}
					}
				}

				any = true;
				segmentFirst = segmentFirst < 0 ? i : segmentFirst;
				segmentLast = i;
			}

			return false;
		}

		private void CheckThenOrDo(Int32 index, String keyword, ScriptDiagnosticRule rule)
		{
			if (this._structuralMacroUsed)
			{
				return;
			}

			Int32 depth = 0;
			Int32 i = this.NextCode(index);
			while (i < this._count)
			{
				if (this._tokens[i].Kind == SslTokenKind.Punctuation)
				{
					if (this.IsPunct(i, '('))
					{
						depth++;
					}
					else if (this.IsPunct(i, ')'))
					{
						depth--;
					}
					else if (depth <= 0 && this.IsPunct(i, ';'))
					{
						this.Report(rule, i, Array.Empty<String>());
						return;
					}
				}
				else if (this._tokens[i].Kind == SslTokenKind.Identifier && depth <= 0)
				{
					if (this.IsKeyword(i, keyword))
					{
						return;
					}

					if (this.IsKeyword(i, "begin") || this.IsKeyword(i, "end") || this.IsKeyword(i, "procedure"))
					{
						this.Report(rule, i, Array.Empty<String>());
						return;
					}
				}

				i = this.NextCode(i);
			}

			this.Report(rule, index, Array.Empty<String>());
		}
	}
}