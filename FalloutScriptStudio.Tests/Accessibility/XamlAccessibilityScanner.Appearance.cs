namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;

	/// <content>Rules: hard coded colors and small fonts.</content>
	internal sealed partial class XamlAccessibilityScanner
	{
		private static readonly HashSet<String> _colorProperties = new HashSet<String>(StringComparer.Ordinal)
		{
			"Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CaretBrush", "SelectionBrush", "OpacityMask",
		};

		private static readonly Regex _hexColor = new Regex(@"^#[0-9A-Fa-f]{3,8}$", RegexOptions.Compiled);

		private static readonly Regex _namedColor = new Regex(@"^[A-Za-z]+$", RegexOptions.Compiled);

		/// <summary>Finds hard coded colors: text, background and border colors must come from the theme resources.</summary>
		public IReadOnlyList<String> FindHardCodedColors(String file)
		{
			return this.FindHardCodedColors(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindHardCodedColors(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				foreach (XAttribute attribute in element.Attributes())
				{
					String property = attribute.Name.LocalName;
					if (!_colorProperties.Contains(property) && !(element.Name.LocalName == "Setter" && attribute.Name.LocalName == "Value" && IsColorSetter(element)))
					{
						continue;
					}

					String value = attribute.Value.Trim();
					if (value.Length == 0 || value.StartsWith('{') || String.Equals(value, "Transparent", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (_hexColor.IsMatch(value) || _namedColor.IsMatch(value))
					{
						findings.Add(Describe(file, element, $"hard coded color {value} in {property}, use a theme brush with DynamicResource"));
					}
				}
			}

			return findings;
		}

		/// <summary>Finds font sizes that are too small to read comfortably.</summary>
		public IReadOnlyList<String> FindSmallFonts(String file, Double minimum)
		{
			return this.FindSmallFonts(file, SourceTree.Load(file), minimum);
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindSmallFonts(String file, XDocument document, Double minimum)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				String? value = element.Name.LocalName == "Setter" && (String?)element.Attribute("Property") == "FontSize"
					? (String?)element.Attribute("Value")
					: (String?)element.Attribute("FontSize");
				if (value is not null && Double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Double size) && size < minimum)
				{
					findings.Add(Describe(file, element, $"font size {value} is below {minimum}"));
				}
			}

			return findings;
		}

		private static Boolean IsColorSetter(XElement setter)
		{
			String? property = (String?)setter.Attribute("Property");
			return property is not null && _colorProperties.Contains(property);
		}
	}
}
