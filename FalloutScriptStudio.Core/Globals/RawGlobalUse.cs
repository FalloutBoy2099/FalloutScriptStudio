namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>
	/// One use of a global variable as the scanner of a file found it, before the macros of the project are resolved. It is kept per file
	/// by the project index, so it stays small: strings are shared through a pool and the procedure is an index into the file's table.
	/// </summary>
	internal readonly struct RawGlobalUse
	{
		internal RawGlobalUse(GlobalFunction function, GlobalKeyKind keyKind, String key, Int32 line, Int32 procedure, String? value, Boolean valueIsSymbol)
		{
			this.Function = function;
			this.KeyKind = keyKind;
			this.Key = key;
			this.Line = line;
			this.Procedure = procedure;
			this.Value = value;
			this.ValueIsSymbol = valueIsSymbol;
		}

		/// <summary>Gets the function that was called.</summary>
		internal GlobalFunction Function { get; }

		/// <summary>Gets how the variable is named: <see cref="GlobalKeyKind.Name"/> (the text of the string), <see cref="GlobalKeyKind.Id"/> (a number) or <see cref="GlobalKeyKind.Symbol"/> (a macro name).</summary>
		internal GlobalKeyKind KeyKind { get; }

		/// <summary>Gets the name, the decimal number or the macro name.</summary>
		internal String Key { get; }

		/// <summary>Gets the 1-based line.</summary>
		internal Int32 Line { get; }

		/// <summary>Gets the index into <see cref="FileGlobalScan.Procedures"/>, or -1 outside a procedure.</summary>
		internal Int32 Procedure { get; }

		/// <summary>Gets the literal value written (a number as written) or the macro name when <see cref="ValueIsSymbol"/> is set; <see langword="null"/> when the value is an expression.</summary>
		internal String? Value { get; }

		/// <summary>Gets a value indicating whether <see cref="Value"/> is the name of a macro.</summary>
		internal Boolean ValueIsSymbol { get; }
	}
}