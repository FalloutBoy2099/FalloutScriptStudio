namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// The code generated for a keyword typed in the editor.
	/// </summary>
	/// <param name="Edit">The replacement to apply (it may also rewrite the end of the keyword, for example <c>ifel</c> to <c>if</c>).</param>
	/// <param name="Selection">
	/// What to select after the edit: the first <c>{placeholder}</c> of the template, or an empty span (the caret position) when
	/// the template has none. Offsets refer to the edited text.
	/// </param>
	public sealed record KeywordExpansion(TextEdit Edit, TextSpan Selection);
}