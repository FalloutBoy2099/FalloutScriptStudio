namespace FalloutScriptStudio.Services.ScriptTools.Procedures
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Everything the procedure name dialog needs to know.
	/// </summary>
	public sealed record ProcedureNameRequest
	{
		/// <summary>Gets the suggested name; empty when there is none.</summary>
		public String InitialName { get; init; } = String.Empty;

		/// <summary>Gets how the dialog is used.</summary>
		public ProcedureNameDialogMode Mode { get; init; } = ProcedureNameDialogMode.Create;

		/// <summary>Gets the names that are already taken (compared case-insensitively).</summary>
		public IReadOnlyCollection<String> ExistingNames { get; init; } = Array.Empty<String>();

		/// <summary>Gets what is being renamed ("Procedure", "Local Variable"...); used in the title of a rename dialog.</summary>
		public String Subject { get; init; } = String.Empty;

		/// <summary>Gets a value indicating whether the name is shown but cannot be edited (handler procedures such as <c>look_at_p_proc</c>).</summary>
		public Boolean NameReadOnly { get; init; }

		/// <summary>Gets a value indicating whether the user may choose where the procedure is inserted.</summary>
		public Boolean CanChoosePlacement { get; init; }

		/// <summary>Gets a value indicating whether the user may copy the body of the current procedure into the new one.</summary>
		public Boolean CanCopyBody { get; init; }

		/// <summary>Gets a value indicating whether "after the current procedure" is preselected instead of "at the end of the script".</summary>
		public Boolean PreferInsertAfter { get; init; }
	}
}