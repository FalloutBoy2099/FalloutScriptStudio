namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	/// <summary>
	/// Everything the parsers know about one script: its procedures, variables and macros, with the lookups used by
	/// tooltips, navigation, refactoring and autocomplete (legacy <c>ProgramInfo</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// An instance is an immutable snapshot, safe to share between threads; a new parse produces a new instance.
	/// The <c>With...</c> methods return modified copies.
	/// </para>
	/// <para>
	/// Line numbers are 1-based (the first line of a file is line 1) and -1 means "unknown". Methods that take a line
	/// number (<see cref="Lookup"/>, <see cref="GetProcedureFromPosition"/>, <see cref="GetNearProcedure"/>) expect the same
	/// 1-based numbering, which matches editor document lines; the legacy API took 0-based lines for the last two.
	/// </para>
	/// </remarks>
	public sealed class ProgramInfo
	{
		private const String ShortCircuitMacro = "SFALL_SC_EVALUATION";
		private const String ScriptNameMacro = "NAME";
		private const Int32 MaxAliasDepth = 16;
		private const Int32 MaxMacroPreview = 300;

		private readonly Dictionary<String, ScriptProcedure> _procedureLookup;
		private readonly Dictionary<String, ScriptVariable> _variableLookup;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgramInfo"/> class.
		/// </summary>
		/// <param name="procedures">The procedures of the script and of its includes.</param>
		/// <param name="variables">The script-level variables.</param>
		/// <param name="scriptMacros">The macros of the script and of its includes, keyed by token.</param>
		/// <param name="globalMacros">The macros shared by all scripts; an empty registry is used when <c>null</c>.</param>
		/// <param name="scriptFilePath">Full path of the script this information was produced for.</param>
		public ProgramInfo(
			IEnumerable<ScriptProcedure> procedures,
			IEnumerable<ScriptVariable> variables,
			IReadOnlyDictionary<String, ScriptMacro>? scriptMacros = null,
			MacroRegistry? globalMacros = null,
			String? scriptFilePath = null)
		{
			this.Procedures = procedures.ToArray();
			this.Variables = variables.ToArray();
			this.ScriptMacros = scriptMacros ?? new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			this.GlobalMacros = globalMacros ?? new MacroRegistry();
			this.ScriptFilePath = scriptFilePath;
			this._procedureLookup = BuildProcedureLookup(this.Procedures);
			this._variableLookup = BuildVariableLookup(this.Variables);
		}

		private ProgramInfo(ProgramInfo source, IReadOnlyList<ScriptProcedure> procedures)
		{
			this.Procedures = procedures;
			this.Variables = source.Variables;
			this.ScriptMacros = source.ScriptMacros;
			this.GlobalMacros = source.GlobalMacros;
			this.ScriptFilePath = source.ScriptFilePath;
			this.IsParsed = source.IsParsed;
			this.HasNativeData = source.HasNativeData;
			this.HasParseError = source.HasParseError;
			this.NeedsDataRefresh = source.NeedsDataRefresh;
			this._procedureLookup = BuildProcedureLookup(procedures);
			this._variableLookup = source._variableLookup;
		}

		private ProgramInfo(ProgramInfo source)
		{
			this.Procedures = source.Procedures;
			this.Variables = source.Variables;
			this.ScriptMacros = source.ScriptMacros;
			this.GlobalMacros = source.GlobalMacros;
			this.ScriptFilePath = source.ScriptFilePath;
			this.IsParsed = source.IsParsed;
			this.HasNativeData = source.HasNativeData;
			this.HasParseError = source.HasParseError;
			this.NeedsDataRefresh = source.NeedsDataRefresh;
			this._procedureLookup = source._procedureLookup;
			this._variableLookup = source._variableLookup;
		}

		/// <summary>Gets the procedures, in the order they appear in the script.</summary>
		public IReadOnlyList<ScriptProcedure> Procedures { get; }

		/// <summary>Gets the script-level variables.</summary>
		public IReadOnlyList<ScriptVariable> Variables { get; }

		/// <summary>Gets the macros of the script and of the files it includes, keyed by token (case-sensitive).</summary>
		public IReadOnlyDictionary<String, ScriptMacro> ScriptMacros { get; private init; }

		/// <summary>Gets the macros shared by all scripts.</summary>
		public MacroRegistry GlobalMacros { get; private init; }

		/// <summary>Gets the full path of the script this information was produced for, or <c>null</c> for an unsaved script.</summary>
		public String? ScriptFilePath { get; private init; }

		/// <summary>Gets a value indicating whether at least one parser has processed the script.</summary>
		public Boolean IsParsed { get; private init; }

		/// <summary>
		/// Gets a value indicating whether the procedure and variable data come from the native parser
		/// (references, local variables and arguments are available) — legacy <c>parseData</c>.
		/// </summary>
		public Boolean HasNativeData { get; private init; }

		/// <summary>Gets a value indicating whether the last native parse reported errors in the script.</summary>
		public Boolean HasParseError { get; private init; }

		/// <summary>
		/// Gets a value indicating that the file path of the script changed (Save As) and the data must be re-bound
		/// to the new path on the next parse (legacy <c>reParseData</c>).
		/// </summary>
		public Boolean NeedsDataRefresh { get; private init; }

		/// <summary>Gets the value of the <c>SFALL_SC_EVALUATION</c> macro (short-circuit evaluation); <c>false</c> when it is not defined.</summary>
		/// <remarks>The legacy code accepted only <c>True</c>/<c>False</c>; numeric values such as <c>1</c> are accepted here as well.</remarks>
		public Boolean ShortCircuitEvaluation
		{
			get
			{
				if (!this.ScriptMacros.TryGetValue(ShortCircuitMacro, out ScriptMacro? macro))
				{
					return false;
				}

				String value = GetMacroValue(macro);
				if (Boolean.TryParse(value, out Boolean flag))
				{
					return flag;
				}

				return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 number) && number != 0;
			}
		}

		/// <summary>
		/// Gets the script ID defined by the <c>NAME</c> macro (following macro aliases), or -1 when it cannot be determined.
		/// </summary>
		public Int32 ScriptNameId
		{
			get
			{
				if (!this.ScriptMacros.TryGetValue(ScriptNameMacro, out ScriptMacro? macro))
				{
					return -1;
				}

				for (Int32 depth = 0; depth < MaxAliasDepth; depth++)
				{
					String value = GetMacroValue(macro);
					if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 id))
					{
						return id;
					}

					ScriptMacro? alias = this.FindMacro(value);
					if (alias is null)
					{
						return -1;
					}

					macro = alias;
				}

				return -1;
			}
		}

		/// <summary>Returns a copy with other procedures.</summary>
		/// <param name="procedures">The new procedures.</param>
		/// <returns>The new snapshot.</returns>
		public ProgramInfo WithProcedures(IEnumerable<ScriptProcedure> procedures)
		{
			return new ProgramInfo(this, procedures.ToArray());
		}

		/// <summary>Returns a copy with other script macros.</summary>
		/// <param name="scriptMacros">The new macros.</param>
		/// <returns>The new snapshot.</returns>
		public ProgramInfo WithScriptMacros(IReadOnlyDictionary<String, ScriptMacro> scriptMacros)
		{
			return new ProgramInfo(this) { ScriptMacros = scriptMacros };
		}

		/// <summary>Returns a copy bound to another script path.</summary>
		/// <param name="scriptFilePath">The new path.</param>
		/// <returns>The new snapshot.</returns>
		public ProgramInfo WithScriptFilePath(String? scriptFilePath)
		{
			return new ProgramInfo(this) { ScriptFilePath = scriptFilePath };
		}

		/// <summary>Returns a copy using another global macro registry.</summary>
		/// <param name="globalMacros">The registry.</param>
		/// <returns>The new snapshot.</returns>
		public ProgramInfo WithGlobalMacros(MacroRegistry globalMacros)
		{
			return new ProgramInfo(this) { GlobalMacros = globalMacros };
		}

		/// <summary>Returns a copy with changed state flags; arguments left <c>null</c> keep their value.</summary>
		/// <param name="isParsed">New value of <see cref="IsParsed"/>.</param>
		/// <param name="hasNativeData">New value of <see cref="HasNativeData"/>.</param>
		/// <param name="hasParseError">New value of <see cref="HasParseError"/>.</param>
		/// <param name="needsDataRefresh">New value of <see cref="NeedsDataRefresh"/>.</param>
		/// <returns>The new snapshot.</returns>
		public ProgramInfo WithFlags(Boolean? isParsed = null, Boolean? hasNativeData = null, Boolean? hasParseError = null, Boolean? needsDataRefresh = null)
		{
			return new ProgramInfo(this)
			{
				IsParsed = isParsed ?? this.IsParsed,
				HasNativeData = hasNativeData ?? this.HasNativeData,
				HasParseError = hasParseError ?? this.HasParseError,
				NeedsDataRefresh = needsDataRefresh ?? this.NeedsDataRefresh,
			};
		}

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

		/// <summary>
		/// Builds the autocomplete list for the word being typed (legacy <c>LookupAutosuggest</c>).
		/// </summary>
		/// <param name="part">The beginning of the word.</param>
		/// <param name="opcodes">Optional opcode catalog whose matches are listed first.</param>
		/// <returns>
		/// The matching opcodes, procedures, variables and macros (in this order) with duplicated names removed.
		/// </returns>
		public IReadOnlyList<AutosuggestItem> GetAutosuggest(String part, OpcodeCatalog? opcodes = null)
		{
			var matches = new List<AutosuggestItem>();
			if (opcodes is not null)
			{
				matches.AddRange(opcodes.Lookup(part));
			}

			String lower = part.ToLowerInvariant();
			foreach (KeyValuePair<String, ScriptProcedure> entry in this._procedureLookup)
			{
				if (entry.Key.StartsWith(lower, StringComparison.Ordinal))
				{
					matches.Add(new AutosuggestItem(entry.Value.Name, entry.Value.ShortDescription, AutosuggestKind.Procedure));
				}
			}

			foreach (KeyValuePair<String, ScriptVariable> entry in this._variableLookup)
			{
				if (entry.Key.StartsWith(lower, StringComparison.Ordinal))
				{
					matches.Add(new AutosuggestItem(entry.Value.Name, entry.Value.FullDescription, AutosuggestKind.Variable));
				}
			}

			var macros = new SortedDictionary<String, AutosuggestItem>(StringComparer.Ordinal);
			foreach (KeyValuePair<String, ScriptMacro> entry in this.ScriptMacros)
			{
				if (entry.Key.StartsWith(part, StringComparison.OrdinalIgnoreCase))
				{
					macros[entry.Value.DefinitionName] = CreateMacroItem(entry.Value);
				}
			}

			foreach (KeyValuePair<String, ScriptMacro> entry in this.GlobalMacros.Macros)
			{
				if (entry.Key.StartsWith(part, StringComparison.OrdinalIgnoreCase) && !macros.ContainsKey(entry.Value.DefinitionName))
				{
					macros[entry.Value.DefinitionName] = CreateMacroItem(entry.Value);
				}
			}

			matches.AddRange(macros.Values);

			var seen = new HashSet<String>(StringComparer.Ordinal);
			return matches.Where(m => seen.Add(m.Name)).ToList();
		}

		private static AutosuggestItem CreateMacroItem(ScriptMacro macro)
		{
			String preview = macro.Code.Length > MaxMacroPreview ? "No preview macros." : macro.Code;
			return new AutosuggestItem(macro.DefinitionName, "Define:\n" + preview, AutosuggestKind.Macro);
		}

		private static String GetMacroValue(ScriptMacro macro)
		{
			String value = macro.Code.Trim('(', ')');
			Int32 index = value.IndexOf(')');
			return index > 0 ? value.Remove(index) : value;
		}

		private static Dictionary<String, ScriptProcedure> BuildProcedureLookup(IReadOnlyList<ScriptProcedure> procedures)
		{
			var lookup = new Dictionary<String, ScriptProcedure>(procedures.Count, StringComparer.Ordinal);
			foreach (ScriptProcedure procedure in procedures)
			{
				lookup.TryAdd(procedure.NormalizedName, procedure);
			}

			return lookup;
		}

		private static Dictionary<String, ScriptVariable> BuildVariableLookup(IReadOnlyList<ScriptVariable> variables)
		{
			var lookup = new Dictionary<String, ScriptVariable>(variables.Count, StringComparer.Ordinal);
			foreach (ScriptVariable variable in variables)
			{
				lookup.TryAdd(variable.Name.ToLowerInvariant(), variable);
			}

			return lookup;
		}
	}
}
