namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>A procedure that the headers of a script declare or define.</summary>
	/// <param name="Name">The name as written (the first declaration).</param>
	/// <param name="MinArguments">The fewest arguments any declaration accepts.</param>
	/// <param name="MaxArguments">The most arguments any declaration accepts.</param>
	/// <param name="HasBody">Whether a header defines the procedure.</param>
	public sealed record ExternalProcedure(String Name, Int32 MinArguments, Int32 MaxArguments, Boolean HasBody);

	/// <summary>The procedures and variables that the files included by a script declare, ready to be looked up by name (the case is ignored).</summary>
	public sealed class ExternalSymbols
	{
		private readonly Dictionary<String, ExternalProcedure> _procedures;
		private readonly HashSet<String> _variables;
		private readonly Dictionary<String, ExternalProcedure>.AlternateLookup<ReadOnlySpan<Char>> _procedureLookup;
		private readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> _variableLookup;

		private ExternalSymbols(Dictionary<String, ExternalProcedure> procedures, HashSet<String> variables)
		{
			this._procedures = procedures;
			this._variables = variables;
			this._procedureLookup = procedures.GetAlternateLookup<ReadOnlySpan<Char>>();
			this._variableLookup = variables.GetAlternateLookup<ReadOnlySpan<Char>>();
		}

		/// <summary>Gets symbols with nothing in them.</summary>
		public static ExternalSymbols Empty { get; } = new ExternalSymbols(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

		/// <summary>Gets the names of the procedures.</summary>
		public IEnumerable<String> ProcedureNames => this._procedures.Keys;

		/// <summary>Gets the names of the variables.</summary>
		public IEnumerable<String> VariableNames => this._variables;

		/// <summary>Gets the number of procedures.</summary>
		public Int32 ProcedureCount => this._procedures.Count;

		/// <summary>Gets the number of variables.</summary>
		public Int32 VariableCount => this._variables.Count;

		/// <summary>Merges the tables of several files.</summary>
		/// <param name="tables">The tables of the included files.</param>
		/// <returns>The symbols.</returns>
		public static ExternalSymbols Create(IEnumerable<ScriptSymbolTable> tables)
		{
			ArgumentNullException.ThrowIfNull(tables);

			Dictionary<String, ExternalProcedure> procedures = new(StringComparer.OrdinalIgnoreCase);
			HashSet<String> variables = new(StringComparer.OrdinalIgnoreCase);
			foreach (ScriptSymbolTable table in tables)
			{
				foreach (ScriptProcedureSymbol symbol in table.Procedures)
				{
					if (procedures.TryGetValue(symbol.Name, out ExternalProcedure? known))
					{
						procedures[symbol.Name] = known with
						{
							MinArguments = Math.Min(known.MinArguments, symbol.MinArguments),
							MaxArguments = Math.Max(known.MaxArguments, symbol.MaxArguments),
							HasBody = known.HasBody || symbol.HasBody,
						};
					}
					else
					{
						procedures[symbol.Name] = new ExternalProcedure(symbol.Name, symbol.MinArguments, symbol.MaxArguments, symbol.HasBody);
					}
				}

				foreach (String variable in table.Variables)
				{
					variables.Add(variable);
				}
			}

			return new ExternalSymbols(procedures, variables);
		}

		/// <summary>Finds a procedure.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <param name="procedure">Receives the procedure.</param>
		/// <returns><see langword="true"/> when a header declares or defines it.</returns>
		public Boolean TryGetProcedure(ReadOnlySpan<Char> name, out ExternalProcedure? procedure)
		{
			return this._procedureLookup.TryGetValue(name, out procedure);
		}

		/// <summary>Determines whether a header declares a variable.</summary>
		/// <param name="name">The name; the case is ignored.</param>
		/// <returns><see langword="true"/> when it does.</returns>
		public Boolean ContainsVariable(ReadOnlySpan<Char> name)
		{
			return this._variableLookup.Contains(name);
		}
	}
}