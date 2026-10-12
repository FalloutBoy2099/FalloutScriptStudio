namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>
	/// The access keys (the letter after an underscore in a menu header or in the text of a button) must exist, be unique inside a window or
	/// a menu, in every language, and never be shown by an element that does not recognize them.
	/// </summary>
	public sealed class AccessKeyTests
	{
		/// <summary>The Alt+letter shortcuts that are always on in the main window; a menu must not take these letters.</summary>
		private static readonly HashSet<Char> _alwaysOnAltLetters = new HashSet<Char> { 'E', 'R', 'L', 'P', 'M' };

		/// <summary>The Latin key of the Cyrillic letters on a Russian keyboard: Alt+key is read by key, not by the letter it types.</summary>
		private static readonly Dictionary<Char, Char> _russianLayout = "ЙЦУКЕНГШЩЗФЫВАПРОЛДЯЧСМИТЬ".Zip("QWERTYUIOPASDFGHJKLZXCVBNM").ToDictionary(pair => pair.First, pair => pair.Second);

		private static readonly Regex _marker = new Regex(@"(?<!_)_(?!_)(\S)", RegexOptions.Compiled);
		private static readonly Regex _resource = new Regex(@"^\{DynamicResource\s+(Str\.[A-Za-z0-9_.]+)\s*\}$", RegexOptions.Compiled);

		private static readonly Lazy<Dictionary<String, Dictionary<String, String>>> _strings = new Lazy<Dictionary<String, Dictionary<String, String>>>(
			() => SourceTree.LanguageCodes().ToDictionary(code => code, SourceTree.LoadStrings, StringComparer.Ordinal));

		public static IEnumerable<Object[]> Languages()
		{
			return SourceTree.LanguageCodes().Select(code => new Object[] { code });
		}

		public static IEnumerable<Object[]> LanguagesAndViews()
		{
			foreach (String code in SourceTree.LanguageCodes())
			{
				foreach (String file in SourceTree.ViewFiles())
				{
					yield return new Object[] { code, Path.GetRelativePath(SourceTree.AppFolder, file) };
				}
			}
		}

		/// <summary>Gets a value indicating whether a text is a label (a header or a button text), not a sentence that mentions an identifier.</summary>
		private static Boolean IsLabel(String text)
		{
			return text.Length <= 60;
		}

		/// <summary>Gets the access key of a text, or <see langword="null"/>.</summary>
		private static Char? AccessKeyOf(String? text)
		{
			Match match = _marker.Match(text ?? String.Empty);
			return match.Success ? Char.ToUpperInvariant(match.Groups[1].Value[0]) : null;
		}

		/// <summary>Gets the Latin key a letter is typed with (the Alt+letter shortcuts are read by key).</summary>
		private static Char PhysicalKey(String language, Char letter)
		{
			return language == "ru" && _russianLayout.TryGetValue(letter, out Char key) ? key : letter;
		}

		private static String? Resolve(String language, String? value)
		{
			if (value is null)
			{
				return null;
			}

			Match match = _resource.Match(value.Trim());
			if (match.Success)
			{
				return _strings.Value[language].TryGetValue(match.Groups[1].Value, out String? text) ? text : null;
			}

			return value.StartsWith('{') ? null : value;
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void EveryMenuOfTheMenuBarHasAUniqueAccessKey(String language)
		{
			XDocument window = SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml"));
			XElement menu = window.Descendants(SourceTree.Presentation + "Menu").Single();
			List<(String Header, Char? Key)> items = menu.Elements(SourceTree.Presentation + "MenuItem")
				.Select(item => Resolve(language, (String?)item.Attribute("Header")) ?? String.Empty)
				.Select(header => (header, AccessKeyOf(header)))
				.ToList();

			Assert.True(items.Count >= 9, "The menu bar was not found.");
			Findings.AssertNone(items.Where(item => item.Key is null).Select(item => item.Header + " has no access key"));
			Findings.AssertNone(items.GroupBy(item => item.Key).Where(group => group.Count() > 1).Select(group => group.Key + ": " + String.Join(", ", group.Select(item => item.Header))));
			Findings.AssertNone(items.Where(item => _alwaysOnAltLetters.Contains(PhysicalKey(language, item.Key!.Value))).Select(item => item.Header + " takes a letter of an Alt shortcut"));
		}

		[Theory]
		[MemberData(nameof(LanguagesAndViews))]
		public void NoAccessKeyIsUsedTwiceInAWindowOrAMenu(String language, String relativePath)
		{
			XDocument document = SourceTree.Load(Path.Combine(SourceTree.AppFolder, relativePath));
			List<String> duplicates = new List<String>();
			Dictionary<(XElement? Scope, Char Key), List<String>> used = new Dictionary<(XElement? Scope, Char Key), List<String>>();

			foreach (XElement element in document.Descendants())
			{
				if (element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "ControlTemplate" or "DataTemplate" or "HierarchicalDataTemplate" or "Style"))
				{
					continue;
				}

				String? text = Resolve(language, (String?)element.Attribute("Header") ?? (String?)element.Attribute("Content"));
				if (AccessKeyOf(text) is not { } key)
				{
					continue;
				}

				// The items of a menu share the access keys of their menu; the other controls share those of the window.
				XElement? scope = element.Name.LocalName == "MenuItem" ? element.Parent : null;
				(XElement?, Char) id = (scope, key);
				if (!used.TryGetValue(id, out List<String>? owners))
				{
					used[id] = owners = new List<String>();
				}

				owners.Add(text!);
			}

			foreach (KeyValuePair<(XElement? Scope, Char Key), List<String>> entry in used.Where(entry => entry.Value.Count > 1))
			{
				duplicates.Add($"{relativePath} [{language}] access key {entry.Key.Key}: {String.Join(" / ", entry.Value)}");
			}

			Findings.AssertNone(duplicates);
		}

		[Fact]
		public void AnAccessKeyMarkerIsInTheSameKeysOfEveryLanguage()
		{
			HashSet<String> english = _strings.Value["en"].Where(pair => IsLabel(pair.Value) && AccessKeyOf(pair.Value) is not null).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
			Assert.NotEmpty(english);
			List<String> problems = new List<String>();
			foreach ((String language, Dictionary<String, String> strings) in _strings.Value.Where(pair => pair.Key != "en"))
			{
				HashSet<String> marked = strings.Where(pair => IsLabel(_strings.Value["en"][pair.Key]) && IsLabel(pair.Value) && AccessKeyOf(pair.Value) is not null).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
				problems.AddRange(english.Except(marked).Select(key => $"{language}: {key} has no access key"));
				problems.AddRange(marked.Except(english).Select(key => $"{language}: {key} has an access key that English does not have"));
			}

			Findings.AssertNone(problems);
		}

		[Fact]
		public void ATextWithAnAccessKeyIsOnlyShownWhereTheKeyIsRecognized()
		{
			HashSet<String> marked = _strings.Value["en"].Where(pair => IsLabel(pair.Value) && AccessKeyOf(pair.Value) is not null).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
			List<String> problems = new List<String>();

			foreach (String file in Directory.EnumerateFiles(SourceTree.AppFolder, "*.*", SearchOption.AllDirectories)
				.Where(path => (path.EndsWith(".xaml", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
					&& !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
					&& !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
					&& !path.Contains(Path.DirectorySeparatorChar + "Strings" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
			{
				if (file.EndsWith(".cs", StringComparison.Ordinal))
				{
					String code = File.ReadAllText(file);
					problems.AddRange(marked.Where(key => code.Contains('"' + key + '"', StringComparison.Ordinal)).Select(key => $"{SourceTree.Relative(file)}: code reads {key}, which has an access key"));
					continue;
				}

				foreach (XElement element in XDocument.Load(file).Descendants())
				{
					foreach (XAttribute attribute in element.Attributes().Where(a => a.Name.LocalName is not ("Header" or "Content")))
					{
						Match match = Regex.Match(attribute.Value, @"DynamicResource\s+(Str\.[A-Za-z0-9_.]+)");
						if (match.Success && marked.Contains(match.Groups[1].Value))
						{
							problems.Add($"{SourceTree.Relative(file)}: {attribute.Name.LocalName} shows {match.Groups[1].Value}, which has an access key");
						}
					}
				}
			}

			Findings.AssertNone(problems);
		}

		[Theory]
		[InlineData("_Save", 'S')]
		[InlineData("Fi_le", 'L')]
		[InlineData("Save__as", null)]
		[InlineData("文件(_I)", 'I')]
		[InlineData("Plain", null)]
		public void TheAccessKeyIsTheLetterAfterTheUnderscore(String text, Char? expected)
		{
			Assert.Equal(expected, AccessKeyOf(text));
		}
	}
}
