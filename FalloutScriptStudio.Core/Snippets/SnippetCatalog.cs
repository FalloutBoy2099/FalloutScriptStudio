namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Combines the snippets of the sources: the project's over the user's over the built-in ones, per trigger.</summary>
	public static class SnippetCatalog
	{
		/// <summary>Works out the snippets in effect.</summary>
		/// <param name="builtIn">The built-in snippets.</param>
		/// <param name="user">The user's snippets, or <see langword="null"/>.</param>
		/// <param name="project">The snippets of the project, or <see langword="null"/>.</param>
		/// <returns>
		/// One snippet per trigger (the triggers are compared ignoring the case): the one of the latest source, in the order of the triggers.
		/// A trigger whose latest snippet is disabled is left out, so a disabled snippet hides the ones of the earlier sources.
		/// </returns>
		public static IReadOnlyList<Snippet> Merge(IEnumerable<Snippet> builtIn, IEnumerable<Snippet>? user, IEnumerable<Snippet>? project)
		{
			ArgumentNullException.ThrowIfNull(builtIn);

			Dictionary<String, Snippet> byTrigger = new(StringComparer.OrdinalIgnoreCase);
			foreach (IEnumerable<Snippet> layer in new[] { builtIn, user ?? Enumerable.Empty<Snippet>(), project ?? Enumerable.Empty<Snippet>() })
			{
				foreach (Snippet snippet in layer)
				{
					if (snippet.Trigger.Length > 0)
					{
						byTrigger[snippet.Trigger] = snippet;
					}
				}
			}

			return byTrigger.Values.Where(snippet => snippet.IsEnabled).OrderBy(snippet => snippet.Trigger, StringComparer.OrdinalIgnoreCase).ToList();
		}

		/// <summary>Finds the snippet of a trigger.</summary>
		/// <param name="snippets">The snippets in effect.</param>
		/// <param name="trigger">The word typed.</param>
		/// <returns>The snippet, or <see langword="null"/>.</returns>
		public static Snippet? Find(IEnumerable<Snippet> snippets, String trigger)
		{
			ArgumentNullException.ThrowIfNull(snippets);
			ArgumentNullException.ThrowIfNull(trigger);

			foreach (Snippet snippet in snippets)
			{
				if (snippet.IsEnabled && String.Equals(snippet.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
				{
					return snippet;
				}
			}

			return null;
		}
	}
}