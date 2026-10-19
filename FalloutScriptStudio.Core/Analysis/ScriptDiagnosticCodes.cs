namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The rules of <see cref="ScriptDiagnostics"/>. <c>SSL1xxx</c> are rules of the language itself that sslc enforces (an error here means
	/// the compiler fails; each one is checked against the real compiler by the tests), <c>SSL2xxx</c> compare the calls with the
	/// descriptions of the engine and sfall functions (<c>opcodes.txt</c>, <c>functions.ini</c>) and are never more than warnings.
	/// </summary>
	public static class ScriptDiagnosticCodes
	{
		/// <summary>A procedure is called with fewer or more arguments than it takes.</summary>
		public static readonly ScriptDiagnosticRule WrongArgumentCount = new ScriptDiagnosticRule("SSL1001", nameof(WrongArgumentCount), ScriptDiagnosticSeverity.Error);

		/// <summary>A call of a name that is no procedure, no macro and no known function.</summary>
		public static readonly ScriptDiagnosticRule UndefinedProcedure = new ScriptDiagnosticRule("SSL1002", nameof(UndefinedProcedure), ScriptDiagnosticSeverity.Error);

		/// <summary>A name that is no variable, no procedure, no macro and no known function.</summary>
		public static readonly ScriptDiagnosticRule UndefinedVariable = new ScriptDiagnosticRule("SSL1003", nameof(UndefinedVariable), ScriptDiagnosticSeverity.Error);

		/// <summary>A procedure that is defined further down and has no forward declaration before the use.</summary>
		public static readonly ScriptDiagnosticRule ProcedureUsedBeforeDefinition = new ScriptDiagnosticRule("SSL1004", nameof(ProcedureUsedBeforeDefinition), ScriptDiagnosticSeverity.Error);

		/// <summary>A variable that is declared further down.</summary>
		public static readonly ScriptDiagnosticRule VariableUsedBeforeDeclaration = new ScriptDiagnosticRule("SSL1005", nameof(VariableUsedBeforeDeclaration), ScriptDiagnosticSeverity.Error);

		/// <summary>A local variable of another procedure.</summary>
		public static readonly ScriptDiagnosticRule VariableOutOfScope = new ScriptDiagnosticRule("SSL1006", nameof(VariableOutOfScope), ScriptDiagnosticSeverity.Error);

		/// <summary>An assignment to a constant, a literal or a function of the engine.</summary>
		public static readonly ScriptDiagnosticRule AssignToConstant = new ScriptDiagnosticRule("SSL1007", nameof(AssignToConstant), ScriptDiagnosticSeverity.Error);

		/// <summary>A procedure that is defined twice (or declared after its definition).</summary>
		public static readonly ScriptDiagnosticRule DuplicateProcedure = new ScriptDiagnosticRule("SSL1008", nameof(DuplicateProcedure), ScriptDiagnosticSeverity.Error);

		/// <summary>A variable or argument that is declared twice in the same scope.</summary>
		public static readonly ScriptDiagnosticRule DuplicateVariable = new ScriptDiagnosticRule("SSL1009", nameof(DuplicateVariable), ScriptDiagnosticSeverity.Error);

		/// <summary>A <c>begin</c> that is never closed.</summary>
		public static readonly ScriptDiagnosticRule MissingEnd = new ScriptDiagnosticRule("SSL1010", nameof(MissingEnd), ScriptDiagnosticSeverity.Error);

		/// <summary>An <c>end</c> that closes nothing.</summary>
		public static readonly ScriptDiagnosticRule UnexpectedEnd = new ScriptDiagnosticRule("SSL1011", nameof(UnexpectedEnd), ScriptDiagnosticSeverity.Error);

		/// <summary>An <c>if</c> without <c>then</c>.</summary>
		public static readonly ScriptDiagnosticRule MissingThen = new ScriptDiagnosticRule("SSL1012", nameof(MissingThen), ScriptDiagnosticSeverity.Error);

		/// <summary>A <c>while</c> without <c>do</c>.</summary>
		public static readonly ScriptDiagnosticRule MissingDo = new ScriptDiagnosticRule("SSL1013", nameof(MissingDo), ScriptDiagnosticSeverity.Error);

		/// <summary>A statement that starts with the name of a procedure instead of <c>call</c>.</summary>
		public static readonly ScriptDiagnosticRule MissingCall = new ScriptDiagnosticRule("SSL1014", nameof(MissingCall), ScriptDiagnosticSeverity.Error);

		/// <summary>A procedure that is declared and used but has no body.</summary>
		public static readonly ScriptDiagnosticRule ProcedureNeverDefined = new ScriptDiagnosticRule("SSL1015", nameof(ProcedureNeverDefined), ScriptDiagnosticSeverity.Error);

		/// <summary>A <c>call name(arguments) in delay</c>: a delayed call cannot pass arguments.</summary>
		public static readonly ScriptDiagnosticRule CallInWithArguments = new ScriptDiagnosticRule("SSL1016", nameof(CallInWithArguments), ScriptDiagnosticSeverity.Error);

		/// <summary>An argument with a default value in front of an argument without one.</summary>
		public static readonly ScriptDiagnosticRule DefaultBeforeRequired = new ScriptDiagnosticRule("SSL1017", nameof(DefaultBeforeRequired), ScriptDiagnosticSeverity.Error);

		/// <summary>A procedure with default values that has a forward declaration.</summary>
		public static readonly ScriptDiagnosticRule DefaultOnForwardDeclared = new ScriptDiagnosticRule("SSL1018", nameof(DefaultOnForwardDeclared), ScriptDiagnosticSeverity.Error);

		/// <summary>A definition whose number of arguments differs from its forward declaration.</summary>
		public static readonly ScriptDiagnosticRule DeclarationMismatch = new ScriptDiagnosticRule("SSL1019", nameof(DeclarationMismatch), ScriptDiagnosticSeverity.Error);

		/// <summary>A function of the catalogs called with a number of arguments its description does not allow.</summary>
		public static readonly ScriptDiagnosticRule FunctionArgumentCount = new ScriptDiagnosticRule("SSL2001", nameof(FunctionArgumentCount), ScriptDiagnosticSeverity.Warning);

		/// <summary>A literal whose type is not the type the description of the function states.</summary>
		public static readonly ScriptDiagnosticRule FunctionArgumentType = new ScriptDiagnosticRule("SSL2002", nameof(FunctionArgumentType), ScriptDiagnosticSeverity.Warning);

		/// <summary>A function the catalog marks as deprecated.</summary>
		public static readonly ScriptDiagnosticRule DeprecatedFunction = new ScriptDiagnosticRule("SSL2003", nameof(DeprecatedFunction), ScriptDiagnosticSeverity.Info);

		/// <summary>A function without arguments that is called with parentheses.</summary>
		public static readonly ScriptDiagnosticRule FunctionTakesNoParentheses = new ScriptDiagnosticRule("SSL2004", nameof(FunctionTakesNoParentheses), ScriptDiagnosticSeverity.Warning);

		/// <summary>Gets every rule, in the order of their codes.</summary>
		public static IReadOnlyList<ScriptDiagnosticRule> All { get; } = new[]
		{
			WrongArgumentCount,
			UndefinedProcedure,
			UndefinedVariable,
			ProcedureUsedBeforeDefinition,
			VariableUsedBeforeDeclaration,
			VariableOutOfScope,
			AssignToConstant,
			DuplicateProcedure,
			DuplicateVariable,
			MissingEnd,
			UnexpectedEnd,
			MissingThen,
			MissingDo,
			MissingCall,
			ProcedureNeverDefined,
			CallInWithArguments,
			DefaultBeforeRequired,
			DefaultOnForwardDeclared,
			DeclarationMismatch,
			FunctionArgumentCount,
			FunctionArgumentType,
			DeprecatedFunction,
			FunctionTakesNoParentheses,
		};

		/// <summary>Finds a rule by its code.</summary>
		/// <param name="code">The code, for example <c>SSL1001</c>.</param>
		/// <returns>The rule, or <see langword="null"/> when there is none.</returns>
		public static ScriptDiagnosticRule? Find(String code)
		{
			foreach (ScriptDiagnosticRule rule in All)
			{
				if (String.Equals(rule.Code, code, StringComparison.Ordinal))
				{
					return rule;
				}
			}

			return null;
		}
	}
}