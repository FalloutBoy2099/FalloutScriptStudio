namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// A variable declared in a script: global, imported/exported, local to a procedure or a procedure argument.
	/// </summary>
	public sealed class ScriptVariable : IScriptSymbol
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptVariable"/> class.
		/// </summary>
		/// <param name="name">Variable name as written in the script.</param>
		/// <param name="scope">Variable scope.</param>
		public ScriptVariable(String name, VariableScope scope)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Scope = scope;
		}

		/// <inheritdoc />
		public String Name { get; }

		/// <summary>Gets the scope of the variable.</summary>
		public VariableScope Scope { get; }

		/// <summary>Gets the full path of the file where the variable is declared, or <c>null</c> when unknown.</summary>
		public String? DeclaredFile { get; init; }

		/// <summary>Gets the lower-cased file name of <see cref="DeclaredFile"/>, or <c>null</c> when unknown.</summary>
		public String? FileName { get; init; }

		/// <summary>Gets the 1-based line of the declaration, or -1 when unknown.</summary>
		public Int32 DeclaredLine { get; init; } = -1;

		/// <summary>
		/// Gets the 1-based line where the variable is declared as an argument in the procedure header,
		/// or -1 when it is not an argument (legacy <c>adeclared</c>).
		/// </summary>
		public Int32 ArgumentDeclaredLine { get; init; } = -1;

		/// <summary>Gets the initial value formatted as source text (strings are quoted), or <c>null</c> when there is none.</summary>
		public String? InitialValue { get; init; }

		/// <summary>Gets the kind of the initial value.</summary>
		public ValueKind InitialValueKind { get; init; }

		/// <summary>Gets the array length, or 0 for a scalar.</summary>
		public Int32 ArrayLength { get; init; }

		/// <inheritdoc />
		public IReadOnlyList<SourceReference> References { get; init; } = Array.Empty<SourceReference>();

		/// <summary>Gets a value indicating whether the variable is a procedure argument.</summary>
		public Boolean IsArgument => this.ArgumentDeclaredLine > 0;

		/// <inheritdoc />
		public SymbolKind Kind => this.Scope == VariableScope.Local ? SymbolKind.LocalVariable : SymbolKind.GlobalVariable;

		/// <inheritdoc />
		public Boolean IsImported => this.Scope == VariableScope.Import;

		/// <inheritdoc />
		public Boolean IsExported => this.Scope == VariableScope.Export;

		/// <inheritdoc />
		public Boolean IsStandard => false;

		/// <inheritdoc />
		public SourceReference? Declaration => new SourceReference(this.DeclaredFile, this.DeclaredLine);

		/// <inheritdoc />
		public String FullDescription
		{
			get
			{
				String prefix = this.Scope switch
				{
					VariableScope.Local => this.IsArgument ? "Argument variable " : "Local variable ",
					VariableScope.Global => "Variable ",
					VariableScope.Import => "Imported variable ",
					VariableScope.Export => "Exported variable ",
					_ => String.Empty,
				};

				return this.InitialValue is null ? prefix + this.Name : prefix + this.Name + " := " + this.InitialValue;
			}
		}

		/// <summary>
		/// Gets the short description. The legacy code returned nothing for variables; the full description is used here
		/// so that callers never have to handle a missing value.
		/// </summary>
		public String ShortDescription => this.FullDescription;

		/// <summary>Returns a copy of this variable declared in another file.</summary>
		/// <param name="declaredFile">The new declaration file.</param>
		/// <param name="references">The new references.</param>
		internal ScriptVariable WithFile(String? declaredFile, IReadOnlyList<SourceReference> references)
		{
			return new ScriptVariable(this.Name, this.Scope)
			{
				DeclaredFile = declaredFile,
				FileName = String.IsNullOrEmpty(declaredFile) ? this.FileName : Path.GetFileName(declaredFile).ToLowerInvariant(),
				DeclaredLine = this.DeclaredLine,
				ArgumentDeclaredLine = this.ArgumentDeclaredLine,
				InitialValue = this.InitialValue,
				InitialValueKind = this.InitialValueKind,
				ArrayLength = this.ArrayLength,
				References = references,
			};
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this.FullDescription;
		}
	}
}
