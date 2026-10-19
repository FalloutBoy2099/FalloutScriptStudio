namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Read access to the XAML sources and the string dictionaries of the application, found from the solution folder. The
	/// accessibility rules are checked statically on the markup, so the tests do not depend on a theme, a display or a DPI setting.
	/// </summary>
	internal static class SourceTree
	{
		public static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
		public static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static readonly Lazy<String> _root = new Lazy<String>(FindRoot);

		/// <summary>Gets the solution folder.</summary>
		public static String Root => _root.Value;

		/// <summary>Gets the folder of the application project.</summary>
		public static String AppFolder => Path.Combine(Root, "FalloutScriptStudio");

		/// <summary>Gets every window and view of the application (not the string dictionaries).</summary>
		public static IReadOnlyList<String> ViewFiles()
		{
			return Directory.EnumerateFiles(Path.Combine(AppFolder, "Views"), "*.xaml", SearchOption.AllDirectories)
				.OrderBy(file => file, StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>Gets every window of the application.</summary>
		public static IReadOnlyList<String> WindowFiles()
		{
			return ViewFiles().Where(file => file.EndsWith("Window.xaml", StringComparison.Ordinal)).ToList();
		}

		/// <summary>Gets every resource dictionary and view that is scanned for hard coded colors.</summary>
		public static IReadOnlyList<String> StyledFiles()
		{
			List<String> files = ViewFiles().ToList();
			files.AddRange(Directory.EnumerateFiles(Path.Combine(AppFolder, "Resources"), "*.xaml", SearchOption.AllDirectories));
			files.Add(Path.Combine(AppFolder, "App.xaml"));
			return files;
		}

		/// <summary>Loads a XAML file with line information.</summary>
		public static XDocument Load(String file)
		{
			return XDocument.Load(file, LoadOptions.SetLineInfo);
		}

		/// <summary>Gets the file path relative to the application folder, for the messages.</summary>
		public static String Relative(String file)
		{
			return Path.GetRelativePath(AppFolder, file);
		}

		/// <summary>Reads all the strings of a language (every area).</summary>
		public static Dictionary<String, String> LoadStrings(String language)
		{
			Dictionary<String, String> strings = new(StringComparer.Ordinal);
			foreach (String area in Loc.Areas)
			{
				String file = Path.Combine(AppFolder, "Localization", "Strings", $"{area}.{language}.xaml");
				if (!File.Exists(file))
				{
					continue;
				}

				foreach (XElement element in XDocument.Load(file).Root!.Elements())
				{
					String? key = (String?)element.Attribute(Xaml + "Key");
					if (key is not null)
					{
						strings[key] = element.Value;
					}
				}
			}

			return strings;
		}

		/// <summary>Gets the codes of the supported languages.</summary>
		public static IReadOnlyList<String> LanguageCodes()
		{
			return Loc.Languages.Select(language => language.Code).ToList();
		}

		private static String FindRoot()
		{
			DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FalloutScriptStudio.sln")))
			{
				directory = directory.Parent;
			}

			return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found.");
		}
	}
}