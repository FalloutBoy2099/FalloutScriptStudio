namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A finding of <see cref="ScriptDiagnostics"/>: a place of the script together with what is wrong. The message is a localization key
	/// with arguments, so the application can show it in the language of the user and Core stays free of texts.
	/// </summary>
	/// <param name="Code">The stable code of the rule, see <see cref="ScriptDiagnosticCodes"/> (for example <c>SSL1001</c>).</param>
	/// <param name="Severity">How serious it is.</param>
	/// <param name="MessageKey">The localization key of the message.</param>
	/// <param name="MessageArguments">The arguments of the message, already formatted as text.</param>
	/// <param name="Offset">The zero-based offset of the first character.</param>
	/// <param name="Length">The number of characters.</param>
	/// <param name="Line">The 1-based line of the first character.</param>
	/// <param name="Column">The 1-based column of the first character.</param>
	public sealed record ScriptDiagnostic(
		String Code,
		ScriptDiagnosticSeverity Severity,
		String MessageKey,
		IReadOnlyList<String> MessageArguments,
		Int32 Offset,
		Int32 Length,
		Int32 Line,
		Int32 Column)
	{
		/// <summary>Gets the automatic correction, or <see langword="null"/> when there is none.</summary>
		public ScriptQuickFix? Fix { get; init; }

		/// <summary>Gets the localization key of the hint ("what to do"): the message key followed by <c>Hint</c>.</summary>
		public String HintKey => this.MessageKey + "Hint";

		/// <summary>Gets the offset just after the last character.</summary>
		public Int32 End => this.Offset + this.Length;

		/// <summary>Determines whether the range of the diagnostic contains an offset (the end counts: a caret right after the name is on it).</summary>
		/// <param name="offset">The offset, for example of the caret.</param>
		/// <returns><see langword="true"/> when the offset is inside the range or right after it.</returns>
		public Boolean Contains(Int32 offset)
		{
			return offset >= this.Offset && offset <= this.End;
		}
	}
}