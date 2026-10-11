namespace FalloutScriptStudio.Core.TextTools
{
	/// <summary>
	/// Where a new procedure is inserted (legacy <c>InsertAt</c>).
	/// </summary>
	public enum InsertPlacement
	{
		/// <summary>At the end of the script.</summary>
		End = 0,

		/// <summary>After a given procedure.</summary>
		After = 1,

		/// <summary>At the caret line; a caret inside a procedure inserts after that procedure instead.</summary>
		Caret = 2,
	}
}
