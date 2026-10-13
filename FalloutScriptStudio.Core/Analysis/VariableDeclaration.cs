namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>One variable declared in a script: a parameter, a local variable or a script level variable.</summary>
	public sealed class VariableDeclaration
	{
		/// <summary>Initializes a new instance of the <see cref="VariableDeclaration"/> class.</summary>
		/// <param name="name">The name as written.</param>
		/// <param name="kind">What the variable is.</param>
		/// <param name="procedureName">The name of the procedure that declares it, or <see langword="null"/> for a script level variable.</param>
		/// <param name="tokenIndex">The index of the name token.</param>
		/// <param name="offset">The offset of the name.</param>
		/// <param name="line">The 1-based line of the name.</param>
		/// <param name="column">The 1-based column of the name.</param>
		public VariableDeclaration(String name, VariableDeclarationKind kind, String? procedureName, Int32 tokenIndex, Int32 offset, Int32 line, Int32 column)
		{
			ArgumentNullException.ThrowIfNull(name);
			this.Name = name;
			this.Kind = kind;
			this.ProcedureName = procedureName;
			this.TokenIndex = tokenIndex;
			this.Offset = offset;
			this.Line = line;
			this.Column = column;
		}

		/// <summary>Gets the name as written.</summary>
		public String Name { get; }

		/// <summary>Gets what the variable is.</summary>
		public VariableDeclarationKind Kind { get; }

		/// <summary>Gets the name of the procedure that declares the variable, or <see langword="null"/> for a script level variable.</summary>
		public String? ProcedureName { get; }

		/// <summary>Gets the index of the name token.</summary>
		public Int32 TokenIndex { get; }

		/// <summary>Gets the offset of the name.</summary>
		public Int32 Offset { get; }

		/// <summary>Gets the 1-based line of the name.</summary>
		public Int32 Line { get; }

		/// <summary>Gets the 1-based column of the name.</summary>
		public Int32 Column { get; }

		/// <summary>Gets the offset where the declaration of this variable starts (its name).</summary>
		public Int32 ItemStart => this.Offset;

		/// <summary>Gets the offset just after the declaration of this variable (its name, array length and initial value).</summary>
		public Int32 ItemEnd { get; internal set; }

		/// <summary>Gets a value indicating whether the declaration has an initial value (<c>:=</c>).</summary>
		public Boolean HasInitializer { get; internal set; }

		/// <summary>Gets a value indicating whether the initial value calls something or changes a variable, so that removing it changes what the script does.</summary>
		public Boolean InitializerHasSideEffects { get; internal set; }

		/// <summary>Gets the statement this variable is declared in.</summary>
		public DeclarationStatement Statement { get; internal set; } = null!;

		/// <summary>Gets the position of the variable in its statement.</summary>
		public Int32 ItemIndex { get; internal set; }

		/// <summary>Gets a value indicating whether the declaration sits inside parentheses (a parameter list or the header of a loop).</summary>
		public Boolean IsInParentheses => this.Statement.IsInParentheses;

		/// <summary>Gets a value indicating whether the name is one of the names a script exchanges with another one (<c>import</c> / <c>export</c>).</summary>
		public Boolean IsShared => this.Kind is VariableDeclarationKind.Import or VariableDeclarationKind.Export;
	}
}
