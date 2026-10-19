namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Finds where the declared variables of a script are named: every identifier is resolved to a parameter or local variable of the
	/// procedure it is in, or else to a script level variable. A name used on a preprocessor line (a macro body can mention any variable)
	/// counts as a use of every variable of that name.
	/// </summary>
	public static class VariableOccurrenceFinder
	{
		/// <summary>Finds the occurrences of the variables of a script.</summary>
		/// <param name="model">The model of the script.</param>
		/// <returns>The occurrences in text order; the declarations themselves are not listed.</returns>
		public static IReadOnlyList<VariableOccurrence> Find(SslScriptModel model)
		{
			ArgumentNullException.ThrowIfNull(model);

			SslTokenStream tokens = model.Tokens;
			List<VariableOccurrence> result = [];
			if (model.Declarations.Count == 0)
			{
				return result;
			}

			Dictionary<String, List<VariableDeclaration>> byName = new(StringComparer.OrdinalIgnoreCase);
			Dictionary<String, VariableDeclaration> globals = new(StringComparer.OrdinalIgnoreCase);
			HashSet<Int32> declarationTokens = [];
			foreach (VariableDeclaration declaration in model.Declarations)
			{
				declarationTokens.Add(declaration.TokenIndex);
				if (!byName.TryGetValue(declaration.Name, out List<VariableDeclaration>? list))
				{
					list = new(1);
					byName.Add(declaration.Name, list);
				}

				list.Add(declaration);
				if (declaration.Kind is VariableDeclarationKind.Global or VariableDeclarationKind.Import or VariableDeclarationKind.Export)
				{
					globals.TryAdd(declaration.Name, declaration);
				}
			}

			Dictionary<String, List<VariableDeclaration>>.AlternateLookup<ReadOnlySpan<Char>> lookup = byName.GetAlternateLookup<ReadOnlySpan<Char>>();
			Int32 procedureIndex = 0;
			SslProcedureModel? current = null;
			Dictionary<String, VariableDeclaration>? locals = null;
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				SslToken token = tokens[i];
				while (procedureIndex < model.Procedures.Count && i > model.Procedures[procedureIndex].EndToken)
				{
					procedureIndex++;
				}

				SslProcedureModel? inside = procedureIndex < model.Procedures.Count && i >= model.Procedures[procedureIndex].BeginToken ? model.Procedures[procedureIndex] : null;
				if (!ReferenceEquals(inside, current))
				{
					current = inside;
					locals = current is null ? null : BuildLocals(current);
				}

				if (token.Kind != SslTokenKind.Identifier || declarationTokens.Contains(i))
				{
					continue;
				}

				if (!lookup.TryGetValue(tokens.Span(i), out List<VariableDeclaration>? candidates))
				{
					continue;
				}

				if (token.InDirective)
				{
					foreach (VariableDeclaration declaration in candidates)
					{
						result.Add(new VariableOccurrence(i, declaration, false));
					}

					continue;
				}

				String name = candidates[0].Name;
				VariableDeclaration? target = null;
				if (locals is not null && locals.TryGetValue(name, out VariableDeclaration? local))
				{
					target = local;
				}
				else if (globals.TryGetValue(name, out VariableDeclaration? global))
				{
					target = global;
				}

				if (target is not null)
				{
					result.Add(new VariableOccurrence(i, target, IsAssignmentTarget(tokens, i)));
				}
			}

			return result;
		}

		/// <summary>Determines whether the identifier at a token is only assigned to (<c>name := value;</c> or <c>name = value;</c> as a statement).</summary>
		/// <param name="tokens">The tokens.</param>
		/// <param name="index">The index of the identifier.</param>
		/// <returns><see langword="true"/> when the identifier starts a statement and is followed by <c>:=</c>.</returns>
		public static Boolean IsAssignmentTarget(SslTokenStream tokens, Int32 index)
		{
			ArgumentNullException.ThrowIfNull(tokens);
			return (tokens.IsPunctuation(index + 1, ":=") || tokens.IsPunctuation(index + 1, '=')) && IsStatementStart(tokens, index);
		}

		/// <summary>Determines whether a token can be the first token of a statement.</summary>
		/// <param name="tokens">The tokens.</param>
		/// <param name="index">The index of the token.</param>
		/// <returns><see langword="true"/> when the token follows a <c>;</c>, a block keyword, a label, a preprocessor line, or nothing.</returns>
		public static Boolean IsStatementStart(SslTokenStream tokens, Int32 index)
		{
			ArgumentNullException.ThrowIfNull(tokens);

			if (index <= 0)
			{
				return true;
			}

			SslToken previous = tokens[index - 1];
			if (previous.InDirective)
			{
				return true;
			}

			if (previous.Kind == SslTokenKind.Punctuation)
			{
				return tokens.IsPunctuation(index - 1, ';') || tokens.IsPunctuation(index - 1, ':');
			}

			return previous.Kind == SslTokenKind.Identifier
				&& (tokens.IsKeyword(index - 1, "begin") || tokens.IsKeyword(index - 1, "end") || tokens.IsKeyword(index - 1, "then")
					|| tokens.IsKeyword(index - 1, "else") || tokens.IsKeyword(index - 1, "do"));
		}

		private static Dictionary<String, VariableDeclaration> BuildLocals(SslProcedureModel procedure)
		{
			Dictionary<String, VariableDeclaration> locals = new(StringComparer.OrdinalIgnoreCase);
			foreach (VariableDeclaration declaration in procedure.Declarations)
			{
				locals.TryAdd(declaration.Name, declaration);
			}

			return locals;
		}
	}
}