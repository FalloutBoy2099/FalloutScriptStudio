namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Parsing;

	/// <summary>The checks that need the whole outline: duplicates, forward declarations and the structure.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		private void Finish()
		{
			if (!this._structuralMacroUsed)
			{
				foreach (PendingDiagnostic pending in this._structure)
				{
					this.Report(pending.Rule, pending.Token, pending.Arguments);
				}
			}

			this.CheckProcedureGroups();
			this.CheckVariableDuplicates();
		}

		private void CheckProcedureGroups()
		{
			Boolean reliable = this._environment.IsComplete && this._globalTaintFrom < 0;
			foreach (KeyValuePair<String, List<ProcedureDeclaration>> entry in this._proceduresByName)
			{
				this._cancellation.ThrowIfCancellationRequested();
				List<ProcedureDeclaration> group = entry.Value;
				Boolean ambiguous = this._maybeProcedures.Contains(entry.Key);
				ProcedureDeclaration? definition = null;
				ProcedureDeclaration? forward = null;
				Boolean declaredElsewhere = false;
				foreach (ProcedureDeclaration declaration in group)
				{
					switch (declaration.Kind)
					{
						case ProcedureKind.Definition:
							if (definition is null)
							{
								definition = declaration;
							}
							else if (!ambiguous && declaration.Frame == definition.Frame)
							{
								this.Report(ScriptDiagnosticCodes.DuplicateProcedure, declaration.NameToken, new[] { declaration.Name, this.LineOf(definition.NameToken) });
							}

							break;
						case ProcedureKind.Forward:
							if (definition is not null && !ambiguous && declaration.Frame == definition.Frame)
							{
								this.Report(ScriptDiagnosticCodes.DuplicateProcedure, declaration.NameToken, new[] { declaration.Name, this.LineOf(definition.NameToken) }, this.CreateStatementRemoval(declaration));
							}
							else
							{
								forward ??= declaration;
							}

							break;
						default:
							declaredElsewhere = true;
							break;
					}
				}

				if (definition is not null && forward is not null && !ambiguous && forward.Frame == definition.Frame && forward.NameToken < definition.NameToken)
				{
					if (forward.MaxArguments != definition.MaxArguments)
					{
						this.Report(
							ScriptDiagnosticCodes.DeclarationMismatch,
							definition.NameToken,
							new[] { definition.Name, forward.MaxArguments.ToString(CultureInfo.InvariantCulture), definition.MaxArguments.ToString(CultureInfo.InvariantCulture) });
					}
					else if (definition.HasDefaults)
					{
						foreach (ParameterDeclaration parameter in definition.Parameters)
						{
							if (parameter.HasDefault)
							{
								this.Report(ScriptDiagnosticCodes.DefaultOnForwardDeclared, parameter.Token, new[] { definition.Name });
								break;
							}
						}
					}
				}

				if (definition is null && forward is not null && !declaredElsewhere && !ambiguous && reliable
					&& (this._referencedProcedures.Contains(entry.Key) || ScriptProcedure.IsStandardHandlerName(entry.Key)) && !this.HasExternalBody(entry.Key))
				{
					this.Report(ScriptDiagnosticCodes.ProcedureNeverDefined, forward.NameToken, new[] { forward.Name });
				}
			}
		}

		private Boolean HasExternalBody(String name)
		{
			return this._environment.Symbols.TryGetProcedure(name, out ExternalProcedure? external) && external is not null && external.HasBody;
		}

		private void CheckVariableDuplicates()
		{
			foreach (List<VariableDeclarationInfo> list in this._globals.Values)
			{
				this.ReportDuplicates(list);
			}

			foreach (ProcedureDeclaration procedure in this._procedures)
			{
				if (procedure.Kind != ProcedureKind.Definition)
				{
					continue;
				}

				foreach (List<VariableDeclarationInfo> list in procedure.Locals.Values)
				{
					if (list.Count > 1)
					{
						this.ReportDuplicates(list);
					}
				}
			}
		}

		private void ReportDuplicates(List<VariableDeclarationInfo> list)
		{
			VariableDeclarationInfo? first = null;
			foreach (VariableDeclarationInfo declaration in list)
			{
				if (declaration.Scope == VariableScopeKind.Loop)
				{
					continue;
				}

				if (first is null)
				{
					first = declaration;
					continue;
				}

				if (declaration.Frame != first.Frame)
				{
					continue;
				}

				ScriptQuickFix? fix = null;
				if (!declaration.HasInitializer && declaration.Scope != VariableScopeKind.Parameter)
				{
					SslToken name = this._tokens[declaration.Token];
					fix = new ScriptQuickFix(ScriptQuickFix.RemoveDeclarationId, ScriptQuickFixes.VariableTarget, name.Offset, name.Length);
				}

				this.Report(ScriptDiagnosticCodes.DuplicateVariable, declaration.Token, new[] { declaration.Name, this.LineOf(first.Token) }, fix);
			}
		}

		private String LineOf(Int32 token)
		{
			return this._tokens[token].Line.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Creates the fix that removes a forward declaration, <c>procedure name;</c>, with its modifiers.</summary>
		private ScriptQuickFix? CreateStatementRemoval(ProcedureDeclaration declaration)
		{
			if (declaration.SemicolonToken < 0)
			{
				return null;
			}

			Int32 start = this._tokens[declaration.FirstToken].Offset;
			Int32 end = this._tokens[declaration.SemicolonToken].End;
			return new ScriptQuickFix(ScriptQuickFix.RemoveDeclarationId, ScriptQuickFixes.StatementTarget, start, end - start);
		}

		/// <summary>
		/// Creates the fix that adds a forward declaration for a procedure that is used before it is defined: the declaration is built
		/// from the header of the procedure and put in front of the first procedure of the script.
		/// </summary>
		private ScriptQuickFix? CreateForwardDeclarationFix(List<ProcedureDeclaration> declarations)
		{
			ProcedureDeclaration? source = null;
			foreach (ProcedureDeclaration declaration in declarations)
			{
				if (declaration.Kind is ProcedureKind.Definition or ProcedureKind.Forward)
				{
					source = declaration;
					break;
				}
			}

			if (source is null || source.HasDefaults || source.HasModifiers || source.IsImport || source.IsExport)
			{
				return null;
			}

			Int32 first = -1;
			foreach (ProcedureDeclaration declaration in this._procedures)
			{
				if (declaration.Frame == 0 && (first < 0 || declaration.FirstToken < first))
				{
					first = declaration.FirstToken;
				}
			}

			if (first < 0)
			{
				return null;
			}

			String text = this._tokens.Text;
			Int32 offset = this._tokens[first].Offset;
			while (offset > 0 && text[offset - 1] != '\n' && text[offset - 1] != '\r')
			{
				offset--;
			}

			System.Text.StringBuilder declarationText = new System.Text.StringBuilder("procedure ");
			declarationText.Append(source.Name);
			if (source.Parameters.Count > 0)
			{
				declarationText.Append('(');
				for (Int32 i = 0; i < source.Parameters.Count; i++)
				{
					if (i > 0)
					{
						declarationText.Append(", ");
					}

					declarationText.Append("variable ").Append(source.Parameters[i].Name);
				}

				declarationText.Append(')');
			}

			declarationText.Append(';');
			return new ScriptQuickFix(ScriptQuickFix.ForwardDeclareId, declarationText.ToString(), offset, 0);
		}
	}
}