namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows.Media;
	using Core.Configuration;

	/// <summary>
	/// Finds the editor font: the fonts bundled in the fonts folder (selected by the 1-based index of the settings)
	/// or Consolas.
	/// </summary>
	public sealed class EditorFonts
	{
		/// <summary>The family used when no bundled font is selected or found.</summary>
		public const String FallbackFamily = "Consolas";

		private readonly String _fontsDirectory;
		private IReadOnlyList<FontFamily>? _bundled;

		/// <summary>Initializes a new instance of the <see cref="EditorFonts"/> class.</summary>
		/// <param name="fontsDirectory">The folder with the bundled fonts (it may not exist).</param>
		public EditorFonts(String fontsDirectory)
		{
			this._fontsDirectory = fontsDirectory;
		}

		/// <summary>Gets the bundled font families ordered by name.</summary>
		public IReadOnlyList<FontFamily> BundledFamilies => this._bundled ??= this.DiscoverFonts();

		/// <summary>Resolves the font for the settings.</summary>
		/// <param name="fontIndex">0 for the default font, N for the Nth bundled font.</param>
		/// <returns>The family and its base size in points.</returns>
		public (FontFamily Family, Double BasePoints) Resolve(Int32 fontIndex)
		{
			IReadOnlyList<FontFamily> bundled = this.BundledFamilies;
			if (fontIndex >= 1 && fontIndex <= bundled.Count)
			{
				FontFamily family = bundled[fontIndex - 1];
				return (family, KnownFonts.GetBaseSize(GetDisplayName(family)));
			}

			return (new FontFamily(FallbackFamily), KnownFonts.GetBaseSize(FallbackFamily));
		}

		/// <summary>Gets the name a font family is known by in the settings.</summary>
		/// <param name="family">The family.</param>
		/// <returns>The family name without the folder part.</returns>
		public static String GetDisplayName(FontFamily family)
		{
			String source = family.Source;
			Int32 hash = source.LastIndexOf('#');
			return hash >= 0 ? source.Substring(hash + 1) : source;
		}

		private FontFamily[] DiscoverFonts()
		{
			try
			{
				if (!Directory.Exists(this._fontsDirectory))
				{
					return Array.Empty<FontFamily>();
				}

				String folder = Path.GetFullPath(this._fontsDirectory);
				if (!folder.EndsWith(Path.DirectorySeparatorChar))
				{
					folder += Path.DirectorySeparatorChar;
				}

				return Fonts.GetFontFamilies(new Uri(folder, UriKind.Absolute))
					.OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException or ArgumentException)
			{
				return Array.Empty<FontFamily>();
			}
		}
	}
}