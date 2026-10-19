namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Kind of a <see cref="FoldingRegion"/>.
	/// </summary>
	public enum FoldingKind
	{
		/// <summary>The body of a procedure.</summary>
		Procedure,

		/// <summary>The block of procedure declarations at the top of a script.</summary>
		Region,

		/// <summary>A <c>variable begin ... end</c> block.</summary>
		Variables,

		/// <summary>An <c>#if ... #endif</c> block.</summary>
		PreprocessorIf,
	}
}