namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Decides when typing <c>"</c>, <c>(</c> or <c>[</c> also inserts the closing character and when typing the closing character
	/// replaces the automatic one (the "auto input of paired characters" of the legacy <c>TextArea_KeyPressed</c>).
	/// </summary>
	/// <remarks>
	/// The decision is made <i>before</i> the typed character is inserted at the caret. The handler keeps no state; the editor
	/// passes whether it armed the overtype after the last opening bracket.
	/// </remarks>
	public sealed class AutoPairHandler
	{
		/// <summary>
		/// Decides what to do for a typed character.
		/// </summary>
		/// <param name="text">The document text before the character is inserted.</param>
		/// <param name="caretOffset">The caret offset.</param>
		/// <param name="typed">The typed character.</param>
		/// <param name="overtypeArmed">Whether an opening bracket was closed automatically a moment ago.</param>
		/// <returns>The action; <see cref="AutoPairAction.None"/> for every character that is not a quote or a bracket.</returns>
		public AutoPairAction Handle(String text, Int32 caretOffset, Char typed, Boolean overtypeArmed)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 caret = Math.Clamp(caretOffset, 0, text.Length);
			Char before = caret > 0 ? text[caret - 1] : '\0';
			Char after = caret < text.Length ? text[caret] : '\0';

			switch (typed)
			{
				case '"':
					return HandleQuote(text, caret, before, after);
				case '(':
				case '[':
					return Char.IsWhiteSpace(after) || after == '\0'
						? new AutoPairAction(AutoPairActionKind.InsertClosing, typed == '[' ? "]" : ")", true)
						: AutoPairAction.None;
				case ')':
				case ']':
					Char opening = typed == ']' ? '[' : '(';
					return overtypeArmed && before == opening && after == typed
						? new AutoPairAction(AutoPairActionKind.RemoveNext, null, false)
						: AutoPairAction.None;
				default:
					return AutoPairAction.None;
			}
		}

		private static AutoPairAction HandleQuote(String text, Int32 caret, Char before, Char after)
		{
			var map = new TextLineMap(text);
			Int32 line = map.GetLineFromOffset(caret);
			Int32 column = caret - map.GetLineStart(line);
			if (QuoteScanner.IsInsideQuotes(map.GetLineText(line), column))
			{
				return AutoPairAction.None;
			}

			Boolean afterEndsLine = after is ' ' or '\r' or '\n' or '\0';
			if ((before == '(' && after == ')') || (before != '"' && afterEndsLine && !Char.IsLetterOrDigit(before)))
			{
				return new AutoPairAction(AutoPairActionKind.InsertClosing, "\"", false);
			}

			return before == '"' && after == '"'
				? new AutoPairAction(AutoPairActionKind.RemoveNext, null, false)
				: AutoPairAction.None;
		}
	}
}
