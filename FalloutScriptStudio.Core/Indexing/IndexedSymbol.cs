namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A procedure, macro or script level variable found in a file of the project.</summary>
	public sealed class IndexedSymbol
	{
		/// <summary>Initializes a new instance of the <see cref="IndexedSymbol"/> class.</summary>
		/// <param name="name">The name as written.</param>
		/// <param name="kind">The kind (a single value).</param>
		/// <param name="file">The full path of the file.</param>
		/// <param name="line">The 1-based line where the symbol is defined (the body of a procedure, the <c>#define</c>, the declaration of a variable).</param>
		public IndexedSymbol(String name, IndexedSymbolKind kind, String file, Int32 line)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(file);
			this.Name = name;
			this.Kind = kind;
			this.File = file;
			this.Line = line;
			this.DeclarationLine = line;
		}

		/// <summary>Gets the name as written.</summary>
		public String Name { get; }

		/// <summary>Gets the kind.</summary>
		public IndexedSymbolKind Kind { get; }

		/// <summary>Gets the full path of the file.</summary>
		public String File { get; }

		/// <summary>Gets the 1-based line where the symbol is defined; for a procedure without a body in this file it is the line of its declaration.</summary>
		public Int32 Line { get; }

		/// <summary>Gets the 1-based line of the declaration: the forward declaration of a procedure when the file has one, otherwise <see cref="Line"/>.</summary>
		public Int32 DeclarationLine { get; init; }

		/// <summary>Gets a value indicating whether the file holds the body of the procedure; always <see langword="true"/> for the other kinds.</summary>
		public Boolean HasBody { get; init; } = true;

		/// <summary>Gets the modifiers of a procedure.</summary>
		public ProcedureFlags Flags { get; init; }

		/// <summary>Gets the scope of a variable.</summary>
		public VariableScope Scope { get; init; } = VariableScope.Global;

		/// <summary>Gets the macro name with its parameters (<c>MAX(a, b)</c>), or <see langword="null"/> when it is the same as the name or the symbol is not a macro.</summary>
		public String? Detail { get; init; }

		/// <summary>Gets the name of the file without its folder.</summary>
		public String FileName => Path.GetFileName(this.File);

		/// <summary>Gets the text to show for the symbol: the name, or the macro with its parameters.</summary>
		public String DisplayName => this.Detail ?? this.Name;

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Name + " (" + this.Kind + ", " + this.FileName + ":" + this.Line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
		}
	}
}
