namespace FalloutScriptStudio.Core.Analysis
{
	/// <summary>What a declared variable is.</summary>
	public enum VariableDeclarationKind
	{
		/// <summary>An argument in the parameter list of a procedure.</summary>
		Parameter = 0,

		/// <summary>A variable declared inside the body of a procedure.</summary>
		Local = 1,

		/// <summary>A script level variable.</summary>
		Global = 2,

		/// <summary>A script level variable imported from another script (<c>import variable</c>).</summary>
		Import = 3,

		/// <summary>A script level variable exported to other scripts (<c>export variable</c>).</summary>
		Export = 4,
	}
}
