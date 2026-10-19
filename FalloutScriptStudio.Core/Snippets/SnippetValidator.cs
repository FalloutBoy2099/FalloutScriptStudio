namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using TextTools;

	/// <summary>Checks the trigger and the body of a snippet.</summary>
	public static class SnippetValidator
	{
		/// <summary>The longest trigger.</summary>
		public const Int32 MaxTriggerLength = 32;

		/// <summary>Checks a snippet.</summary>
		/// <param name="trigger">The trigger.</param>
		/// <param name="body">The body.</param>
		/// <param name="otherTriggers">The triggers of the other snippets of the same list (a trigger can be used once per list; a snippet of another source may use it to replace or disable it).</param>
		/// <returns>What is wrong.</returns>
		public static SnippetValidation Validate(String trigger, String body, IEnumerable<String> otherTriggers)
		{
			ArgumentNullException.ThrowIfNull(trigger);
			ArgumentNullException.ThrowIfNull(body);
			ArgumentNullException.ThrowIfNull(otherTriggers);

			return new SnippetValidation(CheckTrigger(trigger, otherTriggers), SnippetTemplate.Parse(body).Problems);
		}

		/// <summary>Checks a trigger.</summary>
		/// <param name="trigger">The trigger.</param>
		/// <param name="otherTriggers">The triggers of the other snippets of the same list.</param>
		/// <returns>What is wrong.</returns>
		public static SnippetTriggerIssue CheckTrigger(String trigger, IEnumerable<String> otherTriggers)
		{
			ArgumentNullException.ThrowIfNull(trigger);
			ArgumentNullException.ThrowIfNull(otherTriggers);

			String text = trigger.Trim();
			if (text.Length == 0)
			{
				return SnippetTriggerIssue.Empty;
			}

			if (text.Length > MaxTriggerLength)
			{
				return SnippetTriggerIssue.TooLong;
			}

			if (!IsIdentifier(text))
			{
				return SnippetTriggerIssue.InvalidCharacters;
			}

			if (IsReserved(text))
			{
				return SnippetTriggerIssue.Reserved;
			}

			foreach (String other in otherTriggers)
			{
				if (String.Equals(other.Trim(), text, StringComparison.OrdinalIgnoreCase))
				{
					return SnippetTriggerIssue.Duplicate;
				}
			}

			return SnippetTriggerIssue.None;
		}

		/// <summary>Determines whether a word belongs to the block completion of the editor (it expands by itself and cannot be a trigger).</summary>
		/// <param name="word">The word.</param>
		/// <returns><see langword="true"/> for <c>if</c>, <c>ifel</c>, <c>for</c>, <c>proc</c>...</returns>
		public static Boolean IsReserved(String word)
		{
			ArgumentNullException.ThrowIfNull(word);
			return new KeywordExpander().IsKeyword(word);
		}

		/// <summary>Determines whether a text is a word: letters, digits and underscores, not starting with a digit.</summary>
		/// <param name="text">The text.</param>
		/// <returns><see langword="true"/> for a word.</returns>
		public static Boolean IsIdentifier(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (text.Length == 0 || Char.IsDigit(text[0]))
			{
				return false;
			}

			foreach (Char c in text)
			{
				if (c != '_' && !Char.IsAsciiLetterOrDigit(c))
				{
					return false;
				}
			}

			return true;
		}
	}
}