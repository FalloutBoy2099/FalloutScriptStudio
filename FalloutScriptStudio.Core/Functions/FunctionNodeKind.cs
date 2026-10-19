namespace FalloutScriptStudio.Core.Functions
{
	/// <summary>
	/// Kind of a node of the functions tree.
	/// </summary>
	public enum FunctionNodeKind
	{
		/// <summary>A menu node that holds functions and other nodes (<c>&lt;m&gt;</c> / <c>&lt;m+&gt;</c>).</summary>
		Category,

		/// <summary>A function with a description and the code inserted into the script.</summary>
		Function,

		/// <summary>A separator line between top level categories (<c>&lt;-&gt;</c>).</summary>
		Separator,
	}
}