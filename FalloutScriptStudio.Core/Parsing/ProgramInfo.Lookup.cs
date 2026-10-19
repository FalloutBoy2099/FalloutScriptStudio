namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Lookups of symbols: macros, procedures, variables, declarations, references and name conflicts.</summary>
	public sealed partial class ProgramInfo
	{
		/// <summary>
		/// Finds a macro by its (case-sensitive) name: the script's macros first, then the global ones.
		/// </summary>
		/// <param name="token">The macro name.</param>
		/// <returns>The macro, or <c>null</c>.</returns>
		public ScriptMacro? FindMacro(String token)
		{
			if (this.ScriptMacros.TryGetValue(token, out ScriptMacro? macro))
			{
				return macro;
			}

			this.GlobalMacros.TryGetValue(token, out macro);
			return macro;
		}

		/// <summary>
		/// Finds the symbol a word refers to.
		/// </summary>
		/// <param name="token">The word.</param>
		/// <param name="file">Full path of the file the word is in; enables the search of procedure-local variables.</param>
		/// <param name="line">1-based line of the word in <paramref name="file"/>.</param>
		/// <returns>The symbol, or <c>null</c>. Procedures and variables are matched case-insensitively, macros case-sensitively.</returns>
		public IScriptSymbol? Lookup(String token, String? file = null, Int32 line = 0)
		{
			String lower = token.ToLowerInvariant();
			if (file is not null && line > 0)
			{
				foreach (ScriptProcedure procedure in this.Procedures)
				{
					Boolean inside = line == procedure.DeclaredLine || (line >= procedure.StartLine && line <= procedure.EndLine);
					if (!inside || !String.Equals(file, procedure.DeclaredFile, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					foreach (ScriptVariable variable in procedure.Variables)
					{
						if (String.Equals(variable.Name, lower, StringComparison.OrdinalIgnoreCase))
						{
							return variable;
						}
					}
				}
			}

			if (this._procedureLookup.TryGetValue(lower, out ScriptProcedure? found))
			{
				return found;
			}

			if (this._variableLookup.TryGetValue(lower, out ScriptVariable? variableFound))
			{
				return variableFound;
			}

			return this.FindMacro(token);
		}

		/// <summary>Gets the tooltip text of a word.</summary>
		/// <param name="token">The word.</param>
		/// <param name="file">See <see cref="Lookup"/>.</param>
		/// <param name="line">See <see cref="Lookup"/>.</param>
		/// <param name="shortDescription">Whether to return the short description.</param>
		/// <returns>The description, or <c>null</c> when the word is unknown.</returns>
		public String? LookupDescription(String token, String? file, Int32 line, Boolean shortDescription = false)
		{
			IScriptSymbol? symbol = this.Lookup(token, file, line);
			return symbol is null ? null : shortDescription ? symbol.ShortDescription : symbol.FullDescription;
		}

		/// <summary>Gets the kind of the symbol a word refers to.</summary>
		/// <param name="token">The word.</param>
		/// <param name="file">See <see cref="Lookup"/>.</param>
		/// <param name="line">See <see cref="Lookup"/>.</param>
		/// <returns>The kind, or <see cref="SymbolKind.None"/>.</returns>
		public SymbolKind LookupKind(String token, String? file, Int32 line)
		{
			return this.Lookup(token, file, line)?.Kind ?? SymbolKind.None;
		}

		/// <summary>Gets the references of the symbol a word refers to.</summary>
		/// <param name="token">The word.</param>
		/// <param name="file">See <see cref="Lookup"/>.</param>
		/// <param name="line">See <see cref="Lookup"/>.</param>
		/// <returns>The references (empty for macros and for data produced by the internal parser), or <c>null</c> when the word is unknown.</returns>
		public IReadOnlyList<SourceReference>? LookupReferences(String token, String? file, Int32 line)
		{
			return this.Lookup(token, file, line)?.References;
		}

		/// <summary>Gets the declaration of the symbol a word refers to.</summary>
		/// <param name="token">The word.</param>
		/// <param name="file">See <see cref="Lookup"/>.</param>
		/// <param name="line">See <see cref="Lookup"/>.</param>
		/// <returns>The declaration location, or <c>null</c> when the word is unknown.</returns>
		public SourceReference? LookupDeclaration(String token, String? file, Int32 line)
		{
			return this.Lookup(token, file, line)?.Declaration;
		}

		/// <summary>Gets the location of the body of a procedure.</summary>
		/// <param name="token">The procedure name (case-insensitive).</param>
		/// <returns>The location, or <c>null</c> when the procedure is unknown or has no body.</returns>
		public SourceReference? LookupDefinition(String token)
		{
			if (this._procedureLookup.TryGetValue(token.ToLowerInvariant(), out ScriptProcedure? procedure) && procedure.StartLine > 0)
			{
				return new SourceReference(procedure.StartFile, procedure.StartLine);
			}

			return null;
		}

		/// <summary>Gets the declaration line of a procedure.</summary>
		/// <param name="token">The procedure name (case-insensitive).</param>
		/// <returns>The 1-based line, or -1 when the procedure is unknown.</returns>
		public Int32 GetProcedureDeclarationLine(String token)
		{
			return this._procedureLookup.TryGetValue(token.ToLowerInvariant(), out ScriptProcedure? procedure)
				? procedure.DeclaredLine
				: -1;
		}

		/// <summary>Determines whether a procedure with the given name exists.</summary>
		/// <param name="name">The name (case-insensitive).</param>
		/// <returns><c>true</c> when the procedure exists.</returns>
		public Boolean ProcedureExists(String name)
		{
			return this.GetProcedureIndex(name) != -1;
		}

		/// <summary>Gets the index of a procedure in <see cref="Procedures"/>.</summary>
		/// <param name="name">The name (case-insensitive).</param>
		/// <returns>The index, or -1.</returns>
		public Int32 GetProcedureIndex(String name)
		{
			for (Int32 i = 0; i < this.Procedures.Count; i++)
			{
				if (String.Equals(this.Procedures[i].Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}

			return -1;
		}

		/// <summary>Gets a procedure by name.</summary>
		/// <param name="name">The name (case-insensitive).</param>
		/// <returns>The procedure, or <c>null</c>.</returns>
		public ScriptProcedure? GetProcedure(String name)
		{
			Int32 index = this.GetProcedureIndex(name);
			return index != -1 ? this.Procedures[index] : null;
		}

		/// <summary>Gets the procedure whose body contains a line.</summary>
		/// <param name="lineNumber">1-based line.</param>
		/// <returns>The procedure, or <c>null</c> when the line is outside every procedure.</returns>
		public ScriptProcedure? GetProcedureFromPosition(Int32 lineNumber)
		{
			return this.Procedures.FirstOrDefault(p => lineNumber >= p.StartLine && lineNumber <= p.EndLine);
		}

		/// <summary>
		/// Gets the procedure nearest to a line: the first one that starts after the line, or the last one when there is none.
		/// </summary>
		/// <param name="lineNumber">1-based line.</param>
		/// <returns>The procedure, or <c>null</c> when the script has no procedures.</returns>
		public ScriptProcedure? GetNearProcedure(Int32 lineNumber)
		{
			if (this.Procedures.Count == 0)
			{
				return null;
			}

			List<ScriptProcedure> ordered = this.Procedures.OrderBy(p => p.StartLine).ToList();
			foreach (ScriptProcedure procedure in ordered)
			{
				if (lineNumber < procedure.StartLine)
				{
					return procedure;
				}
			}

			return ordered[ordered.Count - 1];
		}

		/// <summary>Gets the first procedure with a body, in file order.</summary>
		/// <returns>The procedure, or <c>null</c>.</returns>
		public ScriptProcedure? GetTopProcedure()
		{
			ScriptProcedure? top = null;
			foreach (ScriptProcedure procedure in this.Procedures)
			{
				if (procedure.StartLine == -1)
				{
					continue;
				}

				if (top is null || procedure.StartLine < top.StartLine)
				{
					top = procedure;
				}
			}

			return top;
		}

		/// <summary>
		/// Determines whether a name is already used (legacy <c>CheckExistsName</c>).
		/// </summary>
		/// <param name="name">The name to check.</param>
		/// <param name="kind">
		/// What the name will be used for. A macro, global variable or procedure conflicts with anything except a local
		/// variable; a local variable conflicts only with another local variable.
		/// </param>
		/// <param name="file">File where the name will be used (for local variables).</param>
		/// <param name="line">1-based line where the name will be used.</param>
		/// <returns><c>true</c> when the name is already declared.</returns>
		public Boolean NameExists(String name, SymbolKind kind, String? file = null, Int32 line = 0)
		{
			if (!this.HasNativeData)
			{
				return this.ContainsName(name, true, true);
			}

			SymbolKind existing = this.LookupKind(name, file, line);
			if (existing == SymbolKind.None)
			{
				return false;
			}

			return kind switch
			{
				SymbolKind.Macro or SymbolKind.GlobalVariable or SymbolKind.Procedure => existing != SymbolKind.LocalVariable,
				SymbolKind.LocalVariable => existing == SymbolKind.LocalVariable,
				_ => false,
			};
		}

		/// <summary>
		/// Determines whether a procedure or script macro with the name exists; used when no native data is available.
		/// </summary>
		/// <param name="name">The name to check.</param>
		/// <param name="countDeclarationOnly">Whether a procedure that has only a declaration (no body) counts as existing.</param>
		/// <param name="checkMacros">Whether to look at the script's macros too (case-sensitive).</param>
		/// <returns><c>true</c> when the name exists.</returns>
		public Boolean ContainsName(String name, Boolean countDeclarationOnly, Boolean checkMacros)
		{
			foreach (ScriptProcedure procedure in this.Procedures)
			{
				if (String.Equals(procedure.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					if (countDeclarationOnly || procedure.StartLine != -1)
					{
						return true;
					}

					break;
				}
			}

			return checkMacros && this.ScriptMacros.ContainsKey(name);
		}
	}
}