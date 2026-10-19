namespace FalloutScriptStudio.Documents
{
	/// <summary>What a navigation across the files of the project looks for.</summary>
	public enum ProjectNavigationKind
	{
		/// <summary>The place where a procedure has its body, a macro its <c>#define</c> or a variable its declaration.</summary>
		Definition = 0,

		/// <summary>The place where a procedure, a macro or a variable is declared (the forward declaration of a procedure).</summary>
		Declaration = 1,

		/// <summary>The places where the symbol is used.</summary>
		References = 2,
	}
}