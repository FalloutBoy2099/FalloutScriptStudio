namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Replaces regular expression matches (legacy <c>ReplaceCommonText</c> / <c>ReplaceSpecialText</c> / <c>ReplaceMatch</c>).
	/// </summary>
	public static class TextReplacer
	{
		/// <summary>
		/// Creates the edits that replace every match of a regular expression.
		/// </summary>
		/// <param name="regex">The expression.</param>
		/// <param name="text">The text to search.</param>
		/// <param name="newText">The replacement (inserted literally).</param>
		/// <param name="keepFirstGroup">
		/// When <c>true</c> and the expression has a capturing group, the text of the first group is kept and only the rest of
		/// the match is replaced — used to replace a name that follows a keyword such as <c>procedure </c> (legacy <c>ReplaceSpecialText</c>).
		/// </param>
		/// <returns>The edits in the coordinates of <paramref name="text"/>.</returns>
		public static IReadOnlyList<TextEdit> CreateEdits(Regex regex, String text, String newText, Boolean keepFirstGroup)
		{
			ArgumentNullException.ThrowIfNull(regex);
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(newText);

			var edits = new List<TextEdit>();
			foreach (Match match in regex.Matches(text))
			{
				edits.Add(CreateEdit(match, newText, keepFirstGroup));
			}

			return edits;
		}

		/// <summary>
		/// Creates the edit that replaces one match.
		/// </summary>
		/// <param name="match">The match.</param>
		/// <param name="newText">The replacement.</param>
		/// <param name="keepFirstGroup">See <see cref="CreateEdits"/>.</param>
		/// <returns>The edit.</returns>
		public static TextEdit CreateEdit(Match match, String newText, Boolean keepFirstGroup)
		{
			ArgumentNullException.ThrowIfNull(match);

			Int32 shift = keepFirstGroup && match.Groups.Count > 1 ? match.Groups[1].Length : 0;
			return new TextEdit(match.Index + shift, match.Length - shift, newText);
		}

		/// <summary>
		/// Replaces every match of a regular expression in a text.
		/// </summary>
		/// <param name="regex">The expression.</param>
		/// <param name="text">The text.</param>
		/// <param name="newText">The replacement (inserted literally).</param>
		/// <param name="keepFirstGroup">See <see cref="CreateEdits"/>.</param>
		/// <returns>The new text and the edits.</returns>
		public static ReplaceResult Replace(Regex regex, String text, String newText, Boolean keepFirstGroup)
		{
			IReadOnlyList<TextEdit> edits = CreateEdits(regex, text, newText, keepFirstGroup);
			return new ReplaceResult(TextEditApplier.Apply(text, edits), edits);
		}
	}
}