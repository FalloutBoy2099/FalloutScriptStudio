namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Remembers which procedures of which script the user collapsed (legacy <c>Settings.SetScriptProcedureFold</c>
	/// and friends). The application provides the persistence.
	/// </summary>
	public interface ICollapsedProcedureStore
	{
		/// <summary>Determines whether a procedure was left collapsed.</summary>
		/// <param name="scriptName">Name of the script (case-insensitive).</param>
		/// <param name="procedureName">Name of the procedure (case-insensitive).</param>
		/// <returns><c>true</c> when the procedure is remembered as collapsed.</returns>
		Boolean IsCollapsed(String scriptName, String procedureName);

		/// <summary>Records whether a procedure is collapsed.</summary>
		/// <param name="scriptName">Name of the script (case-insensitive).</param>
		/// <param name="procedureName">Name of the procedure (case-insensitive).</param>
		/// <param name="collapsed"><c>true</c> to remember the procedure as collapsed, <c>false</c> to forget it.</param>
		void SetCollapsed(String scriptName, String procedureName, Boolean collapsed);
	}
}