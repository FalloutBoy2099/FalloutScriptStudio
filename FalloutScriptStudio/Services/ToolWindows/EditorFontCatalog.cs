namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Windows.Media;

	/// <inheritdoc />
	public sealed class EditorFontCatalog : IEditorFontCatalog
	{
		/// <inheritdoc />
		public IReadOnlyList<String> GetFamilyNames(String fontsDirectory)
		{
			ArgumentException.ThrowIfNullOrEmpty(fontsDirectory);

			if (!Directory.Exists(fontsDirectory))
			{
				return Array.Empty<String>();
			}

			HashSet<String> names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			foreach (String file in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
			{
				String? name = TryReadFamilyName(file);
				if (name != null)
				{
					names.Add(name);
				}
			}

			return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static String? TryReadFamilyName(String file)
		{
			try
			{
				GlyphTypeface typeface = new GlyphTypeface(new Uri(Path.GetFullPath(file)));
				IDictionary<CultureInfo, String> names = typeface.FamilyNames;
				if (names.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out String? english))
				{
					return english;
				}

				if (names.TryGetValue(CultureInfo.CurrentUICulture, out String? localized))
				{
					return localized;
				}

				return names.Values.FirstOrDefault();
			}
			catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException or ArgumentException)
			{
				return null;
			}
		}
	}
}
