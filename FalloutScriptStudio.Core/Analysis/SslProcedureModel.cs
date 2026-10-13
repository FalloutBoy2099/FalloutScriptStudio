namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>A procedure with a body, located in the tokens of a script.</summary>
	public sealed class SslProcedureModel
	{
		private readonly List<VariableDeclaration> _declarations = new List<VariableDeclaration>();

		/// <summary>Initializes a new instance of the <see cref="SslProcedureModel"/> class.</summary>
		/// <param name="name">The name.</param>
		/// <param name="firstToken">The index of the first token of the declaration (a modifier or the <c>procedure</c> keyword).</param>
		/// <param name="procedureToken">The index of the <c>procedure</c> keyword.</param>
		/// <param name="nameToken">The index of the name.</param>
		public SslProcedureModel(String name, Int32 firstToken, Int32 procedureToken, Int32 nameToken)
		{
			this.Name = name;
			this.FirstToken = firstToken;
			this.ProcedureToken = procedureToken;
			this.NameToken = nameToken;
		}

		/// <summary>Gets the name as written.</summary>
		public String Name { get; }

		/// <summary>Gets the index of the first token of the declaration.</summary>
		public Int32 FirstToken { get; }

		/// <summary>Gets the index of the <c>procedure</c> keyword.</summary>
		public Int32 ProcedureToken { get; }

		/// <summary>Gets the index of the name.</summary>
		public Int32 NameToken { get; }

		/// <summary>Gets the index of the <c>begin</c> that opens the body.</summary>
		public Int32 BeginToken { get; internal set; }

		/// <summary>Gets the index of the <c>end</c> that closes the body (the last token of the procedure when the body is not closed).</summary>
		public Int32 EndToken { get; internal set; }

		/// <summary>Gets a value indicating whether the <c>end</c> of the body was found.</summary>
		public Boolean IsClosed { get; internal set; }

		/// <summary>Gets the parameters and the local variables, in text order.</summary>
		public IReadOnlyList<VariableDeclaration> Declarations => this._declarations;

		internal void Add(VariableDeclaration declaration)
		{
			this._declarations.Add(declaration);
		}
	}
}
