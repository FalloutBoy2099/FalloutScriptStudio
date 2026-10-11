namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Describes a procedure to insert into a script (legacy <c>PrepareInsertProcedure</c> parameters).
	/// </summary>
	/// <param name="Declaration">
	/// The procedure name, optionally with arguments exactly as they appear after the <c>procedure</c> keyword,
	/// for example <c>Node001</c> or <c>Helper(variable a, variable b)</c> (see <see cref="ProcedureNameParser"/>).
	/// </param>
	public sealed record ProcedureInsertRequest(String Declaration)
	{
		/// <summary>Gets where to insert the procedure.</summary>
		public InsertPlacement Placement { get; init; } = InsertPlacement.End;

		/// <summary>Gets the name of the procedure to insert after (for <see cref="InsertPlacement.After"/>) and to copy the body from.</summary>
		public String? AnchorProcedure { get; init; }

		/// <summary>Gets the 1-based line of the caret (for <see cref="InsertPlacement.Caret"/>).</summary>
		public Int32 CaretLine { get; init; } = 1;

		/// <summary>Gets a value indicating whether the body of <see cref="AnchorProcedure"/> is copied into the new procedure.</summary>
		public Boolean CopyAnchorBody { get; init; }

		/// <summary>
		/// Gets a value indicating whether the body starts with <c>script_overrides;</c> (needed by handlers such as
		/// <c>look_at_p_proc</c>, see <see cref="ProcedureHandlerCatalog.RequiresScriptOverrides"/>).
		/// </summary>
		public Boolean UseScriptOverrides { get; init; }

		/// <summary>Gets the complete body (between <c>begin</c> and <c>end</c>) to use instead of an empty one; line breaks are converted to the script's.</summary>
		public String? Body { get; init; }

		/// <summary>Gets the indentation used for generated body lines.</summary>
		public String Indentation { get; init; } = "    ";

		/// <summary>Gets the line terminator for new text; the terminator of the script is used when <c>null</c>.</summary>
		public String? NewLine { get; init; }

		/// <summary>Gets a value indicating whether a forward declaration (<c>procedure Name;</c>) is added to the declaration section.</summary>
		public Boolean AddDeclaration { get; init; } = true;
	}
}
