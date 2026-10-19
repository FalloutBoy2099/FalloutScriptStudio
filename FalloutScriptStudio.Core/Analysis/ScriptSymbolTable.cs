namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>A procedure declared or defined at the top level of a file.</summary>
	/// <param name="Name">The name as written.</param>
	/// <param name="MinArguments">The fewest arguments a call needs (the arguments without a default value).</param>
	/// <param name="MaxArguments">The most arguments a call takes.</param>
	/// <param name="HasBody">Whether the file defines the procedure (a forward declaration or an <c>import</c> has no body).</param>
	public sealed record ScriptProcedureSymbol(String Name, Int32 MinArguments, Int32 MaxArguments, Boolean HasBody);

	/// <summary>
	/// What a script or header declares at its top level and what it includes: the facts another script that includes it needs.
	/// </summary>
	public sealed class ScriptSymbolTable
	{
		/// <summary>Initializes a new instance of the <see cref="ScriptSymbolTable"/> class.</summary>
		/// <param name="procedures">The procedures declared or defined.</param>
		/// <param name="variables">The names of the variables declared at the top level.</param>
		/// <param name="includes">The paths of the <c>#include</c> directives, as written.</param>
		/// <param name="isOpaque">Whether the file uses macros that may declare more names than are listed.</param>
		public ScriptSymbolTable(IReadOnlyList<ScriptProcedureSymbol> procedures, IReadOnlyList<String> variables, IReadOnlyList<String> includes, Boolean isOpaque)
		{
			ArgumentNullException.ThrowIfNull(procedures);
			ArgumentNullException.ThrowIfNull(variables);
			ArgumentNullException.ThrowIfNull(includes);
			this.Procedures = procedures;
			this.Variables = variables;
			this.Includes = includes;
			this.IsOpaque = isOpaque;
		}

		/// <summary>Gets a table with nothing in it.</summary>
		public static ScriptSymbolTable Empty { get; } = new ScriptSymbolTable(Array.Empty<ScriptProcedureSymbol>(), Array.Empty<String>(), Array.Empty<String>(), false);

		/// <summary>Gets the procedures declared or defined at the top level (also those inside a conditional block that may not be compiled).</summary>
		public IReadOnlyList<ScriptProcedureSymbol> Procedures { get; }

		/// <summary>Gets the names of the variables declared at the top level (<c>variable</c>, <c>import variable</c>, <c>export variable</c>).</summary>
		public IReadOnlyList<String> Variables { get; }

		/// <summary>Gets the paths of the <c>#include</c> directives, as written.</summary>
		public IReadOnlyList<String> Includes { get; }

		/// <summary>Gets a value indicating whether the file uses macros that may declare names the table does not list; names cannot be told to be missing then.</summary>
		public Boolean IsOpaque { get; }

		/// <summary>Reads the symbols of a text.</summary>
		/// <param name="text">The text of a script or header.</param>
		/// <returns>The table.</returns>
		public static ScriptSymbolTable Extract(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return Extract(SslTokenizer.Tokenize(text));
		}

		/// <summary>Reads the symbols of tokens.</summary>
		/// <param name="tokens">The tokens of a script or header.</param>
		/// <returns>The table.</returns>
		public static ScriptSymbolTable Extract(SslTokenStream tokens)
		{
			ArgumentNullException.ThrowIfNull(tokens);
			ScriptAnalyzer analyzer = new ScriptAnalyzer(tokens, ScriptEnvironment.Empty, new ScriptDiagnosticOptions(), default);
			return analyzer.ExtractSymbols();
		}
	}
}