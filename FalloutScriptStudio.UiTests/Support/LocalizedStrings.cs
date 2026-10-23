namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.TestSupport;

	/// <summary>
	/// Reads the string dictionaries of the program (<c>FalloutScriptStudio\Localization\Strings</c>) so that a test written with the English
	/// names of the menus and windows finds them in another language of the interface.
	/// </summary>
	internal static class LocalizedStrings
	{
		private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static readonly Dictionary<String, IReadOnlyDictionary<String, String>> Cache = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the text of a key in a language.</summary>
		/// <param name="language">The language code.</param>
		/// <param name="key">The key (<c>Str.Shell.Menu.File</c>).</param>
		/// <returns>The text with the access key marker removed, or <see langword="null"/> when the key does not exist.</returns>
		public static String? Get(String language, String key)
		{
			return Load(language).TryGetValue(key, out String? text) ? Clean(text) : null;
		}

		/// <summary>Translates an English text of the program into a language: the English text is looked up in the dictionaries, the text of the same key in the language is returned.</summary>
		/// <param name="english">The English text, as the tests write it (access key markers and a trailing ellipsis are not needed).</param>
		/// <param name="language">The language code.</param>
		/// <returns>The translated text, or the English text when it is not found.</returns>
		public static String Translate(String english, String language)
		{
			if (language.Equals("en", StringComparison.OrdinalIgnoreCase))
			{
				return english;
			}

			String wanted = Uia.Normalize(english);
			IReadOnlyDictionary<String, String> source = Load("en");
			IReadOnlyDictionary<String, String> target = Load(language);
			foreach ((String key, String text) in source)
			{
				if (Uia.Normalize(text) == wanted && target.TryGetValue(key, out String? translated) && translated.Length > 0)
				{
					return Clean(translated);
				}
			}

			return english;
		}

		private static String Clean(String text)
		{
			// The access key markers: "_File", and the Chinese "保存(_S)".
			String clean = System.Text.RegularExpressions.Regex.Replace(text, @"\(_\w\)", String.Empty);
			return clean.Replace("_", String.Empty, StringComparison.Ordinal);
		}

		private static IReadOnlyDictionary<String, String> Load(String language)
		{
			lock (Cache)
			{
				if (Cache.TryGetValue(language, out IReadOnlyDictionary<String, String>? known))
				{
					return known;
				}

				Dictionary<String, String> strings = new(StringComparer.Ordinal);
				String folder = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Localization", "Strings");
				foreach (String file in Directory.GetFiles(folder, "*." + language + ".xaml").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
				{
					foreach (XElement element in XDocument.Load(file).Root!.Elements())
					{
						String? key = (String?)element.Attribute(Xaml + "Key");
						if (key is not null && key.StartsWith("Str.", StringComparison.Ordinal))
						{
							strings[key] = element.Value;
						}
					}
				}

				Cache[language] = strings;
				return strings;
			}
		}
	}
}