namespace FalloutScriptStudio.Editor.Snippets
{
	using System;
	using Core.Snippets;
	using Localization;

	/// <summary>The text that tells what a snippet inserts, in the current language.</summary>
	public static class SnippetDescriptions
	{
		/// <summary>Gets the description of a snippet: the localized text of a built-in snippet, otherwise the description it holds.</summary>
		/// <param name="snippet">The snippet.</param>
		/// <returns>The description.</returns>
		public static String Describe(Snippet snippet)
		{
			ArgumentNullException.ThrowIfNull(snippet);

			if (snippet.DescriptionKey is { } key && Loc.TryGet(key, out String? text) && !String.IsNullOrWhiteSpace(text))
			{
				return text;
			}

			return snippet.Description;
		}
	}
}