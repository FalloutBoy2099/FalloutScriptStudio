namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>
	/// Everything a rename needs, supplied explicitly by the caller.
	/// </summary>
	/// <param name="Info">The parse result of the current script.</param>
	/// <param name="Symbol">The procedure, variable or macro to rename.</param>
	/// <param name="NewName">The new name.</param>
	/// <param name="CurrentFile">Full path of the script that is being edited.</param>
	/// <param name="Documents">The texts of the current script and of the other files that may contain the symbol (open tabs, include files, project files).</param>
	public sealed record RenameRequest(ProgramInfo Info, IScriptSymbol Symbol, String NewName, String CurrentFile, IReadOnlyList<RefactorDocument> Documents)
	{
		/// <summary>
		/// Gets a value indicating whether a procedure defined in another file is renamed there too, replacing every occurrence of
		/// its name in that file (the legacy "also rename procedures in that file" answer).
		/// </summary>
		public Boolean RenameInDefiningFile { get; init; }
	}
}