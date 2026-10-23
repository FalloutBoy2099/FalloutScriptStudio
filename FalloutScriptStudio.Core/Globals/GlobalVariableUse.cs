namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.IO;

	/// <summary>One place of the project where a global variable is read or written.</summary>
	public sealed class GlobalVariableUse
	{
		/// <summary>Initializes a new instance of the <see cref="GlobalVariableUse"/> class.</summary>
		/// <param name="function">The function that was called, in lower case (<c>set_global_var</c>).</param>
		/// <param name="kind">The family of variables.</param>
		/// <param name="written">The variable as it is written in the script: the text of the string, the number or the macro name.</param>
		/// <param name="access">What the use does with the variable.</param>
		/// <param name="file">The full path of the file.</param>
		/// <param name="line">The 1-based line.</param>
		/// <param name="procedure">The procedure the use is in, <c>#define NAME</c> for the body of a macro, or <see langword="null"/>.</param>
		/// <param name="valueType">The type of the value.</param>
		/// <param name="literal">The literal value that is written, or <see langword="null"/>.</param>
		public GlobalVariableUse(String function, GlobalVariableKind kind, String written, GlobalAccess access, String file, Int32 line, String? procedure, GlobalValueType valueType, String? literal)
		{
			ArgumentNullException.ThrowIfNull(function);
			ArgumentNullException.ThrowIfNull(written);
			ArgumentNullException.ThrowIfNull(file);
			this.Function = function;
			this.Kind = kind;
			this.Written = written;
			this.Access = access;
			this.File = file;
			this.Line = line;
			this.Procedure = procedure;
			this.ValueType = valueType;
			this.Literal = literal;
		}

		/// <summary>Gets the function that was called, in lower case.</summary>
		public String Function { get; }

		/// <summary>Gets the family of variables.</summary>
		public GlobalVariableKind Kind { get; }

		/// <summary>Gets the variable as it is written in the script (the name, the number or the macro).</summary>
		public String Written { get; }

		/// <summary>Gets what the use does with the variable.</summary>
		public GlobalAccess Access { get; }

		/// <summary>Gets the full path of the file.</summary>
		public String File { get; }

		/// <summary>Gets the 1-based line.</summary>
		public Int32 Line { get; }

		/// <summary>Gets the procedure the use is in, <c>#define NAME</c> for the body of a macro, or <see langword="null"/>.</summary>
		public String? Procedure { get; }

		/// <summary>Gets the type of the value: the function states it (<c>get_sfall_global_float</c>) or the written literal does.</summary>
		public GlobalValueType ValueType { get; }

		/// <summary>Gets the literal value that is written (a number, with a macro replaced by its value), or <see langword="null"/> when the value is an expression or the use does not write a stated value.</summary>
		public String? Literal { get; }

		/// <summary>Gets the name of the file without its folder.</summary>
		public String FileName => Path.GetFileName(this.File);

		/// <summary>Gets a value indicating whether the use reads the value.</summary>
		public Boolean Reads => (this.Access & GlobalAccess.Read) != 0;

		/// <summary>Gets a value indicating whether the use writes the value.</summary>
		public Boolean Writes => (this.Access & GlobalAccess.Write) != 0;

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Written + " (" + this.Access + ", " + this.FileName + ":" + this.Line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
		}
	}
}