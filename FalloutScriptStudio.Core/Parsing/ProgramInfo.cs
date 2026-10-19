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
	public sealed partial class ProgramInfo
	{
		private const String ShortCircuitMacro = "SFALL_SC_EVALUATION";
		private const String ScriptNameMacro = "NAME";
		private const Int32 MaxAliasDepth = 16;

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