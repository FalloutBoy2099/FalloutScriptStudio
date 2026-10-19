namespace FalloutScriptStudio.Core.Functions
{
	using System.Collections.Generic;

	/// <summary>
	/// The content of one functions ini file.
	/// </summary>
	/// <param name="Roots">The top level nodes.</param>
	/// <param name="Issues">The lines that could not be understood.</param>
	public sealed record ParsedFunctionFile(IReadOnlyList<FunctionNode> Roots, IReadOnlyList<FunctionCatalogIssue> Issues);
}