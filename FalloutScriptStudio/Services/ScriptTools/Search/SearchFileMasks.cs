namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The file masks offered by the folder search.
	/// </summary>
	public static class SearchFileMasks
	{
		/// <summary>The mask list that selects every kind of file the editor works with (legacy "All").</summary>
		public const String AllScriptFiles = "*.ssl;*.h;*.msg";

		private static readonly Char[] _separators = { ';', ',' };

		/// <summary>
		/// Splits a mask list typed by the user. Masks are separated by <c>;</c> or <c>,</c>; <c>All</c> stands for <see cref="AllScriptFiles"/>.
		/// </summary>
		/// <param name="masks">The text.</param>
		/// <returns>The masks without duplicates; <see cref="AllScriptFiles"/> when the text is empty.</returns>
		public static IReadOnlyList<String> Parse(String? masks)
		{
			List<String> result = [];
			String text = String.IsNullOrWhiteSpace(masks) ? AllScriptFiles : masks;
			foreach (String token in text.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				IEnumerable<String> expanded = token.Equals("All", StringComparison.OrdinalIgnoreCase) ? AllScriptFiles.Split(';') : new[] { token };
				foreach (String mask in expanded)
				{
					if (!result.Contains(mask, StringComparer.OrdinalIgnoreCase))
					{
						result.Add(mask);
					}
				}
			}

			if (result.Count == 0)
			{
				result.AddRange(AllScriptFiles.Split(';'));
			}

			return result;
		}
	}
}