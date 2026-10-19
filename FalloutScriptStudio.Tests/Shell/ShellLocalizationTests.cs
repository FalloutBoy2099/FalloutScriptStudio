namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>Guards the texts of the Shell area that carry more than words: line breaks, file filters and placeholders.</summary>
	public sealed class ShellLocalizationTests
	{
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		private static readonly XNamespace XmlNamespace = "http://www.w3.org/XML/1998/namespace";

		public static IEnumerable<Object[]> Languages()
		{
			return Loc.Languages.Where(l => l.Code != Loc.DefaultLanguage).Select(l => new Object[] { l.Code });
		}

		[Fact]
		public void English_texts_are_read_without_an_application_and_keep_their_line_breaks()
		{
			ShellStaRunner.Run(() =>
			{
				Assert.Equal("Ln 5", Loc.Format("Str.Shell.Status.Line", 5));
				Assert.Equal("Project: C:\\x", Loc.Format("Str.Shell.Status.Project", "C:\\x"));
				Assert.Contains("\n\n", Loc.Get("Str.Shell.Dlg.FileAlreadyOpen"), StringComparison.Ordinal);
				Assert.Equal("(none)", Loc.Get("Str.Shell.Toolbar.NoDefine"));
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void Translations_keep_the_line_breaks_and_filter_parts_of_the_english_text(String language)
		{
			Dictionary<String, XElement> english = Read("en");
			Dictionary<String, XElement> translation = Read(language);
			foreach (KeyValuePair<String, XElement> entry in english)
			{
				XElement other = translation[entry.Key];
				Assert.True(Count(entry.Value.Value, '\n') == Count(other.Value, '\n'), $"{language}/{entry.Key}: line breaks differ from English");
				if (other.Value.Contains('\n', StringComparison.Ordinal))
				{
					Assert.Equal("preserve", (String?)other.Attribute(XmlNamespace + "space"));
				}

				if (entry.Key.StartsWith("Str.Shell.Filter.", StringComparison.Ordinal))
				{
					Assert.True(Count(entry.Value.Value, '|') == Count(other.Value, '|'), $"{language}/{entry.Key}: filter parts differ from English");
				}
			}
		}

		private static Int32 Count(String text, Char c)
		{
			return text.Count(x => x == c);
		}

		private static Dictionary<String, XElement> Read(String language)
		{
			String directory = new DirectoryInfo(AppContext.BaseDirectory).Parent!.FullName;
			while (!File.Exists(Path.Combine(directory, "FalloutScriptStudio.sln")))
			{
				directory = Directory.GetParent(directory)!.FullName;
			}

			String file = Path.Combine(directory, "FalloutScriptStudio", "Localization", "Strings", $"Shell.{language}.xaml");
			return XDocument.Load(file).Root!.Elements().ToDictionary(e => (String)e.Attribute(XamlNamespace + "Key")!, e => e, StringComparer.Ordinal);
		}
	}
}