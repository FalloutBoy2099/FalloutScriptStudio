namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Lists the files of a folder that match file masks (the file listing of the legacy search form and of the global macro rename).
	/// </summary>
	public static class ProjectFileFinder
	{
		private static readonly Char[] _maskSeparators = { ';', ',' };

		/// <summary>The masks of script sources and headers.</summary>
		public static IReadOnlyList<String> ScriptMasks { get; } = new[] { "*.ssl", "*.h" };

		/// <summary>Lists the files that match any of the masks.</summary>
		/// <param name="folder">The folder to search.</param>
		/// <param name="masks">The masks, for example <c>*.ssl</c>; a mask can hold several patterns separated by <c>;</c> or <c>,</c>.</param>
		/// <param name="includeSubfolders">Whether sub folders are searched too.</param>
		/// <returns>
		/// The full paths without duplicates, in discovery order. A missing folder gives an empty list; folders that cannot be read
		/// are skipped.
		/// </returns>
		public static IReadOnlyList<String> EnumerateFiles(String folder, IEnumerable<String> masks, Boolean includeSubfolders)
		{
			ArgumentNullException.ThrowIfNull(folder);
			ArgumentNullException.ThrowIfNull(masks);

			if (!Directory.Exists(folder))
			{
				return Array.Empty<String>();
			}

			var options = new EnumerationOptions
			{
				RecurseSubdirectories = includeSubfolders,
				IgnoreInaccessible = true,
				MatchCasing = MatchCasing.CaseInsensitive,
				MatchType = MatchType.Win32,
			};

			var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			var result = new List<String>();
			foreach (String pattern in masks.SelectMany(m => m.Split(_maskSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
			{
				foreach (String file in Directory.EnumerateFiles(folder, pattern, options))
				{
					if (seen.Add(file))
					{
						result.Add(file);
					}
				}
			}

			return result;
		}

		/// <summary>Lists the scripts (<c>*.ssl</c>) and headers (<c>*.h</c>) below a folder.</summary>
		/// <param name="folder">The project folder.</param>
		/// <returns>The full paths.</returns>
		public static IReadOnlyList<String> EnumerateScriptFiles(String folder)
		{
			return EnumerateFiles(folder, ScriptMasks, true);
		}
	}
}
