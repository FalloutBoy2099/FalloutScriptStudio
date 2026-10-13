namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;

	/// <summary>The outcome of examining a selection for "Extract Procedure": either why it cannot be extracted, or what the new procedure needs.</summary>
	public sealed class ExtractAnalysis
	{
		private readonly List<String> _parameters = new List<String>();
		private readonly List<String> _movedLocals = new List<String>();

		internal ExtractAnalysis(ExtractFailure failure, String? detail)
		{
			this.Failure = failure;
			this.Detail = detail;
		}

		/// <summary>Gets why the selection cannot be extracted, or <see cref="ExtractFailure.None"/>.</summary>
		public ExtractFailure Failure { get; }

		/// <summary>Gets the name that explains the failure (the variable for <see cref="ExtractFailure.DeclaredVariableUsedAfter"/>), or <see langword="null"/>.</summary>
		public String? Detail { get; }

		/// <summary>Gets a value indicating whether the selection can be extracted.</summary>
		public Boolean IsValid => this.Failure == ExtractFailure.None;

		/// <summary>Gets the name of the procedure that holds the selection.</summary>
		public String ProcedureName { get; internal set; } = String.Empty;

		/// <summary>Gets the variables of the enclosing procedure that become the parameters of the new one, in order of first use in the selection.</summary>
		public IReadOnlyList<String> Parameters => this._parameters;

		/// <summary>Gets the variable the new procedure returns (the call assigns it), or <see langword="null"/>.</summary>
		public String? ResultVariable { get; internal set; }

		/// <summary>Gets the local variables of the enclosing procedure that only the selection uses: their declarations move into the new procedure.</summary>
		public IReadOnlyList<String> MovedLocals => this._movedLocals;

		/// <summary>Gets a value indicating whether the result variable is declared in the new procedure (the selection assigns it before reading it).</summary>
		public Boolean ResultIsLocal { get; internal set; }

		internal SslScriptModel? Model { get; set; }

		internal SslProcedureModel? Procedure { get; set; }

		internal Int32 FirstToken { get; set; }

		internal Int32 LastToken { get; set; }

		internal List<VariableDeclaration> MovedDeclarations { get; } = new List<VariableDeclaration>();

		/// <summary>Determines whether a list of parameter names (typed by the user) is the list the new procedure needs, in any order.</summary>
		/// <param name="names">The parameter names.</param>
		/// <returns><see langword="true"/> when both lists hold the same names, ignoring the case.</returns>
		public Boolean MatchesParameters(IEnumerable<String> names)
		{
			ArgumentNullException.ThrowIfNull(names);

			List<String> given = names.ToList();
			return given.Count == this._parameters.Count
				&& given.Distinct(StringComparer.OrdinalIgnoreCase).Count() == given.Count
				&& this._parameters.All(parameter => given.Contains(parameter, StringComparer.OrdinalIgnoreCase));
		}

		internal void AddParameter(String name)
		{
			this._parameters.Add(name);
		}

		internal void AddMovedLocal(String name)
		{
			this._movedLocals.Add(name);
		}
	}
}
