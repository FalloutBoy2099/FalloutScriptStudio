namespace FalloutScriptStudio.Services.ScriptTools.Procedures
{
	/// <summary>Where a newly created procedure is inserted (legacy <c>InsertAt</c>).</summary>
	public enum ProcedurePlacement
	{
		/// <summary>At the end of the script.</summary>
		AtEnd = 0,

		/// <summary>After the current procedure.</summary>
		AfterCurrent = 1,

		/// <summary>At the caret position.</summary>
		AtCaret = 2,
	}
}
