namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>Reports for names that were not found, and the checks of the calls of engine and sfall functions.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		/// <summary>Explains a name that is not visible but is declared somewhere else: further down, or in another procedure.</summary>
		/// <returns><see langword="true"/> when a report was made.</returns>
		private Boolean ReportDeclaredElsewhere(Int32 index, ReadOnlySpan<Char> word, ProcedureDeclaration procedure)
		{
			String name = this._tokens.GetText(index);
			if (procedure.Locals.Count > 0 && procedure.Locals.GetAlternateLookup<ReadOnlySpan<Char>>().TryGetValue(word, out List<VariableDeclarationInfo>? locals) && locals.Count > 0)
			{
				this.Report(ScriptDiagnosticCodes.VariableUsedBeforeDeclaration, index, new[] { name, this._tokens[locals[0].Token].Line.ToString(CultureInfo.InvariantCulture) }, null, true);
				return true;
			}

			if (this._globalLookup.TryGetValue(word, out List<VariableDeclarationInfo>? globals) && globals.Count > 0)
			{
				this.Report(ScriptDiagnosticCodes.VariableUsedBeforeDeclaration, index, new[] { name, this._tokens[globals[0].Token].Line.ToString(CultureInfo.InvariantCulture) }, null, true);
				return true;
			}

			if (this._procedureLookup.TryGetValue(word, out List<ProcedureDeclaration>? declarations) && declarations.Count > 0)
			{
				ProcedureDeclaration first = declarations[0];
				this.Report(ScriptDiagnosticCodes.ProcedureUsedBeforeDefinition, index, new[] { name, this._tokens[first.NameToken].Line.ToString(CultureInfo.InvariantCulture) }, this.CreateForwardDeclarationFix(declarations), true);
				return true;
			}

			if (this._ownerLookup.TryGetValue(word, out ProcedureDeclaration? owner) && !Object.ReferenceEquals(owner, procedure))
			{
				this.Report(ScriptDiagnosticCodes.VariableOutOfScope, index, new[] { name, owner.Name }, null, true);
				return true;
			}

			return false;
		}

		private void ReportUndefined(Int32 index, ProcedureDeclaration procedure, Boolean isCall)
		{
			String name = this._tokens.GetText(index);
			ScriptQuickFix? fix = null;
			if (this._options.SuggestNames)
			{
				String key = isCall ? name + "(" : name;
				if (!this._suggestions.TryGetValue(key, out String? suggestion))
				{
					suggestion = NameSuggester.Suggest(name, this.EnumerateCandidates(procedure, isCall));
					this._suggestions[key] = suggestion;
				}

				if (suggestion is not null)
				{
					fix = new ScriptQuickFix(ScriptQuickFix.RenameId, suggestion, this._tokens[index].Offset, this._tokens[index].Length);
				}
			}

			this.Report(isCall ? ScriptDiagnosticCodes.UndefinedProcedure : ScriptDiagnosticCodes.UndefinedVariable, index, new[] { name }, fix, true);
		}

		/// <summary>The names a misspelled one can be compared with, the most likely first.</summary>
		private IEnumerable<String> EnumerateCandidates(ProcedureDeclaration procedure, Boolean isCall)
		{
			if (isCall)
			{
				foreach (String name in this.EnumerateProcedureNames())
				{
					yield return name;
				}

				foreach (String name in this.EnumerateFunctionNames())
				{
					yield return name;
				}

				foreach (String name in this.EnumerateVariableNames(procedure))
				{
					yield return name;
				}
			}
			else
			{
				foreach (String name in this.EnumerateVariableNames(procedure))
				{
					yield return name;
				}

				foreach (String name in this.EnumerateProcedureNames())
				{
					yield return name;
				}

				foreach (String name in this.EnumerateFunctionNames())
				{
					yield return name;
				}
			}
		}

		private IEnumerable<String> EnumerateProcedureNames()
		{
			foreach (List<ProcedureDeclaration> declarations in this._proceduresByName.Values)
			{
				yield return declarations[0].Name;
			}

			foreach (String name in this._environment.Symbols.ProcedureNames)
			{
				yield return name;
			}
		}

		private IEnumerable<String> EnumerateVariableNames(ProcedureDeclaration procedure)
		{
			foreach (VariableDeclarationInfo local in procedure.LocalList)
			{
				yield return local.Name;
			}

			foreach (List<VariableDeclarationInfo> globals in this._globals.Values)
			{
				yield return globals[0].Name;
			}

			foreach (String name in this._environment.Symbols.VariableNames)
			{
				yield return name;
			}
		}

		private IEnumerable<String> EnumerateFunctionNames()
		{
			foreach (String name in this._environment.Functions.Names)
			{
				if (!ScriptBuiltins.IsKeyword(name))
				{
					yield return name;
				}
			}

			foreach (String name in ScriptBuiltins.Names)
			{
				if (!ScriptBuiltins.IsKeyword(name))
				{
					yield return name;
				}
			}
		}

		/// <summary>Checks a call of a function of the engine or of sfall against its description.</summary>
		private void CheckFunctionCall(Int32 index, ReadOnlySpan<Char> word, Int32 next, Boolean isCallTarget)
		{
			if (!this._options.CheckFunctionCalls || isCallTarget || next >= this._count || !this.IsPunct(next, '('))
			{
				return;
			}

			if (!this._environment.Functions.TryGetSignatures(word, out FunctionSignature[] signatures) || signatures.Length == 0)
			{
				return;
			}

			if (!this.TryReadArguments(next, out _, out Int32 count, out Boolean uncertain) || uncertain)
			{
				return;
			}

			String name = this._tokens.GetText(index);
			Boolean allDeprecated = true;
			Int32 minimum = Int32.MaxValue;
			Int32 maximum = 0;
			Boolean anyParentheses = false;
			FunctionSignature? match = null;
			foreach (FunctionSignature signature in signatures)
			{
				allDeprecated &= signature.IsDeprecated;
				minimum = Math.Min(minimum, signature.MinArguments);
				maximum = Math.Max(maximum, signature.MaxArguments);
				anyParentheses |= signature.TakesParentheses;
				if (match is null && signature.Accepts(count) && (count > 0 || signature.TakesParentheses))
				{
					match = signature;
				}
			}

			if (allDeprecated)
			{
				this.Report(ScriptDiagnosticCodes.DeprecatedFunction, index, new[] { name });
			}

			if (match is null)
			{
				if (count == 0 && !anyParentheses)
				{
					this.Report(ScriptDiagnosticCodes.FunctionTakesNoParentheses, index, new[] { name });
				}
				else
				{
					this.Report(ScriptDiagnosticCodes.FunctionArgumentCount, index, new[] { name, FormatRange(minimum, maximum), count.ToString(CultureInfo.InvariantCulture) });
				}

				return;
			}

			this.CheckArgumentTypes(name, match);
		}

		private void CheckArgumentTypes(String name, FunctionSignature signature)
		{
			Int32 limit = Math.Min(this._arguments.Count, signature.Parameters.Count);
			for (Int32 i = 0; i < limit; i++)
			{
				FunctionParameter parameter = signature.Parameters[i];
				ArgumentSegment segment = this._arguments[i];
				if (parameter.IsVariadic)
				{
					return;
				}

				if (segment.First < 0 || segment.First != segment.Last)
				{
					continue;
				}

				SslTokenKind kind = this._tokens[segment.First].Kind;
				String type = parameter.Type.ToLowerInvariant();
				if (kind == SslTokenKind.String && IsNumericType(type))
				{
					this.Report(ScriptDiagnosticCodes.FunctionArgumentType, segment.First, new[] { name, (i + 1).ToString(CultureInfo.InvariantCulture), parameter.Type, "string" });
				}
				else if (kind == SslTokenKind.Number && type == "string" && this._options.ReportNumbersForStrings)
				{
					this.Report(ScriptDiagnosticCodes.FunctionArgumentType, segment.First, new[] { name, (i + 1).ToString(CultureInfo.InvariantCulture), parameter.Type, "number" }, null, false, ScriptDiagnosticSeverity.Info);
				}
			}
		}

		private static Boolean IsNumericType(String type)
		{
			return type is "int" or "unsigned int" or "boolean" or "bool" or "float" or "objectptr" or "long" or "short" or "byte" or "dword";
		}
	}
}