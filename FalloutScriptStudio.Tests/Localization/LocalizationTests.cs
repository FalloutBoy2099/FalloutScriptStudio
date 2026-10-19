namespace FalloutScriptStudio.Tests.Localization
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using FalloutScriptStudio.Localization;
	using Xunit;

	/// <summary>
	/// Guards the string dictionaries: every language of every area defines exactly the keys of the English file,
	/// and every <c>Str.Area.Key</c> used by the XAML or the C# sources exists.
	/// </summary>
	public class LocalizationTests
	{
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		private static readonly Regex KeyPattern = new Regex(@"\bStr\.[A-Za-z0-9]+\.[A-Za-z0-9_]+\b", RegexOptions.Compiled);

		public static IEnumerable<Object[]> AreaLanguages()
		{
			foreach (String area in Loc.Areas)
			{
				foreach (LanguageInfo language in Loc.Languages.Where(l => l.Code != Loc.DefaultLanguage))
				{
					yield return new Object[] { area, language.Code };
				}
			}
		}

		[Fact]
		public void EveryAreaHasAnEnglishFile()
		{
			foreach (String area in Loc.Areas)
			{
				Assert.True(File.Exists(StringsFile(area, "en")), $"Missing Localization\\Strings\\{area}.en.xaml");
			}
		}

		[Theory]
		[MemberData(nameof(AreaLanguages))]
		public void TranslationDefinesTheSameKeysAsEnglish(String area, String language)
		{
			String english = StringsFile(area, "en");
			if (!File.Exists(english))
			{
				return;
			}

			String translation = StringsFile(area, language);
			Assert.True(File.Exists(translation), $"Missing Localization\\Strings\\{area}.{language}.xaml");

			HashSet<String> expected = ReadKeys(english).ToHashSet(StringComparer.Ordinal);
			List<String> actualList = ReadKeys(translation).ToList();
			HashSet<String> actual = actualList.ToHashSet(StringComparer.Ordinal);

			Assert.Empty(actualList.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key));
			Assert.Empty(expected.Except(actual));
			Assert.Empty(actual.Except(expected));
		}

		[Theory]
		[MemberData(nameof(AreaLanguages))]
		public void TranslationKeepsTheFormatPlaceholders(String area, String language)
		{
			String english = StringsFile(area, "en");
			String translation = StringsFile(area, language);
			if (!File.Exists(english) || !File.Exists(translation))
			{
				return;
			}

			Dictionary<String, String> source = ReadTexts(english);
			Dictionary<String, String> target = ReadTexts(translation);
			Regex placeholders = new Regex(@"\{\d+\}");
			foreach (KeyValuePair<String, String> entry in source)
			{
				if (!target.TryGetValue(entry.Key, out String? text))
				{
					continue;
				}

				String[] expected = placeholders.Matches(entry.Value).Select(m => m.Value).OrderBy(v => v).ToArray();
				String[] actual = placeholders.Matches(text).Select(m => m.Value).OrderBy(v => v).ToArray();
				Assert.True(expected.SequenceEqual(actual), $"{language}/{entry.Key}: placeholders differ from English");
			}
		}

		[Fact]
		public void EveryKeyUsedBySourcesExists()
		{
			HashSet<String> defined = new(StringComparer.Ordinal);
			foreach (String area in Loc.Areas)
			{
				String file = StringsFile(area, "en");
				if (File.Exists(file))
				{
					defined.UnionWith(ReadKeys(file));
				}
			}

			String root = Path.Combine(RepositoryRoot(), "FalloutScriptStudio");
			List<String> missing = [];
			foreach (String file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
				.Where(f => (f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
					&& !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
					&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
					&& !f.Contains($"{Path.DirectorySeparatorChar}Strings{Path.DirectorySeparatorChar}")))
			{
				foreach (Match match in KeyPattern.Matches(File.ReadAllText(file)))
				{
					// Keys built at run time ("Str.Common.Theme" + name) are checked through their literal prefix only when complete.
					if (!defined.Contains(match.Value) && !defined.Any(key => key.StartsWith(match.Value, StringComparison.Ordinal)))
					{
						missing.Add($"{Path.GetRelativePath(root, file)}: {match.Value}");
					}
				}
			}

			Assert.Empty(missing.Distinct());
		}

		private static String RepositoryRoot()
		{
			DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FalloutScriptStudio.sln")))
			{
				directory = directory.Parent;
			}

			return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found.");
		}

		private static String StringsFile(String area, String language)
		{
			return Path.Combine(RepositoryRoot(), "FalloutScriptStudio", "Localization", "Strings", $"{area}.{language}.xaml");
		}

		private static IEnumerable<String> ReadKeys(String file)
		{
			return XDocument.Load(file).Root!.Elements().Select(element => (String?)element.Attribute(XamlNamespace + "Key")).Where(key => key is not null).Select(key => key!);
		}

		private static Dictionary<String, String> ReadTexts(String file)
		{
			Dictionary<String, String> texts = new(StringComparer.Ordinal);
			foreach (XElement element in XDocument.Load(file).Root!.Elements())
			{
				String? key = (String?)element.Attribute(XamlNamespace + "Key");
				if (key is not null)
				{
					texts[key] = element.Value;
				}
			}

			return texts;
		}
	}
}