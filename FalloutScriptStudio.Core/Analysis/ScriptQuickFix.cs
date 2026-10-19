namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>
	/// The automatic correction attached to a <see cref="ScriptDiagnostic"/>. Only the kind and what it needs are stored; the text edits are
	/// computed against the current text by <see cref="ScriptQuickFixes"/>, so a fix is never applied to a text it was not made for.
	/// </summary>
	/// <param name="Id">What the fix does, one of the <c>ScriptQuickFix.*Id</c> constants.</param>
	/// <param name="Argument">The text the fix needs: the suggested name for <see cref="RenameId"/>, the forward declaration for <see cref="ForwardDeclareId"/>.</param>
	/// <param name="TargetOffset">The offset where the fix works: the start of the name that is replaced, the start of the declaration that is removed or the place where the declaration is inserted.</param>
	/// <param name="TargetLength">The length of the text that is replaced or removed (0 for an insertion).</param>
	public sealed record ScriptQuickFix(String Id, String Argument, Int32 TargetOffset, Int32 TargetLength)
	{
		/// <summary>Replaces a misspelled name by the nearest known one ("Did you mean ...?").</summary>
		public const String RenameId = "rename";

		/// <summary>Adds the forward declaration of a procedure that is used before its definition.</summary>
		public const String ForwardDeclareId = "forward-declare";

		/// <summary>Removes a declaration that repeats another one.</summary>
		public const String RemoveDeclarationId = "remove-declaration";
	}
}